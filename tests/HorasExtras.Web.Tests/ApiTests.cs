using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HorasExtras.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace HorasExtras.Web.Tests;

public class Fabrica : WebApplicationFactory<Program>
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"horasextras-{Guid.NewGuid():N}.db");

    // Jueves 8 de octubre de 2026, 6:00 p. m. en Colombia.
    public FakeTimeProvider Reloj { get; } = new(new DateTimeOffset(2026, 10, 8, 23, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_db}");
        builder.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(Reloj)));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_db);
    }
}

public class ApiTests : IClassFixture<Fabrica>
{
    private readonly Fabrica _app;

    public ApiTests(Fabrica app) => _app = app;

    private readonly Dictionary<HttpClient, string> _refresh = [];

    private async Task<HttpClient> Cliente(string email)
    {
        var c = _app.CreateClient();
        var reg = await c.PostAsJsonAsync("/api/auth/register", new { email, password = "Clave.Segura1" });
        Assert.True(reg.IsSuccessStatusCode, await reg.Content.ReadAsStringAsync());
        Usar(c, await Json(await c.PostAsJsonAsync("/api/auth/login", new { email, password = "Clave.Segura1" })));
        return c;
    }

    private void Usar(HttpClient c, JsonElement tokens)
    {
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());
        _refresh[c] = tokens.GetProperty("refreshToken").GetString()!;
    }

    /// <summary>Avanza el reloj y renueva los tokens (el de acceso dura 1 hora), como haría la app móvil.</summary>
    private async Task Avanzar(TimeSpan t)
    {
        _app.Reloj.Advance(t);
        foreach (var c in _refresh.Keys.ToList())
            Usar(c, await Json(await c.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = _refresh[c] })));
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task FlujoCompletoMultiusuario()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _app.CreateClient().GetAsync("/api/estado")).StatusCode);

        var ana = await Cliente("ana@ejemplo.co");   // primera en registrarse: administradora
        var beto = await Cliente("beto@ejemplo.co");

        await Json(await ana.PostAsJsonAsync("/api/perfil/salarios", new { vigenteDesde = "2026-01-01", salarioMensual = 2_100_000 }));
        await Json(await beto.PostAsJsonAsync("/api/perfil/salarios", new { vigenteDesde = "2026-01-01", salarioMensual = 4_200_000 }));

        // Ana: 6:00 p. m. a 7:00 p. m. (diurna), pausa 30 min, 7:30 a 8:30 p. m. (nocturna).
        var s = await Json(await ana.PostAsync("/api/sesiones/iniciar", null));
        Assert.Equal("EnCurso", s.GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await ana.PostAsync("/api/sesiones/iniciar", null)).StatusCode);
        await Avanzar(TimeSpan.FromHours(1));
        await Json(await ana.PostAsync("/api/sesiones/pausar", null));
        await Avanzar(TimeSpan.FromMinutes(30));
        await Json(await ana.PostAsync("/api/sesiones/continuar", null));
        await Avanzar(TimeSpan.FromHours(1));

        var estado = await Json(await ana.GetAsync("/api/estado"));
        Assert.Equal(7200, estado.GetProperty("segundosAcumulados").GetInt64());

        // Liquidar en cualquier momento, con la sesión abierta.
        var liq = await Json(await ana.GetAsync("/api/liquidacion"));
        Assert.Equal(30_000m, liq.GetProperty("valorTotal").GetDecimal()); // 12.500 + 17.500
        Assert.Equal(1, liq.GetProperty("diasTrabajados").GetInt32());

        await Json(await ana.PostAsync("/api/sesiones/finalizar", null));
        Assert.Equal(JsonValueKind.Null, (await Json(await ana.GetAsync("/api/estado"))).ValueKind);

        // Beto no ve las horas de Ana.
        Assert.Equal(0, (await Json(await beto.GetAsync("/api/sesiones"))).GetArrayLength());
        Assert.Equal(0m, (await Json(await beto.GetAsync("/api/liquidacion"))).GetProperty("valorTotal").GetDecimal());
        var idSesionAna = (await Json(await ana.GetAsync("/api/sesiones")))[0].GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound, (await beto.DeleteAsync($"/api/sesiones/{idSesionAna}")).StatusCode);

        // Registro manual de Beto el domingo 11 de octubre, 10 a 11 a. m.: dominical diurna (+115 %).
        await Avanzar(TimeSpan.FromDays(4));
        await Json(await beto.PostAsJsonAsync("/api/sesiones", new
        {
            inicioUtc = ZonaHoraria.AUtc(new DateTime(2026, 10, 11, 10, 0, 0)),
            finUtc = ZonaHoraria.AUtc(new DateTime(2026, 10, 11, 11, 0, 0)),
        }));
        Assert.Equal(43_000m, (await Json(await beto.GetAsync("/api/liquidacion"))).GetProperty("valorTotal").GetDecimal());

        // Guardar la liquidación de la quincena.
        var guardada = await Json(await ana.PostAsJsonAsync("/api/liquidaciones", new { }));
        Assert.Equal(30_000m, guardada.GetProperty("valorTotal").GetDecimal());
        Assert.Equal(1, (await Json(await ana.GetAsync("/api/liquidaciones"))).GetArrayLength());

        // Solo la administradora ve el resumen de todos.
        Assert.Equal(HttpStatusCode.Forbidden, (await beto.GetAsync("/api/admin/usuarios")).StatusCode);
        Assert.Equal(2, (await Json(await ana.GetAsync("/api/admin/usuarios"))).GetArrayLength());

        // Validaciones.
        Assert.Equal(HttpStatusCode.BadRequest, (await beto.PostAsJsonAsync("/api/sesiones", new
        {
            inicioUtc = _app.Reloj.GetUtcNow().UtcDateTime.AddHours(1),
            finUtc = _app.Reloj.GetUtcNow().UtcDateTime.AddHours(2),
        })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await ana.PostAsJsonAsync("/api/perfil/salarios", new { vigenteDesde = "2026-01-01", salarioMensual = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ana.GetAsync("/api/liquidacion?desde=2026-10-15&hasta=2026-10-01")).StatusCode);
    }

    [Fact]
    public async Task PaginasWebPidenLogin()
    {
        var c = _app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var r = await c.GetAsync("/liquidar");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Account/Login", r.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Account/Login")).StatusCode);
    }
}
