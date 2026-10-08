using Microsoft.AspNetCore.Identity;

namespace HorasExtras.Infrastructure;

public class Usuario : IdentityUser
{
    public string? Nombre { get; set; }
}
