using System.Text.Json;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Parsing;
using PrecioHerramientas.Scrapers.Platforms;

namespace PrecioHerramientas.Scrapers.Stores;

/// <summary>
/// Sodimac Chile (plataforma Next.js de Falabella). La página de búsqueda
/// <c>/sodimac-cl/buscar?Ntt=...</c> trae los resultados como JSON en
/// <c>&lt;script id="__NEXT_DATA__"&gt;</c> (<c>props.pageProps.results</c>), que es
/// más estable que la grilla HTML. Primero se pide por HTTP; si la tienda bloquea
/// la solicitud o no entrega ese JSON, se pasa a Chromium (Playwright).
/// Si nada de eso funciona, se recurre a los selectores CSS y a JSON-LD.
/// </summary>
public sealed class SodimacScraper(IServiceProvider services, IOptions<ScraperOptions> options, ILogger<SodimacScraper> logger)
    : HtmlListingScraperBase(services, options, logger)
{
    public const string Key = "sodimac";

    public override StoreDescriptor Store => Descriptor;

    private static readonly StoreDescriptor Descriptor = new(Key, "Sodimac", "https://www.sodimac.cl");

    protected override PageFetchMode DefaultFetchMode => PageFetchMode.HttpWithBrowserFallback;

    protected override IReadOnlyList<string> DefaultQueries { get; } =
    [
        "taladro", "rotomartillo", "esmeril angular", "atornillador", "sierra circular", "lijadora", "compresor", "hidrolavadora",
    ];

    // Respaldo si cambia el JSON: tarjetas ("pods") de la grilla de Falabella/Sodimac.
    protected override ListingSelectors DefaultSelectors { get; } = new()
    {
        Card = "[data-pod='catalyst-pod'], div[id^='testId-pod-'], div.pod",
        Title = "[id^='testId-pod-displayName'], b.pod-subTitle, .pod-subTitle",
        Brand = "[id^='testId-pod-brandName'], b.pod-title, .pod-title",
        Link = "a",
        Price = "[data-internet-price], [data-event-price], li.prices-0 span, .prices-0 span",
        ListPrice = "[data-normal-price]",
        Image = "img",
        SkuAttribute = "data-key",
        WaitFor = "#__NEXT_DATA__",
    };

    protected override string BuildPageUrl(string query, int page) =>
        new Uri(BaseUri, $"/sodimac-cl/buscar?Ntt={Uri.EscapeDataString(query)}&page={page}").AbsoluteUri;

    // Sin __NEXT_DATA__ suele ser una página de verificación anti-bot.
    protected override bool NeedsBrowser(string html) => !html.Contains("__NEXT_DATA__", StringComparison.Ordinal);

    protected override IReadOnlyList<ScrapedProduct> ParsePage(IDocument document, string query)
    {
        var json = document.QuerySelector("script#__NEXT_DATA__")?.TextContent;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var products = ParseNextData(json);
                if (products.Count > 0) return products;
            }
            catch (JsonException ex)
            {
                Logger.LogWarning(ex, "Sodimac: __NEXT_DATA__ con formato inesperado");
            }
        }
        return base.ParsePage(document, query);
    }

    /// <summary>Lee los productos de <c>props.pageProps.results</c> (ver <see cref="NextDataProductParser"/>).</summary>
    public IReadOnlyList<ScrapedProduct> ParseNextData(string json) => NextDataProductParser.Parse(json, BaseUri);
}
