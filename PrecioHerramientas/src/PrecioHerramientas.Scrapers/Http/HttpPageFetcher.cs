using System.Net;
using Microsoft.Extensions.Logging;

namespace PrecioHerramientas.Scrapers.Http;

public sealed class HttpPageFetcher(IHttpClientFactory httpClientFactory, ILogger<HttpPageFetcher> logger) : IPageFetcher
{
    public async Task<string?> GetHtmlAsync(string url, string? waitForSelector, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(HttpClientNames.Scraper);
        using var response = await client.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogInformation("Página no encontrada: {Url}", url);
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
