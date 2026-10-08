namespace HorasExtras.Domain;

/// <summary>Colombia usa UTC-5 todo el año (no tiene horario de verano).</summary>
public static class ZonaHoraria
{
    public static readonly TimeSpan Colombia = TimeSpan.FromHours(-5);

    public static DateTime ALocal(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Unspecified) + Colombia;

    public static DateTime AUtc(DateTime local) => DateTime.SpecifyKind(local - Colombia, DateTimeKind.Utc);

    public static DateOnly Hoy(TimeProvider reloj) => DateOnly.FromDateTime(ALocal(reloj.GetUtcNow().UtcDateTime));
}
