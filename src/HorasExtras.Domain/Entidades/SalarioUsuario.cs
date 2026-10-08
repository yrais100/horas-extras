namespace HorasExtras.Domain.Entidades;

/// <summary>
/// Salario de un usuario a partir de una fecha. Se guarda con historial para que un aumento
/// no altere las horas trabajadas antes de que entrara en vigencia.
/// </summary>
public class SalarioUsuario
{
    public int Id { get; set; }
    public string UsuarioId { get; set; } = "";
    public DateOnly VigenteDesde { get; set; }
    public decimal SalarioMensual { get; set; }

    /// <summary>Si tiene valor, reemplaza el cálculo salario / horas mensuales.</summary>
    public decimal? ValorHoraManual { get; set; }
}
