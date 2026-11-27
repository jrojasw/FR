using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Parsing;
using PrecioHerramientas.Scrapers.Platforms;

namespace PrecioHerramientas.Scrapers.Stores;

/// <summary>
/// Paris.cl (Cencosud), Next.js con App Router. Cada página de <c>/search?q={término}&amp;page=N</c>
/// trae 30 productos y el total en el JSON-LD de la página (ver <see cref="ParisSearchParser"/>).
/// Todo por HTTP; si Paris lo bloquea se pasa a Chromium, y si la página no trae ese JSON se
/// usa el respaldo de <see cref="BrowserCaptureScraperBase"/> (respuestas que pide la página y HTML renderizado).
/// Paris es el programa de afiliados de Awin (clave "paris" en la sección Affiliates).
/// </summary>
public sealed class ParisScraper(IServiceProvider services, IOptions<ScraperOptions> options, ILogger<ParisScraper> logger)
    : BrowserCaptureScraperBase(services, options, logger)
{
    public const string Key = "paris";

    public override StoreDescriptor Store => Descriptor;

    private static readonly StoreDescriptor Descriptor = new(Key, "Paris", "https://www.paris.cl");

    protected override IReadOnlyList<string> DefaultQueries { get; } =
    [
        "taladro", "rotomartillo", "esmeril angular", "atornillador", "sierra circular", "lijadora", "compresor", "hidrolavadora",
    ];

    protected override string? BuildSearchPageUrl(string query, int page) =>
        new Uri(BaseUri, $"/search?q={Uri.EscapeDataString(query)}" + (page > 1 ? $"&page={page}" : "")).AbsoluteUri;

    protected override async IAsyncEnumerable<ScrapedProduct> ScrapeQueryAsync(string query, QueryContext context, [EnumeratorCancellation] CancellationToken ct)
    {
        var first = await ReadPageAsync(query, 1, ct);
        if (first.Products.Count == 0)
        {
            Logger.LogInformation("{Store}: \"{Query}\" sin JSON-LD en la página; se usa Chromium", Store.Key, query);
            await foreach (var product in base.ScrapeQueryAsync(query, context, ct))
                yield return product;
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var product in first.Products.Where(p => seen.Add(p.ExternalId)))
            yield return product;

        for (int page = 2; page <= Options.MaxPagesPerQuery; page++)
        {
            if (first.Total is { } total && seen.Count >= total) yield break;
            await PoliteDelayAsync(ct);

            var more = await ReadPageAsync(query, page, ct);
            var fresh = more.Products.Where(p => seen.Add(p.ExternalId)).ToList();
            if (fresh.Count == 0) yield break;
            foreach (var product in fresh) yield return product;
        }
    }

    private async Task<ParisSearchParser.Result> ReadPageAsync(string query, int page, CancellationToken ct)
    {
        var url = SearchPageUrl(query, page);
        if (url is null) return new([], null);
        try
        {
            var html = await FetchAsync(url, json: false, ct);
            if (html is null) return new([], null);
            var result = ParisSearchParser.Parse(html, BaseUri);
            Logger.LogInformation("{Store}: \"{Query}\" página {Page}: {Count} de {Total} productos", Store.Key, query, page,
                result.Products.Count, result.Total?.ToString() ?? "?");
            return result;
        }
        catch (HttpRequestException ex)
        {
            // En la primera página, base.ScrapeQueryAsync lo reintenta con Chromium; después se conserva lo obtenido.
            Logger.LogWarning("{Store}: no se pudo leer la página {Page} de \"{Query}\": {Message}", Store.Key, page, query, ex.Message);
            return new([], null);
        }
    }
}
