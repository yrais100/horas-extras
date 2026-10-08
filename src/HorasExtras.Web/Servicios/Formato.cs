using HorasExtras.Domain;
using HorasExtras.Domain.Calculo;
using HorasExtras.Domain.Entidades;

namespace HorasExtras.Web.Servicios;

public static class Formato
{
    public static string Pesos(decimal v) => v.ToString("C0");

    public static string Horas(decimal h)
    {
        var min = (int)Math.Round(h * 60);
        return $"{min / 60}h {min % 60:00}m";
    }

    public static string HoraLocal(DateTime? utc) => utc is null ? "" : ZonaHoraria.ALocal(utc.Value).ToString("h:mm tt");

    public static string FechaHoraLocal(DateTime? utc) => utc is null ? "" : ZonaHoraria.ALocal(utc.Value).ToString("ddd d MMM, h:mm tt");

    public static string Fecha(DateOnly f) => f.ToString("ddd d MMM");

    public static string Nombre(Categoria c) => c switch
    {
        Categoria.Diurna => "Extra diurna",
        Categoria.Nocturna => "Extra nocturna",
        Categoria.DominicalDiurna => "Extra dominical/festiva diurna",
        Categoria.DominicalNocturna => "Extra dominical/festiva nocturna",
        _ => c.ToString(),
    };

    public static string Nombre(EstadoSesion e) => e switch
    {
        EstadoSesion.EnCurso => "En curso",
        EstadoSesion.Pausada => "Pausada",
        _ => "Finalizada",
    };
}
