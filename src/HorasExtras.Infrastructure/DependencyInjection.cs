using HorasExtras.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HorasExtras.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra la base de datos. "Database:Provider" = "Postgres" (producción, con migraciones)
    /// o "Sqlite" (desarrollo y pruebas, sin servidor).
    /// </summary>
    public static IServiceCollection AddInfraestructura(this IServiceCollection services, IConfiguration config)
    {
        var proveedor = config["Database:Provider"] ?? "Postgres";
        var conexion = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:Default'.");

        services.AddDbContextFactory<AppDbContext>(o =>
        {
            if (proveedor.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)) o.UseSqlite(conexion);
            else o.UseNpgsql(conexion, npgsql => npgsql
                .MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Esquema)
                .EnableRetryOnFailure(3)); // reintenta cortes breves de red con la base de datos administrada
        });
        services.AddSingleton<IAppDbContextFactory, AppDbContextFactory>();
        return services;
    }

    /// <summary>Aplica migraciones (Postgres) o crea el esquema (Sqlite).</summary>
    public static async Task InicializarBaseDeDatosAsync(this IServiceProvider sp)
    {
        await using var db = await sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        if (db.Database.IsSqlite()) await db.Database.EnsureCreatedAsync();
        else await db.Database.MigrateAsync();
    }

    private class AppDbContextFactory(IDbContextFactory<AppDbContext> fabrica) : IAppDbContextFactory
    {
        public IAppDbContext Crear() => fabrica.CreateDbContext();
    }
}
