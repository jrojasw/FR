namespace PrecioHerramientas.Scrapers.Http;

/// <summary>Obtiene el HTML de una página, ya sea por HTTP directo o con un navegador.</summary>
public interface IPageFetcher
{
    /// <param name="waitForSelector">Solo para navegador: selector que indica que la página terminó de cargar precios.</param>
    Task<string?> GetHtmlAsync(string url, string? waitForSelector, CancellationToken ct);
}
