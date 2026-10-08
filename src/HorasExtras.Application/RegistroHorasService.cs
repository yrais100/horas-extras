using HorasExtras.Domain.Calculo;
using HorasExtras.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace HorasExtras.Application;

/// <summary>Iniciar, pausar, continuar y finalizar sesiones de horas extra de un usuario.</summary>
public class RegistroHorasService(IAppDbContextFactory fabrica, TimeProvider reloj)
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
        db.Sesiones.Add(s);
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

    private static Task<Sesion?> Abierta(IAppDbContext db, string usuarioId, CancellationToken ct) =>
        db.Sesiones.Include(s => s.Tramos)
            .Where(s => s.UsuarioId == usuarioId && s.Estado != EstadoSesion.Finalizada)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);
}
