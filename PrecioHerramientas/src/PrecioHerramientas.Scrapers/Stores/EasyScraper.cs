using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Platforms;

namespace PrecioHerramientas.Scrapers.Stores;

/// <summary>
/// Easy Chile (Cencosud). El catálogo sigue en VTEX, pero el sitio www.easy.cl tiene
/// frontend propio y ya no expone <c>/api/catalog_system/...</c> (responde 404). La API
/// se consulta en el dominio de la cuenta VTEX, <c>easycl.vtexcommercestable.com.br</c>,
/// y las URLs públicas se arman como <c>https://www.easy.cl/{linkText}/p</c>.
/// </summary>
public sealed class EasyScraper(IHttpClientFactory httpClientFactory, IOptions<ScraperOptions> options, ILogger<EasyScraper> logger)
    : VtexScraperBase(httpClientFactory, options, logger)
{
    public const string Key = "easy";

    public override StoreDescriptor Store => Descriptor;

    private static readonly StoreDescriptor Descriptor = new(Key, "Easy", "https://www.easy.cl");

    protected override string DefaultApiBaseUrl => "https://easycl.vtexcommercestable.com.br";

    protected override IReadOnlyList<string> DefaultQueries { get; } =
    [
        "taladro", "rotomartillo", "esmeril angular", "atornillador", "sierra circular", "lijadora", "compresor", "hidrolavadora",
    ];
}
