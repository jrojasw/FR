using Microsoft.Extensions.DependencyInjection;
using PrecioHerramientas.Core.Services;

namespace PrecioHerramientas.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<PriceIngestionService>();
        return services;
    }
}
