using System.Text.Json;
using System.Text.RegularExpressions;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Core.Text;

namespace PrecioHerramientas.Scrapers.Parsing;

/// <summary>
/// Lee los resultados de búsqueda de Easy desde el JSON de Next.js: el
/// <c>&lt;script id="__NEXT_DATA__"&gt;</c> de <c>/search/{término}</c> (que trae el
/// <c>buildId</c>) o la respuesta de <c>/_next/data/{buildId}/search/{término}.json</c>.
/// Los productos están en <c>pageProps.serverProductsResponse.productList[]</c> y el total
/// en <c>recordsFiltered</c>.
/// </summary>
public static partial class EasyNextDataParser
{
    public sealed record Result(IReadOnlyList<ScrapedProduct> Products, int? Total, string? BuildId);

    public static Result Parse(string json, Uri baseUri)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var buildId = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("buildId", out var b) && b.ValueKind == JsonValueKind.String
            ? b.GetString()
            : null;

        if (Find(root, "serverProductsResponse", 0) is not { ValueKind: JsonValueKind.Object } response)
            return new Result([], null, buildId);

        int? total = response.TryGetProperty("recordsFiltered", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : null;
        var products = new List<ScrapedProduct>();
        if (response.TryGetProperty("productList", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray())
            {
                if (ReadProduct(item, baseUri) is { } product) products.Add(product);
            }
        }
        return new Result(products, total, buildId);
    }

    /// <summary>Contenido del <c>&lt;script id="__NEXT_DATA__"&gt;</c> de una página, o null.</summary>
    public static string? ExtractNextDataJson(string html)
    {
        var match = NextDataScript().Match(html);
        return match.Success ? match.Groups["json"].Value : null;
    }

    private static ScrapedProduct? ReadProduct(JsonElement item, Uri baseUri)
    {
        if (item.ValueKind != JsonValueKind.Object) return null;
        var name = Str(item, "productName");
        var id = Str(item, "sku") ?? Str(item, "productId");
        var linkText = Str(item, "linkText");
        if (name is null || id is null || linkText is null) return null;

        decimal? normal = null, offer = null;
        if (item.TryGetProperty("prices", out var prices) && prices.ValueKind == JsonValueKind.Object)
        {
            normal = Money(prices, "normalPrice");
            offer = Money(prices, "offerPrice");
        }
        var price = offer is > 0 ? offer : normal;
        if (price is not > 0) return null;

        var url = linkText.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? linkText
            : new Uri(baseUri, "/" + linkText.TrimStart('/')).AbsoluteUri;

        return new ScrapedProduct
        {
            ExternalId = id,
            Title = name,
            Url = url,
            Price = price.Value,
            ListPrice = normal > price ? normal : null,
            IsAvailable = !(item.TryGetProperty("availableQuantity", out var qty) && qty.ValueKind == JsonValueKind.Number && qty.GetDecimal() <= 0),
            Brand = Str(item, "brand"),
            Model = Str(item, "productReference") ?? Str(item, "model"),
            Ean = Str(item, "ean"),
            ImageUrl = Image(item),
        };
    }

    private static string? Image(JsonElement item)
    {
        if (!item.TryGetProperty("imageUrl", out var image)) return null;
        return image.ValueKind switch
        {
            JsonValueKind.String => image.GetString(),
            JsonValueKind.Array => image.EnumerateArray().Where(i => i.ValueKind == JsonValueKind.String).Select(i => i.GetString()).FirstOrDefault(),
            _ => null,
        };
    }

    /// <summary>Precio como número (12990) o texto chileno ("$12.990", "12.990").</summary>
    private static decimal? Money(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDecimal() is var n && n > 0 ? n : null,
            JsonValueKind.String => TextNormalizer.ParseClpPrice(v.GetString()),
            _ => null,
        };
    }

    private static JsonElement? Find(JsonElement element, string name, int depth)
    {
        if (depth > 8) return null;
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(name, out var found)) return found;
            foreach (var property in element.EnumerateObject())
            {
                if (Find(property.Value, name, depth + 1) is { } nested) return nested;
            }
        }
        return null;
    }

    private static string? Str(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        var text = v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        };
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    [GeneratedRegex("""<script[^>]*\bid=["']__NEXT_DATA__["'][^>]*>(?<json>.*?)</script>""", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex NextDataScript();
}
