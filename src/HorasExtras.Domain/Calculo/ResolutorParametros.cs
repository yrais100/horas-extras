using HorasExtras.Domain.Entidades;

namespace HorasExtras.Domain.Calculo;

/// <summary>Resuelve franjas, porcentajes y valor hora de un usuario para cada fecha.</summary>
public class ResolutorParametros(ConfiguracionRecargos config, IEnumerable<SalarioUsuario> salarios)
{
    private readonly List<SalarioUsuario> _salarios = salarios.OrderByDescending(s => s.VigenteDesde).ToList();

    public ConfiguracionRecargos Config => config;

    public SalarioUsuario? SalarioVigente(DateOnly fecha) => _salarios.FirstOrDefault(s => s.VigenteDesde <= fecha);

    public ParametrosDia Para(DateOnly fecha)
    {
        TimeOnly ini, fin;
        decimal horasMes, d, n, dd, dn;
        if (config.Modo == ModoRecargos.Ley)
        {
            var ley = ValoresLey.Para(fecha);
            (ini, fin, horasMes) = (ley.InicioNocturno, ley.FinNocturno, ley.HorasMensuales);
            (d, n, dd, dn) = (ley.PctExtraDiurna, ley.PctExtraNocturna, ley.PctExtraDominicalDiurna, ley.PctExtraDominicalNocturna);
        }
        else
        {
            (ini, fin, horasMes) = (config.InicioNocturno, config.FinNocturno, config.HorasMensuales);
            (d, n, dd, dn) = (config.PctExtraDiurna, config.PctExtraNocturna, config.PctExtraDominicalDiurna, config.PctExtraDominicalNocturna);
        }

        var salario = SalarioVigente(fecha);
        var valorHora = salario?.ValorHoraManual is > 0
            ? salario.ValorHoraManual.Value
            : salario is not null && horasMes > 0 ? salario.SalarioMensual / horasMes : 0m;

        return new ParametrosDia(ini, fin, valorHora, new Dictionary<Categoria, decimal>
        {
            [Categoria.Diurna] = d,
            [Categoria.Nocturna] = n,
            [Categoria.DominicalDiurna] = dd,
            [Categoria.DominicalNocturna] = dn,
        });
    }
}
