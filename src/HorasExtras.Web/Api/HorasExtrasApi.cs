using System.Security.Claims;
using HorasExtras.Application;
using HorasExtras.Domain;
using HorasExtras.Domain.Calculo;
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

/// <summary>API REST para la app móvil. Todas las rutas actúan sobre el usuario autenticado.</summary>
public static class HorasExtrasApi
{
    public static void MapHorasExtrasApi(this WebApplication app)
    {
        // /api/auth/register, /api/auth/login, /api/auth/refresh, /api/auth/manage/info, ...
        app.MapGroup("/api/auth").WithTags("Autenticación").MapIdentityApi<Usuario>();

        var api = app.MapGroup("/api").RequireAuthorization(PoliticasApi.Usuario).AddEndpointFilter(TraducirErrores);

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

        var perfil = api.MapGroup("/perfil").WithTags("Perfil");
        perfil.MapGet("/salarios", (PerfilService p, ClaimsPrincipal u, CancellationToken ct) => p.SalariosAsync(u.Id(), ct));
        perfil.MapPost("/salarios", (PerfilService p, ClaimsPrincipal u, SalarioInput body, CancellationToken ct) =>
            p.GuardarSalarioAsync(u.Id(), body, ct));
        perfil.MapDelete("/salarios/{id:int}", async (PerfilService p, ClaimsPrincipal u, int id, CancellationToken ct) =>
        {
            await p.EliminarSalarioAsync(u.Id(), id, ct);
            return Results.NoContent();
        });
        perfil.MapGet("/recargos", (PerfilService p, ClaimsPrincipal u, CancellationToken ct) => p.RecargosAsync(u.Id(), ct));
        perfil.MapPut("/recargos", (PerfilService p, ClaimsPrincipal u, RecargosDto body, CancellationToken ct) =>
            p.GuardarRecargosAsync(u.Id(), body, ct));

        var refe = api.MapGroup("/referencia").WithTags("Referencia");
        refe.MapGet("/ley", (DateOnly? fecha, TimeProvider reloj) => ValoresLey.Para(fecha ?? ZonaHoraria.Hoy(reloj)));
        refe.MapGet("/festivos", (int? anio, TimeProvider reloj) => Festivos.DelAnio(anio ?? ZonaHoraria.Hoy(reloj).Year));

        api.MapGet("/admin/usuarios", async (UserManager<Usuario> um, LiquidacionService l, CancellationToken ct) =>
        {
            var usuarios = await um.Users.OrderBy(x => x.Email).ToListAsync(ct);
            var filas = new List<ResumenUsuarioDto>();
            foreach (var x in usuarios)
            {
                var r = await l.CalcularAsync(x.Id, null, ct);
                filas.Add(new(x.Id, x.Email, x.Nombre, r.HorasTotales, r.ValorTotal, r.DiasTrabajados));
            }
            return filas;
        }).RequireAuthorization(PoliticasApi.Admin).WithTags("Administración");
    }

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

public record PeriodoInput(DateOnly? Desde, DateOnly? Hasta);

public record ResumenUsuarioDto(string Id, string? Email, string? Nombre, decimal HorasQuincena, decimal ValorQuincena, int DiasTrabajados);
