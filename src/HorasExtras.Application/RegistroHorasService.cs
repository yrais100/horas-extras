using HorasExtras.Domain.Calculo;
using HorasExtras.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace HorasExtras.Application;

/// <summary>
/// Iniciar, pausar, continuar y finalizar sesiones de horas extra de un usuario. Lo que registra un
/// empleado queda pendiente hasta que el administrador lo apruebe; lo del administrador queda aprobado.
/// </summary>
public class RegistroHorasService(IAppDbContextFactory fabrica, TimeProvider reloj, IRolesUsuario roles)
{
    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;

    public async Task<SesionDto?> EstadoAsync(string usuarioId, CancellationToken ct = default)
    {
        await using var db = fabrica.Crear();
        var s = await Abierta(db, usuarioId, ct);
        return s is null ? null : SesionDto.De(s, Ahora);
    }

    public async Task<SesionDto> IniciarAsync(string usuarioId, string? nota = null, CancellationToken ct = default)
    {
        await using var db = fabrica.Crear();
        if (await Abierta(db, usuarioId, ct) is not null)
            throw new Domain.ReglaNegocioException("Ya tienes una sesión abierta. Pausa, continúa o finalízala.");
        var s = Sesion.Iniciar(usuarioId, Ahora, nota);
        s.MarcarCambioDelDueno(await roles.EsAdminAsync(usuarioId, ct), Ahora);
        db.Sesiones.Add(s);
        await db.SaveChangesAsync(ct);
        return SesionDto.De(s, Ahora);
    }

    public Task<SesionDto> PausarAsync(string usuarioId, CancellationToken ct = default) =>
        Cambiar(usuarioId, s => s.Pausar(Ahora), ct);

    public Task<SesionDto> ContinuarAsync(string usuarioId, CancellationToken ct = default) =>
        Cambiar(usuarioId, s => s.Continuar(Ahora), ct);

    public Task<SesionDto> FinalizarAsync(string usuarioId, CancellationToken ct = default) =>
        Cambiar(usuarioId, s => s.Finalizar(Ahora), ct);

    public async Task<SesionDto> RegistrarManualAsync(string usuarioId, RegistroManualInput input, CancellationToken ct = default)
    {
        await using var db = fabrica.Crear();
        var s = Sesion.Manual(usuarioId, DateTime.SpecifyKind(input.InicioUtc.ToUniversalTime(), DateTimeKind.Utc),
            DateTime.SpecifyKind(input.FinUtc.ToUniversalTime(), DateTimeKind.Utc), Ahora, input.Nota);
        s.MarcarCambioDelDueno(await roles.EsAdminAsync(usuarioId, ct), Ahora);
        db.Sesiones.Add(s);
        await db.SaveChangesAsync(ct);
        return SesionDto.De(s, Ahora);
    }

    /// <summary>Registro manual por cantidad de horas en una fecha; el recargo sale del día y la franja.</summary>
    public async Task<SesionDto> RegistrarPorHorasAsync(string usuarioId, RegistroPorHorasInput input, CancellationToken ct = default)
    {
        var (inicio, fin) = Sesion.IntervaloPorHoras(input.Fecha, input.HoraInicio, input.Horas);
        await using var db = fabrica.Crear();
        var s = Sesion.Manual(usuarioId, inicio, fin, Ahora, input.Nota);
        s.MarcarCambioDelDueno(await roles.EsAdminAsync(usuarioId, ct), Ahora);
        db.Sesiones.Add(s);
        await db.SaveChangesAsync(ct);
        return SesionDto.De(s, Ahora);
    }

    /// <summary>Ajuste manual de una sesión finalizada: nueva fecha, hora de inicio, horas y observaciones.</summary>
    public async Task<SesionDto> AjustarAsync(string usuarioId, int sesionId, RegistroPorHorasInput input, CancellationToken ct = default)
    {
        var (inicio, fin) = Sesion.IntervaloPorHoras(input.Fecha, input.HoraInicio, input.Horas);
        await using var db = fabrica.Crear();
        var s = await Buscar(db, usuarioId, sesionId, ct);
        var esAdmin = await roles.EsAdminAsync(usuarioId, ct);
        s.ExigirEditablePorDueno(esAdmin);
        s.Ajustar(inicio, fin, Ahora, input.Nota);
        s.MarcarCambioDelDueno(esAdmin, Ahora);
        await db.SaveChangesAsync(ct);
        return SesionDto.De(s, Ahora);
    }

    /// <summary>Cambia las observaciones de cualquier sesión, incluso la que está en curso.</summary>
    public async Task<SesionDto> CambiarNotaAsync(string usuarioId, int sesionId, string? nota, CancellationToken ct = default)
    {
        await using var db = fabrica.Crear();
        var s = await Buscar(db, usuarioId, sesionId, ct);
        var esAdmin = await roles.EsAdminAsync(usuarioId, ct);
        s.ExigirEditablePorDueno(esAdmin);
        s.CambiarNota(nota);
        if (s.Revision == RevisionSesion.Rechazada) s.MarcarCambioDelDueno(esAdmin, Ahora);
        await db.SaveChangesAsync(ct);
        return SesionDto.De(s, Ahora);
    }

    public async Task<IReadOnlyList<SesionDto>> ListarAsync(string usuarioId, Periodo? periodo = null, CancellationToken ct = default)
    {
        await using var db = fabrica.Crear();
        var (desde, hasta) = Consultas.RangoUtc(Consultas.Validar(periodo ?? Quincena.De(Domain.ZonaHoraria.Hoy(reloj))));
        var sesiones = await db.Sesiones.Include(s => s.Tramos)
            .Where(s => s.UsuarioId == usuarioId && s.Tramos.Any(t => t.InicioUtc < hasta && (t.FinUtc == null || t.FinUtc > desde)))
            .ToListAsync(ct);
        return sesiones.OrderByDescending(s => s.InicioUtc).Select(s => SesionDto.De(s, Ahora)).ToList();
    }

    public async Task EliminarAsync(string usuarioId, int sesionId, CancellationToken ct = default)
    {
        await using var db = fabrica.Crear();
        var s = await db.Sesiones.FirstOrDefaultAsync(x => x.Id == sesionId && x.UsuarioId == usuarioId, ct)
            ?? throw new NoEncontradoException("Sesión no encontrada.");
        s.ExigirEditablePorDueno(await roles.EsAdminAsync(usuarioId, ct));
        db.Sesiones.Remove(s);
        await db.SaveChangesAsync(ct);
    }

    private async Task<SesionDto> Cambiar(string usuarioId, Action<Sesion> accion, CancellationToken ct)
    {
        await using var db = fabrica.Crear();
        var s = await Abierta(db, usuarioId, ct) ?? throw new Domain.ReglaNegocioException("No tienes una sesión iniciada.");
        accion(s);
        await db.SaveChangesAsync(ct);
        return SesionDto.De(s, Ahora);
    }

    private static async Task<Sesion> Buscar(IAppDbContext db, string usuarioId, int sesionId, CancellationToken ct) =>
        await db.Sesiones.Include(s => s.Tramos).FirstOrDefaultAsync(x => x.Id == sesionId && x.UsuarioId == usuarioId, ct)
            ?? throw new NoEncontradoException("Sesión no encontrada.");

    private static Task<Sesion?> Abierta(IAppDbContext db, string usuarioId, CancellationToken ct) =>
        db.Sesiones.Include(s => s.Tramos)
            .Where(s => s.UsuarioId == usuarioId && s.Estado != EstadoSesion.Finalizada)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);
}
