using System.Net;
using Microsoft.Extensions.Logging;

namespace PrecioHerramientas.Scrapers.Http;

public sealed class HttpPageFetcher(IHttpClientFactory httpClientFactory, ILogger<HttpPageFetcher> logger) : IPageFetcher
{
    public async Task<string?> GetHtmlAsync(string url, string? waitForSelector, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(HttpClientNames.Scraper);
        // Encabezados que envía un navegador al abrir una página (además de los del cliente).
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "none");
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogInformation("Página no encontrada: {Url}", url);
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
