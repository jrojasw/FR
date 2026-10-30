using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Parsing;
using PrecioHerramientas.Scrapers.Platforms;

namespace PrecioHerramientas.Scrapers.Stores;

/// <summary>
/// Imperial: listados HTML servidos desde el servidor, leídos con AngleSharp sin
/// navegador. Selectores y URL de búsqueda se pueden ajustar en
/// <c>Scrapers:Stores:imperial</c> si la tienda cambia su sitio.
/// </summary>
public sealed class ImperialScraper(IServiceProvider services, IOptions<ScraperOptions> options, ILogger<ImperialScraper> logger)
    : HtmlListingScraperBase(services, options, logger)
{
    public const string Key = "imperial";

    public override StoreDescriptor Store => Descriptor;

    private static readonly StoreDescriptor Descriptor = new(Key, "Imperial", "https://www.imperial.cl");

    protected override IReadOnlyList<string> DefaultQueries { get; } =
    [
        "taladro", "atornillador", "rotomartillo", "esmeril", "sierra circular", "caladora", "lijadora",
        "compresor", "hidrolavadora", "set herramientas", "llave", "alicate",
    ];

    protected override ListingSelectors DefaultSelectors { get; } = new()
    {
        Card = ".product-item, .product-tile, [data-product-id]",
        Title = ".product-item-link, .product-name, .pdp-link a",
        Link = "a.product-item-link, .product-name a, a",
        Brand = ".product-brand, .brand",
        Price = "[data-price-type='finalPrice'] .price, .price .sales .value, .price",
        ListPrice = "[data-price-type='oldPrice'] .price, .price .strike-through .value",
        Image = "img",
        SkuAttribute = "data-product-id",
        OutOfStock = ".stock.unavailable, .out-of-stock",
    };

    /// <summary>Una consulta que empieza con "/" se toma como ruta de categoría; si no, como búsqueda.</summary>
    protected override string BuildPageUrl(string query, int page) => query.StartsWith('/')
        ? new Uri(BaseUri, $"{query}?p={page}").ToString()
        : new Uri(BaseUri, $"/catalogsearch/result/?q={Uri.EscapeDataString(query)}&p={page}").ToString();
}
