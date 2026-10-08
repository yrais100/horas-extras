namespace HorasExtras.Domain.Calculo;

public static class ClasificadorHoras
{
    /// <summary>
    /// Parte el intervalo [inicio, fin) en trozos homogéneos por día local y por franja
    /// (diurna/nocturna, ordinaria/dominical-festiva). Los turnos que cruzan la medianoche
    /// quedan repartidos entre los dos días.
    /// </summary>
    public static IEnumerable<Trozo> Clasificar(DateTime inicioUtc, DateTime finUtc, Func<DateOnly, ParametrosDia> parametros)
    {
        var t = ZonaHoraria.ALocal(inicioUtc);
        var fin = ZonaHoraria.ALocal(finUtc);
        while (t < fin)
        {
            var fecha = DateOnly.FromDateTime(t);
            var inicioDia = t.Date;
            var p = parametros(fecha);
            var nIni = p.InicioNocturno.ToTimeSpan();
            var nFin = p.FinNocturno.ToTimeSpan();
            var hora = t - inicioDia;
            var nocturna = nIni > nFin
                ? hora >= nIni || hora < nFin          // franja que cruza la medianoche (lo normal)
                : hora >= nIni && hora < nFin;
            var siguiente = new[] { nFin, nIni, TimeSpan.FromDays(1) }
                .Select(x => inicioDia + x)
                .Where(x => x > t)
                .Append(fin)
                .Min();
            var dominical = Festivos.EsDominicalOFestivo(fecha);
            var categoria = (dominical, nocturna) switch
            {
                (false, false) => Categoria.Diurna,
                (false, true) => Categoria.Nocturna,
                (true, false) => Categoria.DominicalDiurna,
                (true, true) => Categoria.DominicalNocturna,
            };
            yield return new Trozo(fecha, categoria, siguiente - t);
            t = siguiente;
        }
    }
}
