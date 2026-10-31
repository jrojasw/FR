using System.Runtime.CompilerServices;
using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Http;
using PrecioHerramientas.Scrapers.Parsing;

namespace PrecioHerramientas.Scrapers.Platforms;

/// <summary>Cómo se descargan las páginas de una tienda.</summary>
public enum PageFetchMode
{
    /// <summary>Solo HTTP directo (rápido y liviano).</summary>
    Http,

    /// <summary>Siempre con navegador (Playwright).</summary>
    Browser,

    /// <summary>HTTP; si la tienda lo bloquea o la página no trae los datos, se cambia a navegador por el resto de la ejecución.</summary>
    HttpWithBrowserFallback,
}

/// <summary>
/// Tiendas que se leen desde el HTML de sus listados con AngleSharp. Cada página se
/// obtiene por HTTP o con Playwright según <see cref="DefaultFetchMode"/>, que la
/// configuración puede forzar con <c>UsePlaywright</c> (true = navegador, false = HTTP).
/// </summary>
public abstract class HtmlListingScraperBase : StoreScraperBase
{
    private readonly IServiceProvider _services;

    protected HtmlListingScraperBase(IServiceProvider services, IOptions<ScraperOptions> options, ILogger logger)
        : base(options, logger)
    {
        _services = services;
    }

    protected abstract ListingSelectors DefaultSelectors { get; }
    protected ListingSelectors Selectors => DefaultSelectors.WithOverrides(Options.Selectors);
    protected virtual PageFetchMode DefaultFetchMode => PageFetchMode.Http;

    /// <summary>URL de la página <paramref name="page"/> (desde 1) para una búsqueda o categoría.</summary>
    protected abstract string BuildPageUrl(string query, int page);

    /// <summary>Categoría que se asigna a los productos de una consulta (opcional).</summary>
    protected virtual string? CategoryFor(string query) => null;

    /// <summary>
    /// En modo con respaldo: indica si el HTML recibido por HTTP no sirve (p. ej. una
    /// página de verificación anti-bot) y hay que reintentar con navegador.
    /// </summary>
    protected virtual bool NeedsBrowser(string html) => false;

    private PageFetchMode FetchMode => Options.UsePlaywright switch
    {
        true => PageFetchMode.Browser,
        false => PageFetchMode.Http,
        null => DefaultFetchMode,
    };

    /// <summary>
    /// URL de la página: usa <c>SearchUrlTemplate</c> de la configuración si existe
    /// (con <c>{query}</c> y <c>{page}</c>), o si no la de la clase.
    /// </summary>
    protected string PageUrl(string query, int page) => Options.SearchUrlTemplate is { Length: > 0 } template
        ? new Uri(BaseUri, template.Replace("{query}", Uri.EscapeDataString(query)).Replace("{page}", page.ToString())).AbsoluteUri
        : BuildPageUrl(query, page);

    protected override async IAsyncEnumerable<ScrapedProduct> ScrapeQueryAsync(string query, QueryContext context, [EnumeratorCancellation] CancellationToken ct)
    {
        var browsing = BrowsingContext.New(Configuration.Default);
        string? previousFirstId = null;

        for (int page = 1; page <= Options.MaxPagesPerQuery; page++)
        {
            var url = PageUrl(query, page);
            var html = await FetchAsync(url, ct);
            if (html is null)
            {
                if (page == 1) context.Failure = $"página no encontrada (404) en {url}";
                yield break;
            }

            using var document = await browsing.OpenAsync(req => req.Content(html).Address(url), ct);
            var products = ParsePage(document, query);
            if (products.Count == 0)
            {
                if (page == 1)
                    Logger.LogWarning("{Store}: no se encontraron productos en {Url}; revise los selectores", Store.Key, url);
                yield break;
            }

            // Algunas tiendas devuelven la última página una y otra vez al pasarse del final.
            if (products[0].ExternalId == previousFirstId) yield break;
            previousFirstId = products[0].ExternalId;

            foreach (var product in products) yield return product;
            await PoliteDelayAsync(ct);
        }
    }

    // Una vez que HTTP falla en modo con respaldo, el resto de la ejecución usa navegador.
    private bool _switchedToBrowser;

    private async Task<string?> FetchAsync(string url, CancellationToken ct)
    {
        var waitFor = Selectors.WaitFor ?? Selectors.Card;
        var mode = FetchMode;
        if (mode == PageFetchMode.Browser || _switchedToBrowser)
            return await BrowserFetcher.GetHtmlAsync(url, waitFor, ct);
        if (mode == PageFetchMode.Http)
            return await HttpFetcher.GetHtmlAsync(url, null, ct);

        try
        {
            var html = await HttpFetcher.GetHtmlAsync(url, null, ct);
            if (html is null || !NeedsBrowser(html)) return html;
            Logger.LogInformation("{Store}: la página por HTTP no trae los datos; se usará navegador", Store.Key);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogInformation("{Store}: HTTP respondió {Status}; se usará navegador", Store.Key, ex.StatusCode?.ToString() ?? ex.Message);
        }
        _switchedToBrowser = true;
        return await BrowserFetcher.GetHtmlAsync(url, waitFor, ct);
    }

    private IPageFetcher HttpFetcher => _services.GetRequiredKeyedService<IPageFetcher>(PageFetcherKeys.Http);
    private IPageFetcher BrowserFetcher => _services.GetRequiredKeyedService<IPageFetcher>(PageFetcherKeys.Browser);

    /// <summary>Lee la grilla con selectores y, si no encuentra nada, intenta con JSON-LD.</summary>
    protected virtual IReadOnlyList<ScrapedProduct> ParsePage(IDocument document, string query)
    {
        var products = HtmlListingParser.Parse(document, BaseUri, Selectors, CategoryFor(query));
        return products.Count > 0 ? products : JsonLdProductExtractor.Extract(document, BaseUri);
    }
}
