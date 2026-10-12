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

    private async Task<HttpClient> Cliente(string email, HttpClient? admin = null)
    {
        var c = _app.CreateClient();
        var reg = await c.PostAsJsonAsync("/api/auth/register", new { email, password = "Clave.Segura1" });
        Assert.True(reg.IsSuccessStatusCode, await reg.Content.ReadAsStringAsync());
        if (admin is not null) await Aprobar(admin, email);
        Usar(c, await Json(await c.PostAsJsonAsync("/api/auth/login", new { email, password = "Clave.Segura1" })));
        return c;
    }

    internal static async Task<string> IdDe(HttpClient admin, string email)
    {
        foreach (var u in (await Json(await admin.GetAsync("/api/admin/usuarios"))).EnumerateArray())
            if (u.GetProperty("email").GetString() == email) return u.GetProperty("id").GetString()!;
        throw new InvalidOperationException(email);
    }

    internal static async Task Aprobar(HttpClient admin, string email)
    {
        var r = await admin.PostAsync($"/api/admin/usuarios/{await IdDe(admin, email)}/aprobar", null);
        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
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

    internal static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task FlujoCompletoMultiusuario()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _app.CreateClient().GetAsync("/api/estado")).StatusCode);

        var ana = await Cliente("ana@ejemplo.co");   // primera en registrarse: administradora
        var beto = await Cliente("beto@ejemplo.co", admin: ana);

        // El salario lo define la administradora; Beto solo lo consulta.
        await Json(await ana.PostAsJsonAsync("/api/perfil/salarios", new { vigenteDesde = "2026-01-01", salarioMensual = 2_100_000 }));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await beto.PostAsJsonAsync("/api/perfil/salarios", new { vigenteDesde = "2026-01-01", salarioMensual = 4_200_000 })).StatusCode);
        var idBeto = await IdDe(ana, "beto@ejemplo.co");
        await Json(await ana.PostAsJsonAsync($"/api/admin/usuarios/{idBeto}/salarios", new { vigenteDesde = "2026-01-01", salarioMensual = 4_200_000 }));
        Assert.Equal(4_200_000m, (await Json(await beto.GetAsync("/api/perfil/salarios")))[0].GetProperty("salarioMensual").GetDecimal());

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
        var domingo = await Json(await beto.PostAsJsonAsync("/api/sesiones", new
        {
            inicioUtc = ZonaHoraria.AUtc(new DateTime(2026, 10, 11, 10, 0, 0)),
            finUtc = ZonaHoraria.AUtc(new DateTime(2026, 10, 11, 11, 0, 0)),
        }));
        Assert.Equal("Pendiente", domingo.GetProperty("revision").GetString());
        var porAprobar = await Json(await beto.GetAsync("/api/liquidacion"));
        Assert.Equal(0m, porAprobar.GetProperty("valorTotal").GetDecimal());
        Assert.Equal(43_000m, porAprobar.GetProperty("valorPorAprobar").GetDecimal());

        // La administradora lo aprueba y ya suma.
        await Json(await ana.PostAsync($"/api/admin/sesiones/{domingo.GetProperty("id").GetInt32()}/aprobar", null));
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

    [Fact]
    public async Task ApiResponde401SinRedirigir()
    {
        // La app móvil necesita un 401 para saber que debe volver a pedir el ingreso.
        var c = _app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var r = await c.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "vencido" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Null(r.Headers.Location);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/estado")).StatusCode);
    }
}

public class RegistroManualTests : IClassFixture<Fabrica>
{
    private readonly Fabrica _app;

    public RegistroManualTests(Fabrica app) => _app = app;

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

    private static async Task<decimal> Total(HttpClient c) =>
        (await Json(await c.GetAsync("/api/liquidacion"))).GetProperty("valorTotal").GetDecimal();

