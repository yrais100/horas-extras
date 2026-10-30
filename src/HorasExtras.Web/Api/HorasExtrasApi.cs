using System.Security.Claims;
using HorasExtras.Application;
using HorasExtras.Domain;
using HorasExtras.Domain.Calculo;
using HorasExtras.Domain.Entidades;
using HorasExtras.Infrastructure;
using HorasExtras.Web.Servicios;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HorasExtras.Web.Api;

public static class PoliticasApi
{
    public const string Usuario = "api-usuario";
    public const string Admin = "api-admin";
}

/// <summary>
/// API REST para la app móvil. Las rutas actúan sobre el usuario autenticado, salvo /api/admin, que es
/// solo para administradores.
/// </summary>
public static class HorasExtrasApi
{
    public static void MapHorasExtrasApi(this WebApplication app)
    {
        // /api/auth/register, /api/auth/login, /api/auth/refresh, /api/auth/manage/info, ...
        app.MapGroup("/api/auth").WithTags("Autenticación").MapIdentityApi<Usuario>();

        var api = app.MapGroup("/api").RequireAuthorization(PoliticasApi.Usuario).AddEndpointFilter(TraducirErrores);

        api.MapGet("/cuenta", (AdministracionService a, ClaimsPrincipal u) => a.CuentaAsync(u.Id())).WithTags("Cuenta");

        var sesiones = api.MapGroup("/sesiones").WithTags("Sesiones");
        api.MapGet("/estado", (RegistroHorasService s, ClaimsPrincipal u, CancellationToken ct) => s.EstadoAsync(u.Id(), ct))
            .WithTags("Sesiones");
        sesiones.MapPost("/iniciar", (RegistroHorasService s, ClaimsPrincipal u, NotaInput? body, CancellationToken ct) =>
            s.IniciarAsync(u.Id(), body?.Nota, ct));
        sesiones.MapPost("/pausar", (RegistroHorasService s, ClaimsPrincipal u, CancellationToken ct) => s.PausarAsync(u.Id(), ct));
        sesiones.MapPost("/continuar", (RegistroHorasService s, ClaimsPrincipal u, CancellationToken ct) => s.ContinuarAsync(u.Id(), ct));
        sesiones.MapPost("/finalizar", (RegistroHorasService s, ClaimsPrincipal u, CancellationToken ct) => s.FinalizarAsync(u.Id(), ct));
        sesiones.MapPost("/", (RegistroHorasService s, ClaimsPrincipal u, RegistroManualInput body, CancellationToken ct) =>
            s.RegistrarManualAsync(u.Id(), body, ct));
        sesiones.MapPost("/por-horas", (RegistroHorasService s, ClaimsPrincipal u, RegistroPorHorasInput body, CancellationToken ct) =>
            s.RegistrarPorHorasAsync(u.Id(), body, ct));
        sesiones.MapPut("/{id:int}", (RegistroHorasService s, ClaimsPrincipal u, int id, RegistroPorHorasInput body, CancellationToken ct) =>
            s.AjustarAsync(u.Id(), id, body, ct));
        sesiones.MapPut("/{id:int}/nota", (RegistroHorasService s, ClaimsPrincipal u, int id, NotaInput body, CancellationToken ct) =>
            s.CambiarNotaAsync(u.Id(), id, body.Nota, ct));
        sesiones.MapGet("/", (RegistroHorasService s, LiquidacionService l, ClaimsPrincipal u, DateOnly? desde, DateOnly? hasta, CancellationToken ct) =>
            s.ListarAsync(u.Id(), Periodo(l, desde, hasta), ct));
        sesiones.MapDelete("/{id:int}", async (RegistroHorasService s, ClaimsPrincipal u, int id, CancellationToken ct) =>
        {
            await s.EliminarAsync(u.Id(), id, ct);
            return Results.NoContent();
        });

        var liq = api.MapGroup("/liquidaciones").WithTags("Liquidación");
        api.MapGet("/liquidacion", (LiquidacionService l, ClaimsPrincipal u, DateOnly? desde, DateOnly? hasta, CancellationToken ct) =>
            l.CalcularAsync(u.Id(), Periodo(l, desde, hasta), ct)).WithTags("Liquidación");
        liq.MapPost("/", (LiquidacionService l, ClaimsPrincipal u, PeriodoInput? body, CancellationToken ct) =>
            l.GuardarAsync(u.Id(), Periodo(l, body?.Desde, body?.Hasta), ct));
        liq.MapGet("/", (LiquidacionService l, ClaimsPrincipal u, CancellationToken ct) => l.HistorialAsync(u.Id(), ct));
        liq.MapGet("/{id:int}", (LiquidacionService l, ClaimsPrincipal u, int id, CancellationToken ct) => l.DetalleGuardadoAsync(u.Id(), id, ct));
        liq.MapDelete("/{id:int}", async (LiquidacionService l, ClaimsPrincipal u, int id, CancellationToken ct) =>
        {
            await l.EliminarAsync(u.Id(), id, ct);
            return Results.NoContent();
        });
        // Reporte en PDF. La web lo descarga con su cookie; la app móvil pide un enlace firmado y lo abre en el navegador.
        liq.MapGet("/{id:int}/pdf", async (ReportesService r, ClaimsPrincipal u, int id, CancellationToken ct) =>
            Pdf(await r.LiquidacionAsync(u.Id(), id, ct)));
        liq.MapPost("/{id:int}/enlace-pdf", async (ReportesService r, LiquidacionService l, ClaimsPrincipal u, int id, CancellationToken ct) =>
        {
            await l.ReporteAsync(u.Id(), id, ct); // 404 si no es suya
            // Ruta relativa: detrás del proxy de Azure el esquema y el host que ve el servidor no son los públicos.
            var ruta = $"/api/descargas/liquidacion/{Uri.EscapeDataString(r.FirmarEnlace(u.Id(), id))}";
            return new EnlaceDto(ruta, DateTime.UtcNow + ReportesService.VigenciaEnlace);
        });
        app.MapGet("/api/descargas/liquidacion/{token}", async (ReportesService r, string token, CancellationToken ct) =>
            r.LeerEnlace(token) is var (usuarioId, id)
                ? Pdf(await r.LiquidacionAsync(usuarioId, id, ct))
                : Results.Problem("El enlace de descarga venció o no es válido. Vuelve a pedirlo desde la app.", statusCode: StatusCodes.Status410Gone))
            .AllowAnonymous().AddEndpointFilter(TraducirErrores).WithTags("Liquidación");

        // El salario y los recargos los define el administrador; cada usuario solo los consulta.
        var perfil = api.MapGroup("/perfil").WithTags("Perfil");
        perfil.MapGet("/salarios", (PerfilService p, ClaimsPrincipal u, CancellationToken ct) => p.SalariosAsync(u.Id(), ct));
        perfil.MapPost("/salarios", (PerfilService p, ClaimsPrincipal u, SalarioInput body, CancellationToken ct) =>
            p.GuardarSalarioAsync(u.Id(), body, ct)).RequireAuthorization(PoliticasApi.Admin);
        perfil.MapDelete("/salarios/{id:int}", async (PerfilService p, ClaimsPrincipal u, int id, CancellationToken ct) =>
        {
            await p.EliminarSalarioAsync(u.Id(), id, ct);
            return Results.NoContent();
        }).RequireAuthorization(PoliticasApi.Admin);
        perfil.MapGet("/recargos", (PerfilService p, ClaimsPrincipal u, CancellationToken ct) => p.RecargosAsync(u.Id(), ct));
        perfil.MapPut("/recargos", (PerfilService p, ClaimsPrincipal u, RecargosDto body, CancellationToken ct) =>
            p.GuardarRecargosAsync(u.Id(), body, ct)).RequireAuthorization(PoliticasApi.Admin);

        var refe = api.MapGroup("/referencia").WithTags("Referencia");
        refe.MapGet("/ley", (DateOnly? fecha, TimeProvider reloj) => ValoresLey.Para(fecha ?? ZonaHoraria.Hoy(reloj)));
        refe.MapGet("/festivos", (int? anio, TimeProvider reloj) => Festivos.DelAnio(anio ?? ZonaHoraria.Hoy(reloj).Year));

        MapAdmin(api.MapGroup("/admin").RequireAuthorization(PoliticasApi.Admin).WithTags("Administración"));
    }

