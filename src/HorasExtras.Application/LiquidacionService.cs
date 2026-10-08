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

    private async Task<ResultadoLiquidacion> Calcular(IAppDbContext db, string usuarioId, Periodo periodo, CancellationToken ct)
    {
        // Un día de margen a cada lado: el filtro exacto por fecha local lo hace el liquidador.
        var (desde, hasta) = Consultas.RangoUtc(periodo);
        desde = desde.AddDays(-1);
        hasta = hasta.AddDays(1);
        var tramos = await db.Sesiones.Where(s => s.UsuarioId == usuarioId)
            .SelectMany(s => s.Tramos)
            .Where(t => t.InicioUtc < hasta && (t.FinUtc == null || t.FinUtc > desde))
            .ToListAsync(ct);
        var config = await db.Recargos.FindAsync([usuarioId], ct) ?? ConfiguracionRecargos.PorDefecto(usuarioId);
        var salarios = await db.Salarios.Where(s => s.UsuarioId == usuarioId).ToListAsync(ct);
        return Liquidador.Liquidar(tramos, new ResolutorParametros(config, salarios), periodo, Ahora);
    }
}
