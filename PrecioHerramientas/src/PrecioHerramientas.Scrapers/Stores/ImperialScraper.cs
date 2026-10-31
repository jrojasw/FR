using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Parsing;
using PrecioHerramientas.Scrapers.Platforms;

namespace PrecioHerramientas.Scrapers.Stores;

/// <summary>
/// Imperial (grupo Sodimac), sobre Oracle Commerce Cloud. Usa la misma API que el sitio:
/// <list type="number">
/// <item><c>/ccstore/v1/assembler/pages/Default/services/guidedsearch?Ntt={término}&amp;searchType=simple&amp;Nrpp=36</c>
/// resuelve el término; si es una redirección (p. ej. "taladro" → categoría N-1033696118), se sigue.</item>
/// <item><c>.../guidedsearch/_/N-1033696118?Nrpp=36&amp;No={offset}</c> entrega los productos en
/// <c>records[].records[].attributes</c> (sku.activePrice, sku.availabilityStatus, product.route...)
/// y el total en <c>totalNumRecs</c>.</item>
/// </list>
/// Primero por HTTP con encabezados de navegador; si Imperial responde 403 se pide dentro de una
/// sesión de Chromium. Si la API no entrega nada, se abre el sitio en Chromium y se leen las
/// respuestas que pide la página. Una consulta "N-xxxx" se usa directo como categoría.
/// </summary>
public sealed class ImperialScraper(IServiceProvider services, IOptions<ScraperOptions> options, ILogger<ImperialScraper> logger)
    : BrowserCaptureScraperBase(services, options, logger)
{
    public const string Key = "imperial";
    private const int PageSize = 36;
    private const string GuidedSearch = "/ccstore/v1/assembler/pages/Default/services/guidedsearch";

    public override StoreDescriptor Store => Descriptor;

    private static readonly StoreDescriptor Descriptor = new(Key, "Imperial", "https://www.imperial.cl");

    protected override IReadOnlyList<string> DefaultQueries { get; } =
    [
        "taladro", "rotomartillo", "esmeril angular", "atornillador", "sierra circular", "lijadora", "compresor", "hidrolavadora",
    ];

    // Respaldo con navegador: la página de búsqueda del sitio (que redirige a la categoría).
    protected override string? BuildSearchPageUrl(string query, int page) =>
        page == 1 ? new Uri(BaseUri, $"/search?Ntt={Uri.EscapeDataString(query)}").AbsoluteUri : null;

    protected override ListingSelectors? FallbackSelectors { get; } = new()
    {
        Card = ".product-item, .product-tile, .product-card, [data-product-id], [data-pod='catalyst-pod'], div[id^='testId-pod-']",
        Title = ".product-item-link, .product-name, .pdp-link a, .product-card__title, [id^='testId-pod-displayName'], .pod-subTitle",
        Link = "a.product-item-link, .product-name a, .pdp-link a, a",
        Brand = ".product-brand, .brand, .product-card__brand, [id^='testId-pod-brandName'], .pod-title",
        Price = "[data-price-type='finalPrice'] .price, .price .sales .value, .product-card__price, [data-internet-price], .price",
        ListPrice = "[data-price-type='oldPrice'] .price, .price .strike-through .value, [data-normal-price]",
        Image = "img",
        SkuAttribute = "data-product-id",
        OutOfStock = ".stock.unavailable, .out-of-stock, .agotado",
    };

    private string SearchApiUrl(string term) =>
        new Uri(BaseUri, $"{GuidedSearch}?Ntt={Uri.EscapeDataString(term)}&searchType=simple&Nrpp={PageSize}").AbsoluteUri;

    private string NavigationApiUrl(string state, int offset) =>
        new Uri(BaseUri, $"{GuidedSearch}/_/{state}?Nrpp={PageSize}&No={offset}").AbsoluteUri;

    protected override async IAsyncEnumerable<ScrapedProduct> ScrapeQueryAsync(string query, QueryContext context, [EnumeratorCancellation] CancellationToken ct)
    {
        var first = await FirstPageAsync(query, ct);
        if (first.Products.Count == 0)
        {
            Logger.LogInformation("{Store}: \"{Query}\" sin productos en la API ({Reason}); se usa Chromium", Store.Key, query, first.Reason);
            await foreach (var product in base.ScrapeQueryAsync(query, context, ct))
                yield return product;
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var product in first.Products.Where(p => seen.Add(p.ExternalId)))
            yield return product;

        for (int page = 2; page <= Options.MaxPagesPerQuery && first.NextUrl is not null; page++)
        {
            var offset = (page - 1) * PageSize;
            if (first.Total is { } total && offset >= total) yield break;
            await PoliteDelayAsync(ct);

            var more = await PageAsync(first.NextUrl(offset), ct);
            var fresh = more.Where(p => seen.Add(p.ExternalId)).ToList();
            if (fresh.Count == 0) yield break;
            foreach (var product in fresh) yield return product;
        }
    }

    private sealed record FirstPage(IReadOnlyList<ScrapedProduct> Products, int? Total, Func<int, string>? NextUrl, string Reason);

    private async Task<FirstPage> FirstPageAsync(string query, CancellationToken ct)
    {
        try
        {
            // "N-1033696118" (o "/_/N-...") se usa directo como navegación de categoría.
            var direct = query.TrimStart('/', '_').StartsWith("N-", StringComparison.Ordinal) ? query.TrimStart('/', '_') : null;
            string? state = direct;
            string? json;

            if (state is null)
            {
                var searchUrl = SearchApiUrl(query);
                json = await FetchAsync(searchUrl, json: true, ct);
                if (json is null) return new([], null, null, $"404 en {searchUrl}");

                state = OccGuidedSearch.RedirectNavigationState(json);
                if (state is null)
                {
                    // Sin redirección: la búsqueda misma trae los resultados.
                    var found = CapturedJsonProductParser.Parse(json, BaseUri);
                    return new(found, OccGuidedSearch.TotalRecords(json),
                        offset => searchUrl + $"&No={offset}", found.Count == 0 ? "la búsqueda no trae productos ni redirección" : "");
                }
                Logger.LogInformation("{Store}: \"{Query}\" redirige a {State}", Store.Key, query, state);
            }

            json = await FetchAsync(NavigationApiUrl(state, 0), json: true, ct);
            if (json is null) return new([], null, null, $"404 en {NavigationApiUrl(state, 0)}");
            var products = CapturedJsonProductParser.Parse(json, BaseUri);
            return new(products, OccGuidedSearch.TotalRecords(json), offset => NavigationApiUrl(state, offset),
                products.Count == 0 ? $"{state} sin productos" : "");
        }
        catch (HttpRequestException ex)
        {
            return new([], null, null, ex.Message);
        }
    }

    private async Task<IReadOnlyList<ScrapedProduct>> PageAsync(string url, CancellationToken ct)
    {
        try
        {
            return await FetchAsync(url, json: true, ct) is { } json ? CapturedJsonProductParser.Parse(json, BaseUri) : [];
        }
        catch (HttpRequestException ex)
        {
            Logger.LogWarning("{Store}: no se pudo leer {Url}: {Message}", Store.Key, url, ex.Message);
            return [];
        }
    }
}
