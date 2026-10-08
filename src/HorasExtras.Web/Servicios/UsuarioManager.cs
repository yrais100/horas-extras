using HorasExtras.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HorasExtras.Web.Servicios;

/// <summary>El primer usuario que se registra queda como administrador, tanto desde la web como desde la API.</summary>
public class UsuarioManager(
    IUserStore<Usuario> store, IOptions<IdentityOptions> options, IPasswordHasher<Usuario> hasher,
    IEnumerable<IUserValidator<Usuario>> userValidators, IEnumerable<IPasswordValidator<Usuario>> passwordValidators,
    ILookupNormalizer normalizer, IdentityErrorDescriber errors, IServiceProvider services, ILogger<UserManager<Usuario>> logger)
    : UserManager<Usuario>(store, options, hasher, userValidators, passwordValidators, normalizer, errors, services, logger)
{
    public override async Task<IdentityResult> CreateAsync(Usuario user)
    {
        var resultado = await base.CreateAsync(user);
        if (resultado.Succeeded && (await GetUsersInRoleAsync(AppDbContext.RolAdmin)).Count == 0)
            await AddToRoleAsync(user, AppDbContext.RolAdmin);
        return resultado;
    }
}
