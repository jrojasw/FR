namespace PrecioHerramientas.Scrapers.Http;

/// <summary>Obtiene el HTML de una página, ya sea por HTTP directo o con un navegador.</summary>
public interface IPageFetcher
{
    /// <param name="waitForSelector">Solo para navegador: selector que indica que la página terminó de cargar precios.</param>
    Task<string?> GetHtmlAsync(string url, string? waitForSelector, CancellationToken ct);
}

/// <summary>Claves con que se registran los <see cref="IPageFetcher"/> en el contenedor.</summary>
public static class PageFetcherKeys
{
    public const string Http = "http";
    public const string Browser = "browser";
}
