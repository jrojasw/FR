using System.Runtime.CompilerServices;
using AngleSharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Http;
using PrecioHerramientas.Scrapers.Parsing;

namespace PrecioHerramientas.Scrapers.Platforms;

/// <summary>
/// Tiendas cuyo listado arma el JavaScript del sitio llamando a una API propia (que puede
/// pedir cookies de sesión o encabezados que no conviene replicar a mano). Se abre la
/// búsqueda en Chromium como un visitante, se sigue cualquier redirección del sitio
/// (p. ej. búsqueda → categoría) y se leen las respuestas JSON que la página pidió
/// (<see cref="CapturedJsonProductParser"/>). Las páginas siguientes se piden repitiendo
/// esa llamada con la paginación cambiada (<see cref="PagingUrl"/>) dentro de la misma
/// sesión. Si no se reconoce ninguna respuesta, se lee el HTML ya renderizado
/// (__NEXT_DATA__, selectores CSS y JSON-LD).
/// </summary>
public abstract class BrowserCaptureScraperBase(IServiceProvider services, IOptions<ScraperOptions> options, ILogger logger)
    : StoreScraperBase(options, logger)
{
    private IBrowserSession? _session;

    /// <summary>URL de la página <paramref name="page"/> (desde 1) de una búsqueda; null si el sitio no pagina por URL.</summary>
    protected abstract string? BuildSearchPageUrl(string query, int page);

    /// <summary>Respuestas que vale la pena leer (por URL). Por defecto todas las JSON.</summary>
    protected virtual bool ShouldCapture(string url) => true;

    /// <summary>Selector que indica que el listado ya se pintó (opcional).</summary>
    protected virtual string? WaitForSelector => null;

    /// <summary>Selectores de la grilla para el respaldo HTML (opcional).</summary>
    protected virtual ListingSelectors? FallbackSelectors => null;

    /// <summary>
    /// URL de búsqueda: <c>SearchUrlTemplate</c> de la configuración (con {query} y {page})
    /// si existe; si no, la de la clase. Una consulta que empieza con "/" es una ruta del
    /// sitio (p. ej. una categoría) y se usa tal cual en la primera página.
    /// </summary>
    protected string? SearchPageUrl(string query, int page)
    {
        if (query.StartsWith('/'))
            return page == 1 ? new Uri(BaseUri, query).AbsoluteUri : null;
        if (Options.SearchUrlTemplate is { Length: > 0 } template)
            return page == 1 || template.Contains("{page}")
                ? new Uri(BaseUri, template.Replace("{query}", Uri.EscapeDataString(query)).Replace("{page}", page.ToString())).AbsoluteUri
                : null;
        return BuildSearchPageUrl(query, page);
    }

    protected override async IAsyncEnumerable<ScrapedProduct> ScrapeQueryAsync(string query, QueryContext context, [EnumeratorCancellation] CancellationToken ct)
    {
        var first = await LoadFirstPageAsync(query, context, ct);
        if (first is null) yield break;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var product in first.Products.Where(p => seen.Add(p.ExternalId)))
            yield return product;

        for (int page = 2; page <= Options.MaxPagesPerQuery; page++)
        {
            await PoliteDelayAsync(ct);
            var more = await LoadNextPageAsync(query, first.ApiUrl, page, ct);
            var fresh = more.Where(p => seen.Add(p.ExternalId)).ToList();
            if (fresh.Count == 0) yield break;
            foreach (var product in fresh) yield return product;
        }
    }

    private sealed record FirstPage(IReadOnlyList<ScrapedProduct> Products, string? ApiUrl);

    private async Task<FirstPage?> LoadFirstPageAsync(string query, QueryContext context, CancellationToken ct)
    {
        var url = SearchPageUrl(query, 1)!;
        BrowserPage? page;
        try
        {
            page = await (await SessionAsync(ct)).LoadAsync(url, ShouldCapture, WaitForSelector, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Failure = $"{ex.GetType().Name} al abrir {url}: {ex.Message}";
            return null;
        }
        if (page is null)
        {
            context.Failure = $"página no encontrada (404) en {url}";
            return null;
        }

        // La respuesta con más productos es la del listado; las demás suelen ser
        // recomendaciones o el minicarro.
        var best = page.Responses
            .Select(r => (r.Url, Products: CapturedJsonProductParser.Parse(r.Body, BaseUri)))
            .Where(r => r.Products.Count > 0)
            .OrderByDescending(r => r.Products.Count)
            .FirstOrDefault();
        if (best.Products is { Count: > 0 })
        {
            Logger.LogInformation("{Store}: \"{Query}\" leído desde {Api} ({Count} productos)", Store.Key, query, best.Url, best.Products.Count);
            return new FirstPage(best.Products, best.Url);
        }

        var fromHtml = await ParseHtmlAsync(page.Html, page.FinalUrl, ct);
        if (fromHtml.Count > 0)
            return new FirstPage(fromHtml, null);

        context.Failure = $"sin productos en {page.FinalUrl} ({page.Responses.Count} respuestas JSON revisadas)";
        return null;
    }

    private async Task<IReadOnlyList<ScrapedProduct>> LoadNextPageAsync(string query, string? apiUrl, int page, CancellationToken ct)
    {
        try
        {
            if (apiUrl is not null && PagingUrl.ForPage(apiUrl, page) is { } nextApi)
                return (await (await SessionAsync(ct)).GetAsync(nextApi, ct)) is { } json
                    ? CapturedJsonProductParser.Parse(json, BaseUri)
                    : [];

            if (SearchPageUrl(query, page) is { } nextPage
                && await (await SessionAsync(ct)).LoadAsync(nextPage, ShouldCapture, WaitForSelector, ct) is { } loaded)
            {
                var products = loaded.Responses.SelectMany(r => CapturedJsonProductParser.Parse(r.Body, BaseUri)).ToList();
                return products.Count > 0 ? products : await ParseHtmlAsync(loaded.Html, loaded.FinalUrl, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // La primera página ya entregó productos: se conserva lo obtenido.
            Logger.LogWarning("{Store}: no se pudo leer la página {Page} de \"{Query}\": {Message}", Store.Key, page, query, ex.Message);
        }
        return [];
    }

    private async Task<IReadOnlyList<ScrapedProduct>> ParseHtmlAsync(string html, string url, CancellationToken ct)
    {
        using var document = await BrowsingContext.New(Configuration.Default).OpenAsync(req => req.Content(html).Address(url), ct);

        if (document.QuerySelector("script#__NEXT_DATA__")?.TextContent is { Length: > 0 } nextData
            && CapturedJsonProductParser.Parse(nextData, BaseUri) is { Count: > 0 } fromNext)
            return fromNext;

        if (FallbackSelectors is { } selectors
            && HtmlListingParser.Parse(document, BaseUri, selectors.WithOverrides(Options.Selectors)) is { Count: > 0 } fromGrid)
            return fromGrid;

        return JsonLdProductExtractor.Extract(document, BaseUri);
    }

    private async Task<IBrowserSession> SessionAsync(CancellationToken ct) =>
        _session ??= await services.GetRequiredService<IBrowserSessionFactory>().OpenAsync(ct);

    protected override async ValueTask CleanupAsync()
    {
        if (_session is not null)
        {
            await _session.DisposeAsync();
            _session = null;
        }
    }
}
