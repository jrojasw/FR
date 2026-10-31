using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PrecioHerramientas.Core.Entities;

namespace PrecioHerramientas.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    /// <summary>
    /// Claves de ASP.NET Core Data Protection (antiforgery, cookies). Guardarlas en la base
    /// evita que se pierdan en cada despliegue.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<PricePoint> PricePoints => Set<PricePoint>();
    public DbSet<ScrapeRun> ScrapeRuns => Set<ScrapeRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // pg_trgm permite que las búsquedas LIKE '%texto%' usen índice.
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Pesos chilenos: 12 dígitos enteros bastan y dejamos 2 decimales por si alguna tienda los usa.
        configurationBuilder.Properties<decimal>().HavePrecision(14, 2);
    }
}
