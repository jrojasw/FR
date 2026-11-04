using System.Text.Json;
using System.Text.RegularExpressions;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Core.Text;

namespace PrecioHerramientas.Scrapers.Parsing;

/// <summary>
/// Lee productos del JSON que embeben en <c>&lt;script id="__NEXT_DATA__"&gt;</c> los sitios
/// Next.js de la plataforma Falabella (Sodimac y tiendas del grupo).
/// </summary>
public static partial class NextDataProductParser
{
    /// <summary>
    /// Lee los productos del JSON de Next.js. Se esperan en <c>props.pageProps.results</c>,
    /// pero se buscan en cualquier arreglo <c>results</c> bajo <c>props</c> cuyos elementos
    /// tengan <c>displayName</c>, por si la tienda reorganiza la estructura.
    /// </summary>
    public static IReadOnlyList<ScrapedProduct> Parse(string json, Uri baseUri)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("props", out var props)) return [];
        var results = FindResults(props, depth: 0);
        if (results is null) return [];

        var products = new List<ScrapedProduct>();
        foreach (var r in results.Value.EnumerateArray())
        {
            if (r.ValueKind != JsonValueKind.Object) continue;
            var title = Str(r, "displayName");
            var rawUrl = Str(r, "url");
            if (title is null || rawUrl is null) continue;
            var url = new Uri(baseUri, rawUrl).GetLeftPart(UriPartial.Path);
            // El id de la publicación sale de la URL, igual que en la grilla HTML de respaldo:
            // así una misma publicación no se guarda dos veces según por dónde se leyó.
            var id = ListingId(url) ?? Str(r, "skuId") ?? Str(r, "productId");
            if (id is null) continue;

            var (price, listPrice) = ReadPrices(r);
            if (price is null) continue;

            products.Add(new ScrapedProduct
            {
                ExternalId = id,
                Title = title,
                Url = url,
                Price = price.Value,
                ListPrice = listPrice,
                Brand = Str(r, "brand"),
                Model = ReadModel(r),
                ImageUrl = FirstString(r, "mediaUrls"),
                IsAvailable = true,
                Seller = ReadSeller(r),
            });
        }
        return products;
    }

    /// <summary>
    /// Id de una publicación de Falabella/Sodimac a partir de su URL:
    /// <c>/product/{productId}/{nombre}/{skuId}</c> → skuId; sin SKU, el productId.
    /// </summary>
    public static string? ListingId(string url)
    {
        var path = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : url.Split('?', '#')[0];
        var match = ProductPath().Match(path);
        if (!match.Success) return null;
        return match.Groups["sku"].Success && match.Groups["sku"].Value.Length > 0 ? match.Groups["sku"].Value : match.Groups["pid"].Value;
    }

    // "sellerName": "IMPERIAL"; en algunas versiones "seller": "..." o { "name": "..." }.
    private static string? ReadSeller(JsonElement r)
    {
        if (Str(r, "sellerName") is { } name) return name;
        if (!r.TryGetProperty("seller", out var seller)) return null;
        return seller.ValueKind switch
        {
            JsonValueKind.String => Str(r, "seller"),
            JsonValueKind.Object => Str(seller, "name") ?? Str(seller, "sellerName"),
            _ => null,
        };
    }

    [GeneratedRegex(@"/product/(?<pid>[^/?#]+)(?:/[^/?#]+(?:/(?<sku>[^/?#]+))?)?/?$")]
    private static partial Regex ProductPath();

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
