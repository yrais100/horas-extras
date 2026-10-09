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
        // Si falta, la aplicación arranca igual y /salud lo informa (ver InicializarBaseDeDatosAsync).
        var conexion = config.GetConnectionString("Default") ?? "";

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

    /// <summary>
    /// Aplica migraciones (Postgres) o crea el esquema (Sqlite). Devuelve el error en lugar de lanzarlo,
    /// para que la aplicación arranque y el problema se vea en /salud y en el log, no como un 503 mudo.
    /// </summary>
    public static async Task<string?> InicializarBaseDeDatosAsync(this IServiceProvider sp, IConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.GetConnectionString("Default")))
            return "Falta la cadena de conexión 'Default' (ConnectionStrings:Default).";
        try
        {
            await using var db = await sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            if (db.Database.IsSqlite()) await db.Database.EnsureCreatedAsync();
            else await db.Database.MigrateAsync();
            return null;
        }
        catch (Exception e)
        {
            return $"{e.GetType().Name}: {(e.InnerException ?? e).Message}";
        }
    }

    private class AppDbContextFactory(IDbContextFactory<AppDbContext> fabrica) : IAppDbContextFactory
    {
        public IAppDbContext Crear() => fabrica.CreateDbContext();
    }
}
