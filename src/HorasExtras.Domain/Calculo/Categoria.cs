namespace HorasExtras.Domain.Calculo;

public enum Categoria { Diurna, Nocturna, DominicalDiurna, DominicalNocturna }

/// <summary>Trozo homogéneo de tiempo: un solo día local y una sola categoría.</summary>
public record Trozo(DateOnly Fecha, Categoria Categoria, TimeSpan Duracion);

/// <summary>Parámetros que aplican a un día concreto.</summary>
public record ParametrosDia(TimeOnly InicioNocturno, TimeOnly FinNocturno, decimal ValorHora,
    IReadOnlyDictionary<Categoria, decimal> Porcentajes);