    [Fact]
    public async Task PorCantidadDeHorasAjusteYObservaciones()
    {
        var caro = await Cliente("caro@ejemplo.co");
        var dani = await Cliente("dani@ejemplo.co", admin: caro);
        await Json(await caro.PostAsJsonAsync("/api/perfil/salarios", new { vigenteDesde = "2026-01-01", salarioMensual = 2_100_000 })); // 10.000/h

        // Jueves 1 de octubre, 2 horas sin hora de inicio: se asume 6:00 p. m. → 1 h diurna + 1 h nocturna.
        var jueves = await Json(await caro.PostAsJsonAsync("/api/sesiones/por-horas",
            new { fecha = "2026-10-01", horas = 2, nota = "Cierre de inventario" }));
        Assert.Equal(7200, jueves.GetProperty("segundosAcumulados").GetInt64());
        Assert.Equal("Cierre de inventario", jueves.GetProperty("nota").GetString());
        Assert.Equal(30_000m, await Total(caro));

        // Domingo 4 de octubre, 1 hora desde las 10:00 a. m.: dominical diurna (+115 %).
        var domingo = await Json(await caro.PostAsJsonAsync("/api/sesiones/por-horas",
            new { fecha = "2026-10-04", horaInicio = "10:00:00", horas = 1 }));
        Assert.Equal(51_500m, await Total(caro));

        // Ajuste manual: el domingo fueron 2 horas, con observaciones.
        var id = domingo.GetProperty("id").GetInt32();
        var ajustada = await Json(await caro.PutAsJsonAsync($"/api/sesiones/{id}",
            new { fecha = "2026-10-04", horaInicio = "10:00:00", horas = 2, nota = "Soporte al cierre" }));
        Assert.Equal(7200, ajustada.GetProperty("segundosAcumulados").GetInt64());
        Assert.Equal(73_000m, await Total(caro));

        // Observaciones de una sesión en curso; no se puede ajustar el tiempo hasta finalizarla.
        var abierta = await Json(await caro.PostAsync("/api/sesiones/iniciar", null));
        var idAbierta = abierta.GetProperty("id").GetInt32();
        var conNota = await Json(await caro.PutAsJsonAsync($"/api/sesiones/{idAbierta}/nota", new { nota = "  Migración de datos  " }));
        Assert.Equal("Migración de datos", conNota.GetProperty("nota").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await caro.PutAsJsonAsync($"/api/sesiones/{idAbierta}",
            new { fecha = "2026-10-08", horaInicio = "10:00:00", horas = 1 })).StatusCode);

