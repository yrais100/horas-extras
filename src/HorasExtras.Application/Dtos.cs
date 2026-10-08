using HorasExtras.Domain.Entidades;

namespace HorasExtras.Application;

public record TramoDto(DateTime InicioUtc, DateTime? FinUtc);

public record SesionDto(int Id, EstadoSesion Estado, string? Nota, DateTime? InicioUtc, DateTime? FinUtc,
    long SegundosAcumulados, IReadOnlyList<TramoDto> Tramos)
{
    public static SesionDto De(Sesion s, DateTime ahoraUtc) => new(s.Id, s.Estado, s.Nota, s.InicioUtc, s.FinUtc,
        (long)s.Duracion(ahoraUtc).TotalSeconds,
        s.Tramos.OrderBy(t => t.InicioUtc).Select(t => new TramoDto(t.InicioUtc, t.FinUtc)).ToList());
}

public record RegistroManualInput(DateTime InicioUtc, DateTime FinUtc, string? Nota);

public record SalarioInput(DateOnly VigenteDesde, decimal SalarioMensual, decimal? ValorHoraManual);

public record SalarioDto(int Id, DateOnly VigenteDesde, decimal SalarioMensual, decimal? ValorHoraManual);

public record RecargosDto(ModoRecargos Modo, TimeOnly InicioNocturno, TimeOnly FinNocturno, decimal HorasMensuales,
    decimal PctExtraDiurna, decimal PctExtraNocturna, decimal PctExtraDominicalDiurna, decimal PctExtraDominicalNocturna)
{
    public static RecargosDto De(ConfiguracionRecargos c) => new(c.Modo, c.InicioNocturno, c.FinNocturno, c.HorasMensuales,
        c.PctExtraDiurna, c.PctExtraNocturna, c.PctExtraDominicalDiurna, c.PctExtraDominicalNocturna);
}

public record LiquidacionGuardadaDto(int Id, DateOnly Desde, DateOnly Hasta, DateTime CreadaEnUtc, decimal HorasTotales, decimal ValorTotal);
