namespace HorasExtras.Domain.Entidades;

public enum ModoRecargos { Ley, Manual }

/// <summary>
/// Configuración de recargos de un usuario. En modo <see cref="ModoRecargos.Ley"/> se ignoran los
/// demás campos y se aplican los valores legales vigentes en cada fecha.
/// </summary>
public class ConfiguracionRecargos
{
    public string UsuarioId { get; set; } = "";
    public ModoRecargos Modo { get; set; } = ModoRecargos.Ley;
    public TimeOnly InicioNocturno { get; set; } = new(19, 0);
    public TimeOnly FinNocturno { get; set; } = new(6, 0);
    public decimal HorasMensuales { get; set; } = 210;
    public decimal PctExtraDiurna { get; set; } = 25;
    public decimal PctExtraNocturna { get; set; } = 75;
    public decimal PctExtraDominicalDiurna { get; set; } = 115;
    public decimal PctExtraDominicalNocturna { get; set; } = 165;

    public static ConfiguracionRecargos PorDefecto(string usuarioId) => new() { UsuarioId = usuarioId };
}
