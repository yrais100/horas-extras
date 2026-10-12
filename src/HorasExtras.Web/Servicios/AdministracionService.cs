using HorasExtras.Application;
using HorasExtras.Domain;
using HorasExtras.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HorasExtras.Web.Servicios;

public record CuentaDto(string Id, string? Email, string? Nombre, EstadoUsuario Estado, bool EsAdmin);

public record UsuarioAdminDto(string Id, string? Email, string? Nombre, EstadoUsuario Estado, bool EsAdmin,
    decimal HorasQuincena, decimal ValorQuincena, int DiasTrabajados, decimal HorasPorAprobar);

public record SesionUsuarioDto(string UsuarioId, string? Email, string? Nombre, SesionDto Sesion);

/// <summary>Aprobación y administración de usuarios. Solo para administradores.</summary>
public class AdministracionService(UserManager<Usuario> usuarios, LiquidacionService liquidacion, RevisionService revision)
{
    public async Task<CuentaDto> CuentaAsync(string usuarioId)
    {
        var u = await Exigir(usuarioId);
        return new(u.Id, u.Email, u.Nombre, u.Estado, await usuarios.IsInRoleAsync(u, AppDbContext.RolAdmin));
    }

    /// <summary>Usuarios con el resumen de la quincena actual; primero los que esperan aprobación.</summary>
    public async Task<IReadOnlyList<UsuarioAdminDto>> UsuariosAsync(CancellationToken ct = default)
    {
        var admins = (await usuarios.GetUsersInRoleAsync(AppDbContext.RolAdmin)).Select(u => u.Id).ToHashSet();
        var filas = new List<UsuarioAdminDto>();
        foreach (var u in await usuarios.Users.OrderBy(x => x.Email).ToListAsync(ct))
        {
            var r = await liquidacion.CalcularAsync(u.Id, null, ct);
            filas.Add(new(u.Id, u.Email, u.Nombre, u.Estado, admins.Contains(u.Id), r.HorasTotales, r.ValorTotal, r.DiasTrabajados, r.HorasPorAprobar));
        }
        return filas.OrderBy(f => f.Estado != EstadoUsuario.Pendiente).ToList();
    }

    /// <summary>Activa una cuenta pendiente o vuelve a activar una desactivada.</summary>
    public async Task AprobarAsync(string usuarioId)
    {
        var u = await Exigir(usuarioId);
        u.Estado = EstadoUsuario.Activo;
        Verificar(await usuarios.UpdateAsync(u));
    }

    /// <summary>Quita el acceso: la sesión abierta en la web o en la app se cierra al renovarse.</summary>
    public async Task DesactivarAsync(string adminId, string usuarioId)
    {
        if (adminId == usuarioId) throw new ReglaNegocioException("No puedes desactivar tu propia cuenta.");
        var u = await Exigir(usuarioId);
        u.Estado = EstadoUsuario.Inactivo;
        Verificar(await usuarios.UpdateAsync(u));
        Verificar(await usuarios.UpdateSecurityStampAsync(u));
    }

    public async Task CambiarAdminAsync(string adminId, string usuarioId, bool esAdmin)
    {
        if (adminId == usuarioId) throw new ReglaNegocioException("No puedes cambiar tu propio rol; pídeselo a otro administrador.");
        var u = await Exigir(usuarioId);
        if (esAdmin && u.Estado != EstadoUsuario.Activo) throw new ReglaNegocioException("Aprueba la cuenta antes de hacerla administradora.");
        var ya = await usuarios.IsInRoleAsync(u, AppDbContext.RolAdmin);
        if (ya == esAdmin) return;
        Verificar(esAdmin ? await usuarios.AddToRoleAsync(u, AppDbContext.RolAdmin) : await usuarios.RemoveFromRoleAsync(u, AppDbContext.RolAdmin));
        // El rol viaja en el token: renovarlo obliga a la app a pedir uno nuevo con el rol actualizado.
        Verificar(await usuarios.UpdateSecurityStampAsync(u));
    }

    /// <summary>Sesiones de todos con el nombre de su dueño, para la bandeja de revisión.</summary>
    public async Task<IReadOnlyList<SesionUsuarioDto>> SesionesAsync(Domain.Entidades.RevisionSesion? estado,
        Domain.Calculo.Periodo? periodo, string? usuarioId, CancellationToken ct = default)
    {
        var sesiones = await revision.ListarAsync(estado, periodo, usuarioId, ct);
        var ids = sesiones.Select(s => s.UsuarioId).Distinct().ToList();
        var nombres = await usuarios.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        return sesiones.Select(s => new SesionUsuarioDto(s.UsuarioId, nombres.GetValueOrDefault(s.UsuarioId)?.Email,
            nombres.GetValueOrDefault(s.UsuarioId)?.Nombre, s.Sesion)).ToList();
    }

    public async Task<Usuario> Exigir(string usuarioId) =>
        await usuarios.FindByIdAsync(usuarioId) ?? throw new NoEncontradoException("Usuario no encontrado.");

    private static void Verificar(IdentityResult r)
    {
        if (!r.Succeeded) throw new ReglaNegocioException(string.Join(" ", r.Errors.Select(e => e.Description)));
    }
}
