using Microsoft.AspNetCore.Identity;

namespace HorasExtras.Infrastructure;

/// <summary>
/// Pendiente: se registró y espera que un administrador lo apruebe. Inactivo: el administrador le quitó
/// el acceso. Solo un usuario activo puede ingresar.
/// </summary>
public enum EstadoUsuario { Pendiente, Activo, Inactivo }

public class Usuario : IdentityUser
{
    public string? Nombre { get; set; }
    public EstadoUsuario Estado { get; set; } = EstadoUsuario.Pendiente;
}
