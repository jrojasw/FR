using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Platforms;

namespace PrecioHerramientas.Scrapers.Stores;

/// <summary>Easy Chile (Cencosud), sobre VTEX.</summary>
public sealed class EasyScraper(IHttpClientFactory httpClientFactory, IOptions<ScraperOptions> options, ILogger<EasyScraper> logger)
    : VtexScraperBase(httpClientFactory, options, logger)
{
    public const string Key = "easy";

    public override StoreDescriptor Store => Descriptor;

    private static readonly StoreDescriptor Descriptor = new(Key, "Easy", "https://www.easy.cl");

    protected override IReadOnlyList<string> DefaultQueries { get; } =
    [
        "taladro", "atornillador", "rotomartillo", "esmeril angular", "sierra circular", "sierra caladora",
        "lijadora", "cepillo electrico", "router", "compresor", "hidrolavadora", "soldadora", "multiherramienta",
        "set herramientas", "llave", "alicate", "destornillador", "martillo", "nivel", "huincha de medir",
    ];
}
