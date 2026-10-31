using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Parsing;
using PrecioHerramientas.Scrapers.Platforms;

namespace PrecioHerramientas.Scrapers.Stores;

/// <summary>
/// Easy Chile (Cencosud). La API de catálogo VTEX ya no responde de forma directa: en
/// www.easy.cl da 404 y en el dominio interno de VTEX da 500. Por eso se abre la
/// búsqueda pública <c>https://www.easy.cl/search/{término}</c> en Chromium y se leen
/// las respuestas JSON que el sitio pide para pintar los productos (VTEX, Intelligent
/// Search o GraphQL; el parser reconoce las tres por su forma), con las cookies y
/// encabezados que el propio sitio usa. Respaldo: HTML renderizado.
/// </summary>
public sealed class EasyScraper(IServiceProvider services, IOptions<ScraperOptions> options, ILogger<EasyScraper> logger)
    : BrowserCaptureScraperBase(services, options, logger)
{
    public const string Key = "easy";

    public override StoreDescriptor Store => Descriptor;

    private static readonly StoreDescriptor Descriptor = new(Key, "Easy", "https://www.easy.cl");

    protected override IReadOnlyList<string> DefaultQueries { get; } =
    [
        "taladro", "rotomartillo", "esmeril angular", "atornillador", "sierra circular", "lijadora", "compresor", "hidrolavadora",
    ];

    // La paginación normal llega por la API capturada; esta URL se usa solo si no se reconoce.
    protected override string? BuildSearchPageUrl(string query, int page) =>
        new Uri(BaseUri, $"/search/{Uri.EscapeDataString(query)}" + (page > 1 ? $"?page={page}" : "")).AbsoluteUri;

    protected override ListingSelectors? FallbackSelectors { get; } = new()
    {
        Card = "[data-testid='product-card'], .product-card, article[class*='product'], [class*='productCard'], [class*='product-summary']",
        Title = "[data-testid='product-name'], [class*='productName'], [class*='product-name'], h2, h3",
        Link = "a",
        Brand = "[data-testid='product-brand'], [class*='productBrand'], [class*='brand']",
        Price = "[data-testid='product-price'], [class*='sellingPrice'], [class*='offerPrice'], [class*='price']",
        ListPrice = "[class*='listPrice'], [class*='normalPrice'], del, s",
        Image = "img",
    };
}
