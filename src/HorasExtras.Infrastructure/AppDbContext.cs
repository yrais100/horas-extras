using HorasExtras.Application;
using HorasExtras.Domain.Entidades;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HorasExtras.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<Usuario>(options), IAppDbContext
{
    public const string RolAdmin = "Admin";

    public DbSet<Sesion> Sesiones => Set<Sesion>();
    public DbSet<SalarioUsuario> Salarios => Set<SalarioUsuario>();
    public DbSet<ConfiguracionRecargos> Recargos => Set<ConfiguracionRecargos>();
    public DbSet<Liquidacion> Liquidaciones => Set<Liquidacion>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // SQLite no guarda el "Kind" de las fechas; todas las fechas del modelo son UTC.
        if (Database.IsSqlite())
        {
            b.Properties<DateTime>().HaveConversion<UtcConverter>();
            b.Properties<DateTime?>().HaveConversion<UtcConverter>();
        }
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Sesion>(e =>
        {
            e.ToTable("Sesiones");
            e.Property(x => x.UsuarioId).HasMaxLength(450);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Nota).HasMaxLength(500);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Tramos).WithOne().HasForeignKey(t => t.SesionId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(x => x.InicioUtc);
            e.Ignore(x => x.FinUtc);
            // Garantiza en la base de datos una sola sesión abierta por usuario.
            e.HasIndex(x => x.UsuarioId).IsUnique().HasFilter("\"Estado\" <> 'Finalizada'").HasDatabaseName("IX_Sesiones_UnaAbiertaPorUsuario");
        });

        b.Entity<Tramo>(e =>
        {
            e.ToTable("Tramos");
            e.HasIndex(x => x.InicioUtc);
        });

        b.Entity<SalarioUsuario>(e =>
        {
            e.ToTable("Salarios");
            e.Property(x => x.SalarioMensual).HasPrecision(18, 2);
            e.Property(x => x.ValorHoraManual).HasPrecision(18, 2);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UsuarioId, x.VigenteDesde }).IsUnique();
        });

        b.Entity<ConfiguracionRecargos>(e =>
        {
            e.ToTable("ConfiguracionRecargos");
            e.HasKey(x => x.UsuarioId);
            e.Property(x => x.Modo).HasConversion<string>().HasMaxLength(20);
            foreach (var p in new[] { nameof(ConfiguracionRecargos.HorasMensuales), nameof(ConfiguracionRecargos.PctExtraDiurna),
                         nameof(ConfiguracionRecargos.PctExtraNocturna), nameof(ConfiguracionRecargos.PctExtraDominicalDiurna),
                         nameof(ConfiguracionRecargos.PctExtraDominicalNocturna) })
                e.Property<decimal>(p).HasPrecision(9, 2);
            e.HasOne<Usuario>().WithOne().HasForeignKey<ConfiguracionRecargos>(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Liquidacion>(e =>
        {
            e.ToTable("Liquidaciones");
            e.Property(x => x.HorasTotales).HasPrecision(9, 2);
            e.Property(x => x.ValorTotal).HasPrecision(18, 2);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UsuarioId, x.Desde });
        });
    }

    private class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
