using Microsoft.Extensions.DependencyInjection;
using PrecioHerramientas.Core.Affiliates;
using PrecioHerramientas.Core.Services;

namespace PrecioHerramientas.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<PriceIngestionService>();

        services.AddSingleton<IAffiliateLinkBuilder, DirectLinkBuilder>();
        services.AddSingleton<IAffiliateLinkBuilder, AwinLinkBuilder>();
        services.AddSingleton<IAffiliateLinkBuilder, QueryParametersLinkBuilder>();
        services.AddSingleton<IAffiliateLinkService, AffiliateLinkService>();
        services.AddScoped<ClickRedirectService>();
        return services;
    }
}
