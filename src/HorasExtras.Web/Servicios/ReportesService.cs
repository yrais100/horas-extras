using HorasExtras.Application;
using HorasExtras.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace HorasExtras.Web.Servicios;

public record ArchivoPdf(byte[] Contenido, string Nombre);

/// <summary>
/// Genera el PDF de una liquidación guardada y los enlaces de descarga de corta duración que usa la app
/// móvil: el navegador del teléfono no lleva el token bearer, así que el enlace firmado lo reemplaza.
/// </summary>
public class ReportesService(LiquidacionService liquidaciones, UserManager<Usuario> usuarios, TimeProvider reloj,
    IDataProtectionProvider proteccion)
{
    public static readonly TimeSpan VigenciaEnlace = TimeSpan.FromMinutes(5);

    private ITimeLimitedDataProtector Protector => proteccion.CreateProtector("HorasExtras.EnlacePdfLiquidacion").ToTimeLimitedDataProtector();

    public async Task<ArchivoPdf> LiquidacionAsync(string usuarioId, int id, CancellationToken ct = default)
    {
        var reporte = await liquidaciones.ReporteAsync(usuarioId, id, ct);
        var u = await usuarios.FindByIdAsync(usuarioId);
        var empleado = u is null ? "" : string.IsNullOrWhiteSpace(u.Nombre) ? u.Email ?? "" : $"{u.Nombre} ({u.Email})";
        var pdf = ReporteLiquidacionPdf.Generar(reporte, empleado, reloj.GetUtcNow().UtcDateTime);
        return new(pdf, ReporteLiquidacionPdf.NombreArchivo(reporte.Guardada));
    }

    // El vencimiento usa el reloj del sistema (el protector valida con él), no el TimeProvider de la app.
    public string FirmarEnlace(string usuarioId, int id) => Protector.Protect($"{id}|{usuarioId}", VigenciaEnlace);

    /// <summary>Devuelve el usuario y la liquidación del enlace, o null si es inválido o ya venció.</summary>
    public (string usuarioId, int id)? LeerEnlace(string token)
    {
        try
        {
            var partes = Protector.Unprotect(token).Split('|', 2);
            return int.TryParse(partes[0], out var id) && partes.Length == 2 ? (partes[1], id) : null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}
