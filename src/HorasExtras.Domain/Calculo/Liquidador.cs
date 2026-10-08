using HorasExtras.Domain.Entidades;

namespace HorasExtras.Domain.Calculo;

public record TotalCategoria(Categoria Categoria, decimal Horas, decimal Valor);

public record DetalleDia(DateOnly Fecha, bool DominicalOFestivo, decimal HorasDiurnas, decimal HorasNocturnas,
    decimal Horas, decimal Valor, bool ExcedeLimiteDiario);

public record ResultadoLiquidacion(
    DateOnly Desde,
    DateOnly Hasta,
    DateTime CalculadoEnUtc,
    IReadOnlyList<TotalCategoria> Totales,
    decimal HorasTotales,
    decimal ValorTotal,
    int DiasTrabajados,
    IReadOnlyList<DetalleDia> Dias,
    decimal ValorHoraActual,
    IReadOnlyList<string> Avisos);

public static class Liquidador
{
    /// <summary>Máximo legal de horas extra por día (art. 22 de la Ley 50 de 1990).</summary>
    public const decimal LimiteDiario = 2;

    /// <summary>
    /// Liquida los tramos dentro del periodo. Un tramo abierto se cuenta hasta <paramref name="ahoraUtc"/>,
    /// así se puede liquidar en cualquier momento sin finalizar la sesión.
    /// </summary>
    public static ResultadoLiquidacion Liquidar(IEnumerable<Tramo> tramos, ResolutorParametros resolutor, Periodo periodo, DateTime ahoraUtc)
    {
        var porCategoria = Enum.GetValues<Categoria>().ToDictionary(c => c, _ => (horas: 0m, valor: 0m));
        var porDia = new SortedDictionary<DateOnly, (decimal diurnas, decimal nocturnas, decimal valor)>();

        foreach (var tramo in tramos)
        {
            foreach (var trozo in ClasificadorHoras.Clasificar(tramo.InicioUtc, tramo.FinUtc ?? ahoraUtc, resolutor.Para))
            {
                if (!periodo.Contiene(trozo.Fecha)) continue;
                var p = resolutor.Para(trozo.Fecha);
                var horas = (decimal)trozo.Duracion.TotalHours;
                var valor = horas * p.ValorHora * (1 + p.Porcentajes[trozo.Categoria] / 100m);

                var c = porCategoria[trozo.Categoria];
                porCategoria[trozo.Categoria] = (c.horas + horas, c.valor + valor);

                var d = porDia.GetValueOrDefault(trozo.Fecha);
                var nocturna = trozo.Categoria is Categoria.Nocturna or Categoria.DominicalNocturna;
                porDia[trozo.Fecha] = (d.diurnas + (nocturna ? 0 : horas), d.nocturnas + (nocturna ? horas : 0), d.valor + valor);
            }
        }

        var dias = porDia.Select(kv =>
        {
            var horas = kv.Value.diurnas + kv.Value.nocturnas;
            return new DetalleDia(kv.Key, Festivos.EsDominicalOFestivo(kv.Key), Math.Round(kv.Value.diurnas, 2),
                Math.Round(kv.Value.nocturnas, 2), Math.Round(horas, 2), Math.Round(kv.Value.valor), horas > LimiteDiario);
        }).ToList();

        var totales = porCategoria.Select(kv => new TotalCategoria(kv.Key, Math.Round(kv.Value.horas, 2), Math.Round(kv.Value.valor))).ToList();
        var valorHora = resolutor.Para(periodo.Hasta).ValorHora;

        var avisos = new List<string>();
        if (valorHora == 0) avisos.Add("Registra tu salario mensual o un valor hora manual para calcular los valores.");
        avisos.AddRange(dias.Where(d => d.ExcedeLimiteDiario)
            .Select(d => $"El {d.Fecha:dd/MM/yyyy} se registraron {d.Horas:0.##} h extra; la ley fija un máximo de {LimiteDiario} h diarias."));

        return new ResultadoLiquidacion(periodo.Desde, periodo.Hasta, ahoraUtc, totales,
            totales.Sum(t => t.Horas), totales.Sum(t => t.Valor), dias.Count, dias, Math.Round(valorHora), avisos);
    }
}
