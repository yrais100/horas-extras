namespace HorasExtras.Domain.Calculo;

public record Periodo(DateOnly Desde, DateOnly Hasta)
{
    public bool Contiene(DateOnly fecha) => fecha >= Desde && fecha <= Hasta;
}

public static class Quincena
{
    /// <summary>Quincena que contiene la fecha: del 1 al 15 o del 16 al último día del mes.</summary>
    public static Periodo De(DateOnly fecha) => fecha.Day <= 15
        ? new(new(fecha.Year, fecha.Month, 1), new(fecha.Year, fecha.Month, 15))
        : new(new(fecha.Year, fecha.Month, 16), new(fecha.Year, fecha.Month, DateTime.DaysInMonth(fecha.Year, fecha.Month)));

    public static Periodo Anterior(Periodo p) => De(p.Desde.AddDays(-1));
}
