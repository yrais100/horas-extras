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

    /// <summary>Descarga del reporte de una liquidación guardada (la web la pide con su cookie).</summary>
    public static string RutaPdf(int liquidacionId) => $"/api/liquidaciones/{liquidacionId}/pdf";

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

    public static string Nombre(RevisionSesion r) => r switch
    {
        RevisionSesion.Aprobada => "Aprobada",
        RevisionSesion.Rechazada => "Rechazada",
        _ => "Por aprobar",
    };

    /// <summary>Clase de Bootstrap para la etiqueta del estado de revisión.</summary>
    public static string Insignia(RevisionSesion r) => r switch
    {
        RevisionSesion.Aprobada => "text-bg-success",
        RevisionSesion.Rechazada => "text-bg-danger",
        _ => "text-bg-warning",
    };

    public static string Nombre(Infrastructure.EstadoUsuario e) => e switch
    {
        Infrastructure.EstadoUsuario.Activo => "Activo",
        Infrastructure.EstadoUsuario.Inactivo => "Desactivado",
        _ => "Por aprobar",
    };
}
