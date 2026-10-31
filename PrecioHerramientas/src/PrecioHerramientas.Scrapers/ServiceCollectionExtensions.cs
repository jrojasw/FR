using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Http;
using PrecioHerramientas.Scrapers.Jobs;
using PrecioHerramientas.Scrapers.Stores;

namespace PrecioHerramientas.Scrapers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScrapers(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ScraperOptions>(configuration.GetSection(ScraperOptions.SectionName));

        services.AddHttpClient(HttpClientNames.Scraper, (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<ScraperOptions>>().Value;
                client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
                client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/json;q=0.9,*/*;q=0.8");
                client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es-CL,es;q=0.9");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            })
            // Reintentos con espera exponencial ante 5xx/429/timeouts, y circuit breaker.
            .AddStandardResilienceHandler(o =>
            {
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(90);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(2);
                o.Retry.MaxRetryAttempts = 3;
                o.Retry.Delay = TimeSpan.FromSeconds(2);
            });

        services.AddSingleton<HttpPageFetcher>();
        services.AddSingleton<PlaywrightPageFetcher>();
        services.AddKeyedSingleton<IPageFetcher>(PageFetcherKeys.Http, (sp, _) => sp.GetRequiredService<HttpPageFetcher>());
        services.AddKeyedSingleton<IPageFetcher>(PageFetcherKeys.Browser, (sp, _) => sp.GetRequiredService<PlaywrightPageFetcher>());
        services.AddSingleton<IBrowserSessionFactory>(sp => sp.GetRequiredService<PlaywrightPageFetcher>());

        // Una implementación por tienda. Para agregar una tienda: crear la clase en Stores/,
        // registrarla aquí y habilitarla en appsettings (Scrapers:Stores:<clave>).
        services.AddTransient<IStoreScraper, EasyScraper>();
        services.AddTransient<IStoreScraper, SodimacScraper>();
        services.AddTransient<IStoreScraper, ImperialScraper>();
        services.AddTransient<IStoreScraper, ConstrumartScraper>();

        services.AddScoped<ScrapeJob>();
        return services;
    }
}
