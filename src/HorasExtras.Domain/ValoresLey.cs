namespace HorasExtras.Domain;

/// <summary>Parámetros legales vigentes en una fecha.</summary>
public record ParametrosLegales(
    TimeOnly InicioNocturno,
    TimeOnly FinNocturno,
    int JornadaSemanal,
    decimal HorasMensuales,
    decimal PctDominical,
    decimal PctExtraDiurna,
    decimal PctExtraNocturna,
    decimal PctExtraDominicalDiurna,
    decimal PctExtraDominicalNocturna);

/// <summary>
/// Valores de ley en Colombia según la fecha. REVISAR ante cualquier cambio normativo.
/// - Ley 2466 de 2025: jornada nocturna desde las 19:00 (vigente desde el 25-dic-2025) y
///   recargo dominical/festivo gradual: 80 % (jul-2025), 90 % (jul-2026), 100 % (jul-2027).
/// - Ley 2101 de 2021: jornada semanal máxima 47, 46, 44 y 42 horas (cada 15 de julio desde 2023).
/// - CST arts. 168 y 179: hora extra diurna +25 %, nocturna +75 %.
/// </summary>
public static class ValoresLey
{
    public static ParametrosLegales Para(DateOnly fecha)
    {
        var jornada = fecha < new DateOnly(2023, 7, 15) ? 48
            : fecha < new DateOnly(2024, 7, 15) ? 47
            : fecha < new DateOnly(2025, 7, 15) ? 46
            : fecha < new DateOnly(2026, 7, 15) ? 44
            : 42;
        decimal dominical = fecha < new DateOnly(2025, 7, 1) ? 75
            : fecha < new DateOnly(2026, 7, 1) ? 80
            : fecha < new DateOnly(2027, 7, 1) ? 90
            : 100;
        var inicioNocturno = fecha < new DateOnly(2025, 12, 25) ? new TimeOnly(21, 0) : new TimeOnly(19, 0);

        return new ParametrosLegales(
            InicioNocturno: inicioNocturno,
            FinNocturno: new TimeOnly(6, 0),
            JornadaSemanal: jornada,
            // Convención usual: (horas semanales / 6 días) x 30 días.
            HorasMensuales: jornada * 5,
            PctDominical: dominical,
            PctExtraDiurna: 25,
            PctExtraNocturna: 75,
            PctExtraDominicalDiurna: dominical + 25,
            PctExtraDominicalNocturna: dominical + 75);
    }
}
