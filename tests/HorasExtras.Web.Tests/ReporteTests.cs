using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HorasExtras.Web.Tests;

public class ReporteTests : IClassFixture<Fabrica>
{
    private readonly Fabrica _app;

    public ReporteTests(Fabrica app) => _app = app;

    private async Task<HttpClient> Cliente(string email, HttpClient? admin = null)
    {
        var c = _app.CreateClient();
        Assert.True((await c.PostAsJsonAsync("/api/auth/register", new { email, password = "Clave.Segura1" })).IsSuccessStatusCode);
        if (admin is not null) await ApiTests.Aprobar(admin, email);
        var t = await Json(await c.PostAsJsonAsync("/api/auth/login", new { email, password = "Clave.Segura1" }));
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t.GetProperty("accessToken").GetString());
        return c;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task EsPdf(HttpResponseMessage r)
    {
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("application/pdf", r.Content.Headers.ContentType?.MediaType);
        Assert.Equal("liquidacion-2026-10-01-a-2026-10-15.pdf", r.Content.Headers.ContentDisposition?.FileNameStar);
        var bytes = await r.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task GuardarYDescargarElPdfConElDetalleDeRegistros()
    {
        var eva = await Cliente("eva@ejemplo.co");
        var fer = await Cliente("fer@ejemplo.co", admin: eva);
        await Json(await eva.PostAsJsonAsync("/api/perfil/salarios", new { vigenteDesde = "2026-01-01", salarioMensual = 2_100_000 })); // 10.000/h

        await Json(await eva.PostAsJsonAsync("/api/sesiones/por-horas", new { fecha = "2026-10-01", horas = 2, nota = "Cierre de inventario" }));
        await Json(await eva.PostAsJsonAsync("/api/sesiones/por-horas", new { fecha = "2026-10-04", horaInicio = "10:00:00", horas = 1 }));

        var guardada = await Json(await eva.PostAsJsonAsync("/api/liquidaciones", new { desde = "2026-10-01", hasta = "2026-10-15" }));
        var id = guardada.GetProperty("id").GetInt32();

        // El detalle guardado trae cada registro con lo que aporta y sus observaciones.
        var detalle = await Json(await eva.GetAsync($"/api/liquidaciones/{id}"));
        var registros = detalle.GetProperty("registros");
        Assert.Equal(2, registros.GetArrayLength());
        Assert.Equal("Cierre de inventario", registros[0].GetProperty("nota").GetString());
        Assert.Equal(30_000m, registros[0].GetProperty("valor").GetDecimal()); // 1 h diurna + 1 h nocturna
        Assert.Equal(2, registros[0].GetProperty("categorias").GetArrayLength());
        Assert.Equal(21_500m, registros[1].GetProperty("valor").GetDecimal()); // dominical diurna (+115 %)

        await EsPdf(await eva.GetAsync($"/api/liquidaciones/{id}/pdf"));

        // Nadie más puede bajar el PDF de otra persona.
        Assert.Equal(HttpStatusCode.NotFound, (await fer.GetAsync($"/api/liquidaciones/{id}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fer.PostAsync($"/api/liquidaciones/{id}/enlace-pdf", null)).StatusCode);

        // La app móvil pide un enlace firmado y lo abre sin token.
        var enlace = await Json(await eva.PostAsync($"/api/liquidaciones/{id}/enlace-pdf", null));
        var anonimo = _app.CreateClient();
        await EsPdf(await anonimo.GetAsync(enlace.GetProperty("ruta").GetString()));
        Assert.Equal(HttpStatusCode.Gone, (await anonimo.GetAsync("/api/descargas/liquidacion/no-sirve")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync($"/api/liquidaciones/{id}/pdf")).StatusCode);
    }
}
