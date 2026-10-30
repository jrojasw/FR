using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;

namespace PrecioHerramientas.Scrapers.Platforms;

public abstract class StoreScraperBase : IStoreScraper
{
    private readonly ScraperOptions _options;

    protected StoreScraperBase(IOptions<ScraperOptions> options, ILogger logger)
    {
        _options = options.Value;
        Logger = logger;
    }

    public abstract StoreDescriptor Store { get; }

    protected StoreScraperOptions Options => _options.For(Store.Key);
    protected ILogger Logger { get; }
    protected Uri BaseUri => new(Store.BaseUrl);

    /// <summary>Búsquedas / categorías a recorrer cuando la configuración no define ninguna.</summary>
    protected abstract IReadOnlyList<string> DefaultQueries { get; }

    protected IReadOnlyList<string> Queries => Options.Queries.Count > 0 ? Options.Queries : DefaultQueries;

    public async IAsyncEnumerable<ScrapedProduct> ScrapeAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        // Un mismo producto aparece en varias búsquedas; se entrega una sola vez.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var query in Queries)
        {
            int count = 0;
            await foreach (var product in ScrapeQueryAsync(query, ct))
            {
                if (!seen.Add(product.ExternalId)) continue;
                count++;
                yield return product;
            }
            Logger.LogInformation("{Store}: \"{Query}\" entregó {Count} productos nuevos", Store.Key, query, count);
        }
    }

    protected abstract IAsyncEnumerable<ScrapedProduct> ScrapeQueryAsync(string query, CancellationToken ct);

    protected Task PoliteDelayAsync(CancellationToken ct) =>
        Options.DelayBetweenRequestsMs > 0 ? Task.Delay(Options.DelayBetweenRequestsMs, ct) : Task.CompletedTask;
}