        // Otro usuario no puede tocar sus registros.
        Assert.Equal(HttpStatusCode.NotFound, (await dani.PutAsJsonAsync($"/api/sesiones/{id}/nota", new { nota = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await dani.PutAsJsonAsync($"/api/sesiones/{id}",
            new { fecha = "2026-10-04", horaInicio = "10:00:00", horas = 1 })).StatusCode);

        // Validaciones: horas en el futuro, cero horas, más de 24 y observaciones demasiado largas.
        Assert.Equal(HttpStatusCode.BadRequest, (await caro.PostAsJsonAsync("/api/sesiones/por-horas",
            new { fecha = "2026-10-09", horas = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await caro.PostAsJsonAsync("/api/sesiones/por-horas",
            new { fecha = "2026-10-05", horas = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await caro.PostAsJsonAsync("/api/sesiones/por-horas",
            new { fecha = "2026-10-05", horas = 25 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await caro.PutAsJsonAsync($"/api/sesiones/{id}/nota",
            new { nota = new string('a', 2001) })).StatusCode);
    }
}

public class AprobacionesTests : IClassFixture<Fabrica>
{
    private readonly Fabrica _app;

    public AprobacionesTests(Fabrica app) => _app = app;

    private const string Clave = "Clave.Segura1";

    private async Task<HttpResponseMessage> Ingresar(HttpClient c, string email) =>
        await c.PostAsJsonAsync("/api/auth/login", new { email, password = Clave });

    private async Task<(HttpClient cliente, string refresh)> Sesion(string email)
    {
        var c = _app.CreateClient();
        var t = await ApiTests.Json(await Ingresar(c, email));
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t.GetProperty("accessToken").GetString());
        return (c, t.GetProperty("refreshToken").GetString()!);
    }

    private async Task Registrar(string email) =>
        Assert.True((await _app.CreateClient().PostAsJsonAsync("/api/auth/register", new { email, password = Clave })).IsSuccessStatusCode);

    [Fact]
    public async Task RegistroPendienteYRevisionDeHoras()
    {
        await Registrar("jefa@ejemplo.co");
        var (jefa, _) = await Sesion("jefa@ejemplo.co");
        var cuenta = await ApiTests.Json(await jefa.GetAsync("/api/cuenta"));
        Assert.True(cuenta.GetProperty("esAdmin").GetBoolean());
        Assert.Equal("Activo", cuenta.GetProperty("estado").GetString());

        // Quien se registra después no puede ingresar hasta que lo aprueben.
        await Registrar("emp@ejemplo.co");
        var negado = await Ingresar(_app.CreateClient(), "emp@ejemplo.co");
        Assert.Equal(HttpStatusCode.Unauthorized, negado.StatusCode);
        Assert.Contains("NotAllowed", await negado.Content.ReadAsStringAsync());
        var usuarios = await ApiTests.Json(await jefa.GetAsync("/api/admin/usuarios"));
        Assert.Equal("Pendiente", usuarios[0].GetProperty("estado").GetString()); // los pendientes van primero

        await ApiTests.Aprobar(jefa, "emp@ejemplo.co");
        var (emp, refreshEmp) = await Sesion("emp@ejemplo.co");
        Assert.False((await ApiTests.Json(await emp.GetAsync("/api/cuenta"))).GetProperty("esAdmin").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await emp.GetAsync("/api/admin/sesiones")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await emp.PutAsJsonAsync("/api/perfil/recargos", new { modo = "Ley" })).StatusCode);

        // Las horas de la jefa quedan aprobadas solas; las del empleado, pendientes.
        var propia = await ApiTests.Json(await jefa.PostAsJsonAsync("/api/sesiones/por-horas", new { fecha = "2026-10-07", horas = 1 }));
        Assert.Equal("Aprobada", propia.GetProperty("revision").GetString());
        var registro = await ApiTests.Json(await emp.PostAsJsonAsync("/api/sesiones/por-horas", new { fecha = "2026-10-07", horas = 2, nota = "Cierre" }));
        var id = registro.GetProperty("id").GetInt32();
        Assert.Equal("Pendiente", registro.GetProperty("revision").GetString());

        var bandeja = await ApiTests.Json(await jefa.GetAsync("/api/admin/sesiones"));
        Assert.Equal(1, bandeja.GetArrayLength());
        Assert.Equal("emp@ejemplo.co", bandeja[0].GetProperty("email").GetString());

        // Rechazar exige un motivo; el empleado corrige y vuelve a quedar pendiente.
        Assert.Equal(HttpStatusCode.BadRequest, (await jefa.PostAsJsonAsync($"/api/admin/sesiones/{id}/rechazar", new { comentario = " " })).StatusCode);
        var rechazada = await ApiTests.Json(await jefa.PostAsJsonAsync($"/api/admin/sesiones/{id}/rechazar", new { comentario = "Fue 1 hora" }));
        Assert.Equal("Rechazada", rechazada.GetProperty("revision").GetString());
        Assert.Equal("Fue 1 hora", rechazada.GetProperty("comentarioRevision").GetString());
        var corregida = await ApiTests.Json(await emp.PutAsJsonAsync($"/api/sesiones/{id}", new { fecha = "2026-10-07", horas = 1, nota = "Cierre" }));
        Assert.Equal("Pendiente", corregida.GetProperty("revision").GetString());
        Assert.Equal(JsonValueKind.Null, corregida.GetProperty("comentarioRevision").ValueKind);

        // Aprobada, el empleado ya no la puede cambiar ni borrar.
        await ApiTests.Json(await jefa.PostAsync($"/api/admin/sesiones/{id}/aprobar", null));
        Assert.Equal(HttpStatusCode.Conflict, (await emp.PutAsJsonAsync($"/api/sesiones/{id}", new { fecha = "2026-10-07", horas = 3 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await emp.PutAsJsonAsync($"/api/sesiones/{id}/nota", new { nota = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await emp.DeleteAsync($"/api/sesiones/{id}")).StatusCode);
        Assert.Equal(0, (await ApiTests.Json(await jefa.GetAsync("/api/admin/sesiones"))).GetArrayLength());
        Assert.Equal(2, (await ApiTests.Json(await jefa.GetAsync("/api/admin/sesiones?revision=Todas&desde=2026-10-01&hasta=2026-10-15"))).GetArrayLength());

        // Una sesión en curso no se puede revisar todavía.
        var abierta = await ApiTests.Json(await emp.PostAsync("/api/sesiones/iniciar", null));
        Assert.Equal(HttpStatusCode.Conflict,
            (await jefa.PostAsync($"/api/admin/sesiones/{abierta.GetProperty("id").GetInt32()}/aprobar", null)).StatusCode);

        // La jefa no se quita su propio rol ni se desactiva.
        var idJefa = await ApiTests.IdDe(jefa, "jefa@ejemplo.co");
        Assert.Equal(HttpStatusCode.Conflict, (await jefa.PutAsJsonAsync($"/api/admin/usuarios/{idJefa}/admin", new { esAdmin = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await jefa.PostAsync($"/api/admin/usuarios/{idJefa}/desactivar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await jefa.GetAsync("/api/admin/usuarios/no-existe/salarios")).StatusCode);

        // Desactivado, el empleado no renueva su sesión ni vuelve a ingresar.
        var idEmp = await ApiTests.IdDe(jefa, "emp@ejemplo.co");
        Assert.Equal(HttpStatusCode.NoContent, (await jefa.PostAsync($"/api/admin/usuarios/{idEmp}/desactivar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _app.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refreshEmp })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Ingresar(_app.CreateClient(), "emp@ejemplo.co")).StatusCode);
    }
}
