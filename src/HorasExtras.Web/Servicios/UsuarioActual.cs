using System.Security.Claims;

namespace HorasExtras.Web.Servicios;

public static class UsuarioActual
{
    public static string Id(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
}
