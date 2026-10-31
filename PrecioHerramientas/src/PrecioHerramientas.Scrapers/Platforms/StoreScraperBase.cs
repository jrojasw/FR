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
        var failures = new List<string>();
        try
        {
            foreach (var query in Queries)
            {
                int count = 0;
                var context = new QueryContext();
                await foreach (var product in ScrapeQueryAsync(query, context, ct))
                {
                    if (!seen.Add(product.ExternalId)) continue;
                    count++;
                    yield return product;
                }
                if (context.Failure is { } failure)
                {
                    failures.Add($"\"{query}\": {failure}");
                    Logger.LogWarning("{Store}: \"{Query}\" falló: {Failure}", Store.Key, query, failure);
                }
                else
                {
                    Logger.LogInformation("{Store}: \"{Query}\" entregó {Count} productos nuevos", Store.Key, query, count);
                }
            }
        }
        finally
        {
            await CleanupAsync();
        }

        // Si todo falló o no apareció nada, la ejecución no puede quedar como "OK".
        if (Queries.Count > 0 && failures.Count == Queries.Count)
            throw new ScrapeFailedException($"{Store.Name}: todas las búsquedas fallaron. {string.Join("; ", failures.Take(3))}");
        if (Queries.Count > 0 && seen.Count == 0)
            throw new ScrapeFailedException($"{Store.Name}: ninguna búsqueda devolvió productos; revise la URL de búsqueda y los selectores.");
    }

    /// <summary>Lo que una búsqueda informa al terminar, además de sus productos.</summary>
    protected sealed class QueryContext
    {
        /// <summary>Motivo si la búsqueda no se pudo hacer (p. ej. "HTTP 404 en ..."). Nulo si respondió bien.</summary>
        public string? Failure { get; set; }
    }

    /// <summary>Se llama al terminar la ejecución (también si falla), p. ej. para cerrar el navegador.</summary>
    protected virtual ValueTask CleanupAsync() => ValueTask.CompletedTask;

    protected abstract IAsyncEnumerable<ScrapedProduct> ScrapeQueryAsync(string query, QueryContext context, CancellationToken ct);

    protected Task PoliteDelayAsync(CancellationToken ct) =>
        Options.DelayBetweenRequestsMs > 0 ? Task.Delay(Options.DelayBetweenRequestsMs, ct) : Task.CompletedTask;
}

/// <summary>La tienda no entregó datos utilizables; la ejecución se registra como fallida.</summary>
public sealed class ScrapeFailedException(string message) : Exception(message);
