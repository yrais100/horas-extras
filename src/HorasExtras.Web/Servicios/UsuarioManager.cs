using HorasExtras.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HorasExtras.Web.Servicios;

/// <summary>
/// El primer usuario que se registra queda como administrador activo, tanto desde la web como desde la API.
/// Los demás quedan pendientes hasta que un administrador los apruebe.
/// </summary>
public class UsuarioManager(
    IUserStore<Usuario> store, IOptions<IdentityOptions> options, IPasswordHasher<Usuario> hasher,
    IEnumerable<IUserValidator<Usuario>> userValidators, IEnumerable<IPasswordValidator<Usuario>> passwordValidators,
    ILookupNormalizer normalizer, IdentityErrorDescriber errors, IServiceProvider services, ILogger<UserManager<Usuario>> logger)
    : UserManager<Usuario>(store, options, hasher, userValidators, passwordValidators, normalizer, errors, services, logger)
{
    public override async Task<IdentityResult> CreateAsync(Usuario user)
    {
        var primero = (await GetUsersInRoleAsync(AppDbContext.RolAdmin)).Count == 0;
        user.Estado = primero ? EstadoUsuario.Activo : EstadoUsuario.Pendiente;
        var resultado = await base.CreateAsync(user);
        if (resultado.Succeeded && primero)
            await AddToRoleAsync(user, AppDbContext.RolAdmin);
        return resultado;
    }
}

/// <summary>
/// Con <see cref="SignInOptions.RequireConfirmedAccount"/> activo, Identity solo deja ingresar a quien
/// "confirma" esta regla: aquí, una cuenta activa (aprobada por un administrador).
/// </summary>
public class CuentaActiva : IUserConfirmation<Usuario>
{
    public Task<bool> IsConfirmedAsync(UserManager<Usuario> manager, Usuario user) =>
        Task.FromResult(user.Estado == EstadoUsuario.Activo);
}
