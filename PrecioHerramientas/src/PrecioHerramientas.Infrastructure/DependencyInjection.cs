using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PrecioHerramientas.Core.Abstractions;
using PrecioHerramientas.Core.Queries;
using PrecioHerramientas.Infrastructure.Persistence;
using PrecioHerramientas.Infrastructure.Queries;
using PrecioHerramientas.Infrastructure.Repositories;

namespace PrecioHerramientas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:Default'.");

        services.AddDbContext<AppDbContext>(options => ConfigureDbContext(options, connectionString));

        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOfferRepository, OfferRepository>();
        services.AddScoped<IScrapeRunRepository, ScrapeRunRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICatalogQueries, CatalogQueries>();
        return services;
    }

    internal static void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention();
}
