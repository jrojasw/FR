using AngleSharp.Dom;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Parsing;
using PrecioHerramientas.Scrapers.Platforms;

namespace PrecioHerramientas.Scrapers.Stores;

/// <summary>
/// Imperial (grupo Sodimac). Rechaza las solicitudes HTTP directas con 403, así que
/// usa siempre Chromium (Playwright) con encabezados de navegador de escritorio.
/// Cada página se lee en este orden: JSON <c>__NEXT_DATA__</c> (si el sitio usa la
/// plataforma Next.js del grupo), grilla con selectores CSS y JSON-LD de schema.org.
/// URL de búsqueda y selectores se ajustan en <c>Scrapers:Stores:imperial</c>.
/// </summary>
public sealed class ImperialScraper(IServiceProvider services, IOptions<ScraperOptions> options, ILogger<ImperialScraper> logger)
    : HtmlListingScraperBase(services, options, logger)
{
    public const string Key = "imperial";

    public override StoreDescriptor Store => Descriptor;

    private static readonly StoreDescriptor Descriptor = new(Key, "Imperial", "https://www.imperial.cl");

    protected override PageFetchMode DefaultFetchMode => PageFetchMode.Browser;

    protected override IReadOnlyList<string> DefaultQueries { get; } =
    [
        "taladro", "rotomartillo", "esmeril angular", "atornillador", "sierra circular", "lijadora", "compresor", "hidrolavadora",
    ];

    // Varias alternativas por campo (separadas por coma) para cubrir los temas de
    // tienda más comunes; se toma la primera que exista en cada tarjeta.
    protected override ListingSelectors DefaultSelectors { get; } = new()
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
        WaitFor = ".product-item, .product-tile, .product-card, [data-product-id], #__NEXT_DATA__, script[type='application/ld+json']",
    };

    /// <summary>Una consulta que empieza con "/" se toma como ruta de categoría; si no, como búsqueda.</summary>
    protected override string BuildPageUrl(string query, int page) => query.StartsWith('/')
        ? new Uri(BaseUri, $"{query}?p={page}").AbsoluteUri
        : new Uri(BaseUri, $"/catalogsearch/result/?q={Uri.EscapeDataString(query)}&p={page}").AbsoluteUri;

    protected override IReadOnlyList<ScrapedProduct> ParsePage(IDocument document, string query)
    {
        var json = document.QuerySelector("script#__NEXT_DATA__")?.TextContent;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var products = NextDataProductParser.Parse(json, BaseUri);
                if (products.Count > 0) return products;
            }
            catch (System.Text.Json.JsonException)
            {
                // Sigue con la grilla HTML.
            }
        }
        return base.ParsePage(document, query);
    }
}