    private static void MapAdmin(RouteGroupBuilder admin)
    {
        admin.MapGet("/usuarios", (AdministracionService a, CancellationToken ct) => a.UsuariosAsync(ct));
        admin.MapPost("/usuarios/{id}/aprobar", async (AdministracionService a, string id) =>
        {
            await a.AprobarAsync(id);
            return Results.NoContent();
        });
        admin.MapPost("/usuarios/{id}/desactivar", async (AdministracionService a, ClaimsPrincipal u, string id) =>
        {
            await a.DesactivarAsync(u.Id(), id);
            return Results.NoContent();
        });
        admin.MapPut("/usuarios/{id}/admin", async (AdministracionService a, ClaimsPrincipal u, string id, RolInput body) =>
        {
            await a.CambiarAdminAsync(u.Id(), id, body.EsAdmin);
            return Results.NoContent();
        });

        // Salario, recargos y liquidación de cualquier usuario.
        var usuario = admin.MapGroup("/usuarios/{id}").AddEndpointFilter(async (ctx, next) =>
        {
            await ctx.HttpContext.RequestServices.GetRequiredService<AdministracionService>().Exigir((string)ctx.HttpContext.GetRouteValue("id")!);
            return await next(ctx);
        });
        usuario.MapGet("/salarios", (PerfilService p, string id, CancellationToken ct) => p.SalariosAsync(id, ct));
        usuario.MapPost("/salarios", (PerfilService p, string id, SalarioInput body, CancellationToken ct) => p.GuardarSalarioAsync(id, body, ct));
        usuario.MapDelete("/salarios/{salarioId:int}", async (PerfilService p, string id, int salarioId, CancellationToken ct) =>
        {
            await p.EliminarSalarioAsync(id, salarioId, ct);
            return Results.NoContent();
        });
        usuario.MapGet("/recargos", (PerfilService p, string id, CancellationToken ct) => p.RecargosAsync(id, ct));
        usuario.MapPut("/recargos", (PerfilService p, string id, RecargosDto body, CancellationToken ct) => p.GuardarRecargosAsync(id, body, ct));
        usuario.MapGet("/liquidacion", (LiquidacionService l, string id, DateOnly? desde, DateOnly? hasta, CancellationToken ct) =>
            l.CalcularAsync(id, Periodo(l, desde, hasta), ct));

        // Bandeja de revisión: por defecto, todo lo pendiente. "revision=Todas" no filtra por estado.
        admin.MapGet("/sesiones", (AdministracionService a, LiquidacionService l, string? revision, string? usuarioId,
            DateOnly? desde, DateOnly? hasta, CancellationToken ct) =>
        {
            RevisionSesion? filtro = revision switch
            {
                null or "" => RevisionSesion.Pendiente,
                _ when revision.Equals("Todas", StringComparison.OrdinalIgnoreCase) => null,
                _ when Enum.TryParse<RevisionSesion>(revision, true, out var r) => r,
                _ => throw new DatosInvalidosException("Estado de revisión no válido."),
            };
            return a.SesionesAsync(filtro, Periodo(l, desde, hasta), usuarioId, ct);
        });
        admin.MapPost("/sesiones/{id:int}/aprobar", (RevisionService r, ClaimsPrincipal u, int id, CancellationToken ct) =>
            r.AprobarAsync(u.Id(), id, ct));
        admin.MapPost("/sesiones/{id:int}/rechazar", (RevisionService r, ClaimsPrincipal u, int id, RechazoInput body, CancellationToken ct) =>
            r.RechazarAsync(u.Id(), id, body.Comentario, ct));
    }

    private static IResult Pdf(ArchivoPdf a) => Results.File(a.Contenido, "application/pdf", a.Nombre);

    private static Periodo? Periodo(LiquidacionService l, DateOnly? desde, DateOnly? hasta)
    {
        if (desde is null && hasta is null) return null;
        var q = l.QuincenaActual();
        return new Periodo(desde ?? q.Desde, hasta ?? q.Hasta);
    }

    private static async ValueTask<object?> TraducirErrores(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        try
        {
            return await next(ctx);
        }
        catch (DatosInvalidosException e)
        {
            return Results.Problem(e.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ReglaNegocioException e)
        {
            return Results.Problem(e.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (NoEncontradoException e)
        {
            return Results.Problem(e.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (DbUpdateException)
        {
            return Results.Problem("El registro cambió al mismo tiempo desde otro dispositivo. Intenta de nuevo.",
                statusCode: StatusCodes.Status409Conflict);
        }
    }
}

public record NotaInput(string? Nota);

public record EnlaceDto(string Ruta, DateTime VenceEnUtc);

public record PeriodoInput(DateOnly? Desde, DateOnly? Hasta);

public record RolInput(bool EsAdmin);
