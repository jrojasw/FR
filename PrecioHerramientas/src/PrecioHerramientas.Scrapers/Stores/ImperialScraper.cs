using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Parsing;
using PrecioHerramientas.Scrapers.Platforms;

namespace PrecioHerramientas.Scrapers.Stores;

/// <summary>
/// Imperial (grupo Sodimac). Rechaza HTTP directo (403) y su buscador redirige con
/// JavaScript a una categoría (p. ej. "taladro" → /taladros/category/000300030352), con
/// rutas al estilo de Oracle Commerce Cloud. Se abre la búsqueda en Chromium, se sigue
/// esa redirección y se leen las respuestas JSON que arma el listado (búsqueda guiada
/// <c>/ccstoreui/v1/search</c> o <c>/ccstore/v1/products</c>, reconocidas por su forma).
/// Respaldo: HTML renderizado. Las búsquedas que empiezan con "/" son rutas de categoría.
/// </summary>
public sealed class ImperialScraper(IServiceProvider services, IOptions<ScraperOptions> options, ILogger<ImperialScraper> logger)
    : BrowserCaptureScraperBase(services, options, logger)
{
    public const string Key = "imperial";

    public override StoreDescriptor Store => Descriptor;

    private static readonly StoreDescriptor Descriptor = new(Key, "Imperial", "https://www.imperial.cl");

    protected override IReadOnlyList<string> DefaultQueries { get; } =
    [
        "taladro", "rotomartillo", "esmeril angular", "atornillador", "sierra circular", "lijadora", "compresor", "hidrolavadora",
    ];

    // El sitio pagina por su API; si no se reconoce, no hay paginación por URL.
    protected override string? BuildSearchPageUrl(string query, int page) =>
        page == 1 ? new Uri(BaseUri, $"/searchresults?Ntt={Uri.EscapeDataString(query)}").AbsoluteUri : null;

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
}
