using HorasExtras.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace HorasExtras.Application;

public interface IAppDbContext : IAsyncDisposable
{
    DbSet<Sesion> Sesiones { get; }
    DbSet<SalarioUsuario> Salarios { get; }
    DbSet<ConfiguracionRecargos> Recargos { get; }
    DbSet<Liquidacion> Liquidaciones { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>
/// Crea un contexto por operación. Blazor Server mantiene los servicios vivos durante todo el
/// circuito, así que no conviene compartir un DbContext entre operaciones.
/// </summary>
public interface IAppDbContextFactory
{
    IAppDbContext Crear();
}

/// <summary>Consulta de roles, para que la capa de aplicación no dependa de Identity.</summary>
public interface IRolesUsuario
{
    Task<bool> EsAdminAsync(string usuarioId, CancellationToken ct = default);
}
