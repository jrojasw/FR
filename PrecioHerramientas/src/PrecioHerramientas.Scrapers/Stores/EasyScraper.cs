using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Parsing;
using PrecioHerramientas.Scrapers.Platforms;

namespace PrecioHerramientas.Scrapers.Stores;

/// <summary>
/// Easy Chile (Cencosud), sitio Next.js. Fuente principal: el JSON de Next.js.
/// <list type="number">
/// <item>Página 1: <c>/search/{término}</c>, cuyo <c>&lt;script id="__NEXT_DATA__"&gt;</c> trae los
/// productos (<c>pageProps.serverProductsResponse.productList</c>), el total
/// (<c>recordsFiltered</c>) y el <c>buildId</c> del despliegue actual de Easy.</item>
/// <item>Páginas siguientes: <c>/_next/data/{buildId}/search/{término}.json?search={término}&amp;page=N</c>.
/// El buildId cambia con cada despliegue de Easy: si esa URL da 404, se vuelve a leer de la página.</item>
/// </list>
/// Todo por HTTP; si Easy lo bloquea se pasa a Chromium, y si la página no trae ese JSON se
/// usa el respaldo de <see cref="BrowserCaptureScraperBase"/> (respuestas que pide la página).
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

    // Se mantiene entre búsquedas de la misma ejecución.
    private string? _buildId;

    protected override string? BuildSearchPageUrl(string query, int page) =>
        new Uri(BaseUri, $"/search/{Uri.EscapeDataString(query)}" + (page > 1 ? $"?page={page}" : "")).AbsoluteUri;

    private string NextDataUrl(string query, int page)
    {
        var term = Uri.EscapeDataString(query);
        return new Uri(BaseUri, $"/_next/data/{_buildId}/search/{term}.json?search={term}" + (page > 1 ? $"&page={page}" : "")).AbsoluteUri;
    }

    protected override async IAsyncEnumerable<ScrapedProduct> ScrapeQueryAsync(string query, QueryContext context, [EnumeratorCancellation] CancellationToken ct)
    {
        var first = await FirstPageAsync(query, ct);
        if (first.Products.Count == 0)
        {
            Logger.LogInformation("{Store}: \"{Query}\" sin datos de Next.js ({Reason}); se usa Chromium", Store.Key, query, first.Reason);
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

            var more = await NextPageAsync(query, page, ct);
            var fresh = more.Where(p => seen.Add(p.ExternalId)).ToList();
            if (fresh.Count == 0) yield break;
            foreach (var product in fresh) yield return product;
        }
    }

    private sealed record FirstPage(IReadOnlyList<ScrapedProduct> Products, int? Total, string Reason);

    private async Task<FirstPage> FirstPageAsync(string query, CancellationToken ct)
    {
        var url = BuildSearchPageUrl(query, 1)!;
        try
        {
            var html = await FetchAsync(url, json: false, ct);
            if (html is null) return new([], null, $"404 en {url}");
            if (EasyNextDataParser.ExtractNextDataJson(html) is not { } json) return new([], null, "la página no trae __NEXT_DATA__");

            var result = EasyNextDataParser.Parse(json, BaseUri);
            _buildId = result.BuildId ?? _buildId;
            Logger.LogInformation("{Store}: \"{Query}\" {Count} de {Total} productos desde __NEXT_DATA__ (buildId {BuildId})",
                Store.Key, query, result.Products.Count, result.Total?.ToString() ?? "?", _buildId);
            return new(result.Products, result.Total, result.Products.Count == 0 ? "__NEXT_DATA__ sin productList" : "");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return new([], null, ex.Message);
        }
    }

    private async Task<IReadOnlyList<ScrapedProduct>> NextPageAsync(string query, int page, CancellationToken ct)
    {
        if (_buildId is null) return [];
        try
        {
            var json = await FetchAsync(NextDataUrl(query, page), json: true, ct);
            if (json is null)
            {
                // Easy publicó una versión nueva durante la ejecución: se relee el buildId.
                var previous = _buildId;
                await FirstPageAsync(query, ct);
                if (_buildId == previous) return [];
                Logger.LogInformation("{Store}: buildId cambió de {Old} a {New}", Store.Key, previous, _buildId);
                json = await FetchAsync(NextDataUrl(query, page), json: true, ct);
                if (json is null) return [];
            }
            return EasyNextDataParser.Parse(json, BaseUri).Products;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            // Las páginas anteriores ya entregaron productos: se conserva lo obtenido.
            Logger.LogWarning("{Store}: no se pudo leer la página {Page} de \"{Query}\": {Message}", Store.Key, page, query, ex.Message);
            return [];
        }
    }
}
