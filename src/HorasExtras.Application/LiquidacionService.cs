using System.Text.Json;
using HorasExtras.Domain;
using HorasExtras.Domain.Calculo;
using HorasExtras.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace HorasExtras.Application;

public class LiquidacionService(IAppDbContextFactory fabrica, TimeProvider reloj)
{
    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;

    public Periodo QuincenaActual() => Quincena.De(ZonaHoraria.Hoy(reloj));

    /// <summary>Liquidación en vivo: cuenta también la sesión abierta hasta este momento.</summary>
    public async Task<ResultadoLiquidacion> CalcularAsync(string usuarioId, Periodo? periodo = null, CancellationToken ct = default)
    {
        await using var db = fabrica.Crear();
        return await Calcular(db, usuarioId, Consultas.Validar(periodo ?? QuincenaActual()), ct);
    }

    /// <summary>Guarda la liquidación del periodo como histórico (cierre de quincena).</summary>
    public async Task<LiquidacionGuardadaDto> GuardarAsync(string usuarioId, Periodo? periodo = null, CancellationToken ct = default)
    {
        await using var db = fabrica.Crear();
        var r = await Calcular(db, usuarioId, Consultas.Validar(periodo ?? QuincenaActual()), ct);
        var l = new Liquidacion
        {
            UsuarioId = usuarioId, Desde = r.Desde, Hasta = r.Hasta, CreadaEnUtc = Ahora,
            HorasTotales = r.HorasTotales, ValorTotal = r.ValorTotal, DetalleJson = JsonSerializer.Serialize(r),
        };
        db.Liquidaciones.Add(l);
        await db.SaveChangesAsync(ct);
        return new(l.Id, l.Desde, l.Hasta, l.CreadaEnUtc, l.HorasTotales, l.ValorTotal);
    }

    public async Task<IReadOnlyList<LiquidacionGuardadaDto>> HistorialAsync(string usuarioId, CancellationToken ct = default)
    {
        await using var db = fabrica.Crear();
        return await db.Liquidaciones.Where(l => l.UsuarioId == usuarioId)
            .OrderByDescending(l => l.Id)
            .Select(l => new LiquidacionGuardadaDto(l.Id, l.Desde, l.Hasta, l.CreadaEnUtc, l.HorasTotales, l.ValorTotal))
            .ToListAsync(ct);
    }

    public async Task<ResultadoLiquidacion> DetalleGuardadoAsync(string usuarioId, int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Crear();
        var l = await db.Liquidaciones.FirstOrDefaultAsync(x => x.Id == id && x.UsuarioId == usuarioId, ct)
            ?? throw new NoEncontradoException("Liquidación no encontrada.");
        return JsonSerializer.Deserialize<ResultadoLiquidacion>(l.DetalleJson)!;
    }

    /// <summary>Liquidación guardada con su detalle congelado, para el reporte en PDF.</summary>
    public async Task<LiquidacionReporte> ReporteAsync(string usuarioId, int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Crear();
        var l = await db.Liquidaciones.FirstOrDefaultAsync(x => x.Id == id && x.UsuarioId == usuarioId, ct)
            ?? throw new NoEncontradoException("Liquidación no encontrada.");
        return new(new(l.Id, l.Desde, l.Hasta, l.CreadaEnUtc, l.HorasTotales, l.ValorTotal),
            JsonSerializer.Deserialize<ResultadoLiquidacion>(l.DetalleJson)!);
    }

    private async Task<ResultadoLiquidacion> Calcular(IAppDbContext db, string usuarioId, Periodo periodo, CancellationToken ct)
    {
        // Un día de margen a cada lado: el filtro exacto por fecha local lo hace el liquidador.
        var (desde, hasta) = Consultas.RangoUtc(periodo);
        desde = desde.AddDays(-1);
        hasta = hasta.AddDays(1);
        var sesiones = await db.Sesiones.Include(s => s.Tramos)
            .Where(s => s.UsuarioId == usuarioId && s.Revision != RevisionSesion.Rechazada)
            .Where(s => s.Tramos.Any(t => t.InicioUtc < hasta && (t.FinUtc == null || t.FinUtc > desde)))
            .ToListAsync(ct);
        var config = await db.Recargos.FindAsync([usuarioId], ct) ?? ConfiguracionRecargos.PorDefecto(usuarioId);
        var salarios = await db.Salarios.Where(s => s.UsuarioId == usuarioId).ToListAsync(ct);
        var resolutor = new ResolutorParametros(config, salarios);

        // Solo cuenta lo aprobado; lo pendiente se informa aparte para que nadie se lleve una sorpresa.
        var r = Liquidador.Liquidar(sesiones.Where(s => s.Revision == RevisionSesion.Aprobada).SelectMany(s => s.Tramos), resolutor, periodo, Ahora);
        var pendientes = Liquidador.Liquidar(sesiones.Where(s => s.Revision == RevisionSesion.Pendiente).SelectMany(s => s.Tramos), resolutor, periodo, Ahora);
        r = r with { Registros = Registros(sesiones, resolutor, periodo) };
        if (pendientes.HorasTotales == 0) return r;
        var avisos = r.Avisos.Append(
            $"Hay {pendientes.HorasTotales:0.##} h por aprobar ({pendientes.ValorTotal:C0}) que no suman hasta que el administrador las revise.").ToList();
        return r with { Avisos = avisos, HorasPorAprobar = pendientes.HorasTotales, ValorPorAprobar = pendientes.ValorTotal };
    }

    /// <summary>Lo que aporta cada sesión al periodo, para el detalle del reporte.</summary>
    private List<DetalleRegistro> Registros(IEnumerable<Sesion> sesiones, ResolutorParametros resolutor, Periodo periodo) =>
        sesiones.OrderBy(s => s.InicioUtc)
            .Select(s => (s, l: Liquidador.Liquidar(s.Tramos, resolutor, periodo, Ahora)))
            .Where(x => x.l.HorasTotales > 0)
            .Select(x => new DetalleRegistro(x.s.Id, x.s.InicioUtc, x.s.FinUtc, x.s.Estado != EstadoSesion.Finalizada, x.s.Nota,
                x.s.Revision, x.l.Totales.Where(t => t.Horas > 0).ToList(), x.l.HorasTotales, x.l.ValorTotal))
            .ToList();
}
