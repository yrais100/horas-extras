using HorasExtras.Domain;
using HorasExtras.Domain.Calculo;

namespace HorasExtras.Application;

internal static class Consultas
{
    /// <summary>Instantes UTC que cubren el periodo en hora local de Colombia.</summary>
    public static (DateTime desde, DateTime hasta) RangoUtc(Periodo p) =>
        (ZonaHoraria.AUtc(p.Desde.ToDateTime(TimeOnly.MinValue)), ZonaHoraria.AUtc(p.Hasta.AddDays(1).ToDateTime(TimeOnly.MinValue)));

    public static Periodo Validar(Periodo p)
    {
        if (p.Desde > p.Hasta) throw new DatosInvalidosException("La fecha inicial debe ser anterior a la final.");
        if (p.Hasta.DayNumber - p.Desde.DayNumber > 92) throw new DatosInvalidosException("El periodo no puede superar 3 meses.");
        return p;
    }
}
