using PrecioHerramientas.Core.Scraping;

namespace PrecioHerramientas.Scrapers.Abstractions;

/// <summary>
/// Recolecta productos y precios de una tienda. Cada tienda tiene su propia
/// implementación; las comunes a una plataforma (VTEX, HTML, Playwright) se
/// apoyan en las clases base de <c>Platforms</c>.
/// </summary>
public interface IStoreScraper
{
    StoreDescriptor Store { get; }

    /// <summary>
    /// Devuelve los productos a medida que se descargan, para que la ingesta pueda
    /// guardarlos por lotes sin esperar a que termine todo el catálogo.
    /// </summary>
    IAsyncEnumerable<ScrapedProduct> ScrapeAsync(CancellationToken ct = default);
}
