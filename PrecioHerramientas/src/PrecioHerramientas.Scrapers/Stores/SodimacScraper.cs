using System.Text.Json;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Core.Text;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Parsing;
using PrecioHerramientas.Scrapers.Platforms;

namespace PrecioHerramientas.Scrapers.Stores;

/// <summary>
/// Sodimac Chile (plataforma Falabella). Los listados se protegen contra bots y
/// completan los precios con JavaScript, por eso usa Playwright por defecto.
/// Primero lee el JSON de <c>__NEXT_DATA__</c>; si cambia, recurre a los selectores.
/// </summary>
public sealed class SodimacScraper(IServiceProvider services, IOptions<ScraperOptions> options, ILogger<SodimacScraper> logger)
    : HtmlListingScraperBase(services, options, logger)
{
    public const string Key = "sodimac";

    public override StoreDescriptor Store => Descriptor;

    private static readonly StoreDescriptor Descriptor = new(Key, "Sodimac", "https://sodimac.falabella.com");

    protected override bool UsesBrowserByDefault => true;

    protected override IReadOnlyList<string> DefaultQueries { get; } =
    [
        "taladro", "atornillador", "rotomartillo", "esmeril angular", "sierra circular", "sierra caladora",
        "lijadora", "compresor", "hidrolavadora", "soldadora", "set herramientas", "llave", "alicate",
    ];

    protected override ListingSelectors DefaultSelectors { get; } = new()
    {
        Card = "[data-pod='catalyst-pod'], div.pod",
        Title = ".pod-subTitle, b.pod-subTitle",
        Brand = ".pod-title",
        Link = "a",
        Price = "[data-internet-price], [data-event-price], .prices-0 span",
        ListPrice = "[data-normal-price]",
        Image = "img",
        SkuAttribute = "data-key",
        WaitFor = "#__NEXT_DATA__",
    };

    protected override string BuildPageUrl(string query, int page) =>
        new Uri(BaseUri, $"/sodimac-cl/search?Ntt={Uri.EscapeDataString(query)}&page={page}").ToString();

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

    /// <summary>Lee <c>props.pageProps.results[]</c> del JSON que embebe Next.js.</summary>
    public IReadOnlyList<ScrapedProduct> ParseNextData(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("props", out var props)
            || !props.TryGetProperty("pageProps", out var pageProps)
            || !pageProps.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array)
            return [];

        var products = new List<ScrapedProduct>();
        foreach (var r in results.EnumerateArray())
        {
            var id = Str(r, "skuId") ?? Str(r, "productId");
            var title = Str(r, "displayName");
            var url = Str(r, "url");
            if (id is null || title is null || url is null) continue;

            decimal? price = null, listPrice = null;
            if (r.TryGetProperty("prices", out var prices) && prices.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in prices.EnumerateArray())
                {
                    var value = p.TryGetProperty("price", out var arr) && arr.ValueKind == JsonValueKind.Array
                        ? TextNormalizer.ParseClpPrice(arr.EnumerateArray().FirstOrDefault().GetString())
                        : null;
                    if (value is null) continue;
                    switch (Str(p, "type"))
                    {
                        case "normalPrice": listPrice = value; break;
                        // Precio con tarjeta CMR no se considera: no lo paga cualquier cliente.
                        case "cmrPrice": break;
                        default: price = price is null ? value : Math.Min(price.Value, value.Value); break;
                    }
                }
            }
            price ??= listPrice;
            if (price is null) continue;

            string? image = null;
            if (r.TryGetProperty("mediaUrls", out var media) && media.ValueKind == JsonValueKind.Array)
                image = media.EnumerateArray().Select(m => m.GetString()).FirstOrDefault(m => !string.IsNullOrEmpty(m));

            products.Add(new ScrapedProduct
            {
                ExternalId = id,
                Title = title,
                Url = new Uri(BaseUri, url).ToString(),
                Price = price.Value,
                ListPrice = listPrice,
                Brand = Str(r, "brand"),
                ImageUrl = image,
                IsAvailable = true,
            });
        }
        return products;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;
}
