using HorasExtras.Domain.Calculo;
using HorasExtras.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace HorasExtras.Application;

/// <summary>Revisión de las horas de todos los usuarios. Solo para administradores.</summary>
public class RevisionService(IAppDbContextFactory fabrica, TimeProvider reloj)
{
    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Sesiones finalizadas en el periodo (por defecto, todas las pendientes sin importar la fecha),
    /// opcionalmente de un solo usuario o en un solo estado de revisión.
    /// </summary>
    public async Task<IReadOnlyList<SesionRevisionDto>> ListarAsync(RevisionSesion? revision = RevisionSesion.Pendiente,
        Periodo? periodo = null, string? usuarioId = null, CancellationToken ct = default)
    {
        await using var db = fabrica.Crear();
        var q = db.Sesiones.Include(s => s.Tramos).Where(s => s.Estado == EstadoSesion.Finalizada);
        if (revision is { } r) q = q.Where(s => s.Revision == r);
        if (usuarioId is not null) q = q.Where(s => s.UsuarioId == usuarioId);
        if (periodo is not null)
        {
            var (desde, hasta) = Consultas.RangoUtc(Consultas.Validar(periodo));
            q = q.Where(s => s.Tramos.Any(t => t.InicioUtc < hasta && (t.FinUtc == null || t.FinUtc > desde)));
        }
        var sesiones = await q.ToListAsync(ct);
        return sesiones.OrderBy(s => s.InicioUtc).Select(s => new SesionRevisionDto(s.UsuarioId, SesionDto.De(s, Ahora))).ToList();
    }

    public async Task<int> ContarPendientesAsync(CancellationToken ct = default)
    {
        await using var db = fabrica.Crear();
        return await db.Sesiones.CountAsync(s => s.Estado == EstadoSesion.Finalizada && s.Revision == RevisionSesion.Pendiente, ct);
    }

    public Task<SesionDto> AprobarAsync(string revisorId, int sesionId, CancellationToken ct = default) =>
        Revisar(sesionId, s => s.Aprobar(revisorId, Ahora), ct);

    public Task<SesionDto> RechazarAsync(string revisorId, int sesionId, string? comentario, CancellationToken ct = default) =>
        Revisar(sesionId, s => s.Rechazar(revisorId, Ahora, comentario), ct);

    private async Task<SesionDto> Revisar(int sesionId, Action<Sesion> accion, CancellationToken ct)
    {
        await using var db = fabrica.Crear();
        var s = await db.Sesiones.Include(x => x.Tramos).FirstOrDefaultAsync(x => x.Id == sesionId, ct)
            ?? throw new NoEncontradoException("Sesión no encontrada.");
        accion(s);
        await db.SaveChangesAsync(ct);
        return SesionDto.De(s, Ahora);
    }
}
