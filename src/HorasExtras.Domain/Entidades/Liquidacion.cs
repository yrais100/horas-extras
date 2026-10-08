namespace HorasExtras.Domain.Entidades;

/// <summary>Liquidación guardada (cierre de quincena). El detalle queda congelado en JSON.</summary>
public class Liquidacion
{
    public int Id { get; set; }
    public string UsuarioId { get; set; } = "";
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }
    public DateTime CreadaEnUtc { get; set; }
    public decimal HorasTotales { get; set; }
    public decimal ValorTotal { get; set; }
    public string DetalleJson { get; set; } = "{}";
}
