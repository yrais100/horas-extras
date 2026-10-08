using System.Collections.Concurrent;

namespace HorasExtras.Domain;

public record Festivo(DateOnly Fecha, string Nombre);

/// <summary>Festivos de Colombia (Ley 51 de 1983, "Ley Emiliani"), calculados para cualquier año.</summary>
public static class Festivos
{
    private static readonly ConcurrentDictionary<int, IReadOnlyList<Festivo>> Cache = new();

    public static bool EsFestivo(DateOnly fecha) => DelAnio(fecha.Year).Any(f => f.Fecha == fecha);

    public static bool EsDominicalOFestivo(DateOnly fecha) => fecha.DayOfWeek == DayOfWeek.Sunday || EsFestivo(fecha);

    public static IReadOnlyList<Festivo> DelAnio(int anio) => Cache.GetOrAdd(anio, Calcular);

    private static IReadOnlyList<Festivo> Calcular(int y)
    {
        var p = Pascua(y);
        return new List<Festivo>
        {
            new(new(y, 1, 1), "Año Nuevo"),
            new(ALunes(new(y, 1, 6)), "Reyes Magos"),
            new(ALunes(new(y, 3, 19)), "San José"),
            new(p.AddDays(-3), "Jueves Santo"),
            new(p.AddDays(-2), "Viernes Santo"),
            new(new(y, 5, 1), "Día del Trabajo"),
            new(ALunes(p.AddDays(39)), "Ascensión del Señor"),
            new(ALunes(p.AddDays(60)), "Corpus Christi"),
            new(ALunes(p.AddDays(68)), "Sagrado Corazón"),
            new(ALunes(new(y, 6, 29)), "San Pedro y San Pablo"),
            new(new(y, 7, 20), "Independencia"),
            new(new(y, 8, 7), "Batalla de Boyacá"),
            new(ALunes(new(y, 8, 15)), "Asunción de la Virgen"),
            new(ALunes(new(y, 10, 12)), "Día de la Raza"),
            new(ALunes(new(y, 11, 1)), "Todos los Santos"),
            new(ALunes(new(y, 11, 11)), "Independencia de Cartagena"),
            new(new(y, 12, 8), "Inmaculada Concepción"),
            new(new(y, 12, 25), "Navidad"),
        }.OrderBy(f => f.Fecha).ToList();
    }

    /// <summary>Traslada la fecha al lunes siguiente si no cae en lunes.</summary>
    private static DateOnly ALunes(DateOnly d)
    {
        var dias = ((int)DayOfWeek.Monday - (int)d.DayOfWeek + 7) % 7;
        return d.AddDays(dias);
    }

    /// <summary>Domingo de Pascua (algoritmo anónimo gregoriano).</summary>
    private static DateOnly Pascua(int y)
    {
        int a = y % 19, b = y / 100, c = y % 100, d = b / 4, e = b % 4, f = (b + 8) / 25;
        int g = (b - f + 1) / 3, h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7, m = (a + 11 * h + 22 * l) / 451;
        int mes = (h + l - 7 * m + 114) / 31, dia = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(y, mes, dia);
    }
}
