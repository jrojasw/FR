using System.Text.Json;
using System.Text.RegularExpressions;
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
/// Sodimac Chile (plataforma Next.js de Falabella). La página de búsqueda
/// <c>/sodimac-cl/buscar?Ntt=...</c> trae los resultados como JSON en
/// <c>&lt;script id="__NEXT_DATA__"&gt;</c> (<c>props.pageProps.results</c>), que es
/// más estable que la grilla HTML. Primero se pide por HTTP; si la tienda bloquea
/// la solicitud o no entrega ese JSON, se pasa a Chromium (Playwright).
/// Si nada de eso funciona, se recurre a los selectores CSS y a JSON-LD.
/// </summary>
public sealed partial class SodimacScraper(IServiceProvider services, IOptions<ScraperOptions> options, ILogger<SodimacScraper> logger)
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

    /// <summary>
    /// Lee los productos del JSON de Next.js. Se esperan en <c>props.pageProps.results</c>,
    /// pero se buscan en cualquier arreglo <c>results</c> bajo <c>props</c> cuyos elementos
    /// tengan <c>displayName</c>, por si la tienda reorganiza la estructura.
    /// </summary>
    public IReadOnlyList<ScrapedProduct> ParseNextData(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("props", out var props)) return [];
        var results = FindResults(props, depth: 0);
        if (results is null) return [];

        var products = new List<ScrapedProduct>();
        foreach (var r in results.Value.EnumerateArray())
        {
            if (r.ValueKind != JsonValueKind.Object) continue;
            var id = Str(r, "skuId") ?? Str(r, "productId");
            var title = Str(r, "displayName");
            var url = Str(r, "url");
            if (id is null || title is null || url is null) continue;

            var (price, listPrice) = ReadPrices(r);
            if (price is null) continue;

            products.Add(new ScrapedProduct
            {
                ExternalId = id,
                Title = title,
                Url = new Uri(BaseUri, url).ToString(),
                Price = price.Value,
                ListPrice = listPrice,
                Brand = Str(r, "brand"),
                Model = ReadModel(r),
                ImageUrl = FirstString(r, "mediaUrls"),
                IsAvailable = true,
            });
        }
        return products;
    }

    private static JsonElement? FindResults(JsonElement element, int depth)
    {
        if (depth > 6 || element.ValueKind != JsonValueKind.Object) return null;
        if (element.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array
            && results.EnumerateArray().Any(r => r.ValueKind == JsonValueKind.Object && r.TryGetProperty("displayName", out _)))
            return results;

        foreach (var property in element.EnumerateObject())
        {
            if (FindResults(property.Value, depth + 1) is { } found) return found;
        }
        return null;
    }

    /// <summary>
    /// <c>prices</c> es una lista de { type, price: ["43.989"], crossed }. El precio tachado
    /// (crossed) o "normalPrice" es el precio normal; el de tarjeta CMR se ignora porque no
    /// lo paga cualquier cliente; del resto (internet, evento) se toma el menor. Si
    /// <c>price</c> trae dos valores (rango por variantes) se usa el menor.
    /// </summary>
    private static (decimal? Price, decimal? ListPrice) ReadPrices(JsonElement r)
    {
        decimal? price = null, listPrice = null;
        if (!r.TryGetProperty("prices", out var prices) || prices.ValueKind != JsonValueKind.Array)
            return (null, null);

        foreach (var p in prices.EnumerateArray())
        {
            if (p.ValueKind != JsonValueKind.Object || !p.TryGetProperty("price", out var values)) continue;
            var value = (values.ValueKind == JsonValueKind.Array ? values.EnumerateArray() : Enumerable.Repeat(values, 1))
                .Select(v => v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : TextNormalizer.ParseClpPrice(v.GetString()))
                .Where(v => v is > 0)
                .Min();
            if (value is null) continue;

            var type = Str(p, "type") ?? "";
            var crossed = p.TryGetProperty("crossed", out var c) && c.ValueKind == JsonValueKind.True;
            if (crossed || type.Equals("normalPrice", StringComparison.OrdinalIgnoreCase))
                listPrice = listPrice is null ? value : Math.Max(listPrice.Value, value.Value);
            else if (!type.Contains("cmr", StringComparison.OrdinalIgnoreCase))
                price = price is null ? value : Math.Min(price.Value, value.Value);
        }

        // Si solo viene el precio normal, ese es el que se paga.
        if (price is null && listPrice is not null)
            (price, listPrice) = (listPrice, null);
        return (price, listPrice);
    }

    // "topSpecifications" suele traer textos como "Modelo: DCD778D2".
    private static string? ReadModel(JsonElement r)
    {
        if (!r.TryGetProperty("topSpecifications", out var specs) || specs.ValueKind != JsonValueKind.Array) return null;
        foreach (var spec in specs.EnumerateArray())
        {
            if (spec.ValueKind != JsonValueKind.String) continue;
            var match = ModelSpec().Match(spec.GetString()!);
            if (match.Success) return match.Groups["model"].Value.Trim();
        }
        return null;
    }

    private static string? FirstString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Where(m => m.ValueKind == JsonValueKind.String).Select(m => m.GetString()).FirstOrDefault(m => !string.IsNullOrEmpty(m))
            : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;

    [GeneratedRegex(@"^\s*(?:modelo|model)\s*:\s*(?<model>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ModelSpec();
}
