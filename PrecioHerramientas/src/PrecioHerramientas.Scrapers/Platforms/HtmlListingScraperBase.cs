using System.Runtime.CompilerServices;
using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Http;
using PrecioHerramientas.Scrapers.Parsing;

namespace PrecioHerramientas.Scrapers.Platforms;

/// <summary>
/// Tiendas que se leen desde el HTML de sus listados con AngleSharp. Cada página se
/// obtiene por HTTP o, si la tienda pinta los precios con JavaScript, con Playwright
/// (<see cref="UsesBrowserByDefault"/> o <c>UsePlaywright</c> en configuración).
/// </summary>
public abstract class HtmlListingScraperBase : StoreScraperBase
{
    private readonly IServiceProvider _services;

    protected HtmlListingScraperBase(IServiceProvider services, IOptions<ScraperOptions> options, ILogger logger)
        : base(options, logger)
    {
        _services = services;
    }

    protected abstract ListingSelectors DefaultSelectors { get; }
    protected ListingSelectors Selectors => DefaultSelectors.WithOverrides(Options.Selectors);
    protected virtual bool UsesBrowserByDefault => false;

    /// <summary>URL de la página <paramref name="page"/> (desde 1) para una búsqueda o categoría.</summary>
    protected abstract string BuildPageUrl(string query, int page);

    /// <summary>Categoría que se asigna a los productos de una consulta (opcional).</summary>
    protected virtual string? CategoryFor(string query) => null;

    private IPageFetcher Fetcher => Options.UsePlaywright ?? UsesBrowserByDefault
        ? _services.GetRequiredService<PlaywrightPageFetcher>()
        : _services.GetRequiredService<HttpPageFetcher>();

    protected override async IAsyncEnumerable<ScrapedProduct> ScrapeQueryAsync(string query, [EnumeratorCancellation] CancellationToken ct)
    {
        var fetcher = Fetcher;
        var browsing = BrowsingContext.New(Configuration.Default);
        string? previousFirstId = null;

        for (int page = 1; page <= Options.MaxPagesPerQuery; page++)
        {
            var url = BuildPageUrl(query, page);
            var html = await fetcher.GetHtmlAsync(url, Selectors.WaitFor ?? Selectors.Card, ct);
            if (html is null) yield break;

            using var document = await browsing.OpenAsync(req => req.Content(html).Address(url), ct);
            var products = ParsePage(document, query);
            if (products.Count == 0)
            {
                if (page == 1)
                    Logger.LogWarning("{Store}: no se encontraron productos en {Url}; revise los selectores", Store.Key, url);
                yield break;
            }

            // Algunas tiendas devuelven la última página una y otra vez al pasarse del final.
            if (products[0].ExternalId == previousFirstId) yield break;
            previousFirstId = products[0].ExternalId;

            foreach (var product in products) yield return product;
            await PoliteDelayAsync(ct);
        }
    }

    /// <summary>Lee la grilla con selectores y, si no encuentra nada, intenta con JSON-LD.</summary>
    protected virtual IReadOnlyList<ScrapedProduct> ParsePage(IDocument document, string query)
    {
        var products = HtmlListingParser.Parse(document, BaseUri, Selectors, CategoryFor(query));
        return products.Count > 0 ? products : JsonLdProductExtractor.Extract(document, BaseUri);
    }
}
