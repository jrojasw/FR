using System.Globalization;
using System.Text.Json;
using AngleSharp.Dom;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Core.Text;

namespace PrecioHerramientas.Scrapers.Parsing;

/// <summary>
/// Lee productos desde los bloques <c>application/ld+json</c> (schema.org Product /
/// ItemList) que muchas tiendas incluyen por SEO. Suele ser más estable que los
/// selectores CSS porque Google exige ese formato.
/// </summary>
public static class JsonLdProductExtractor
{
    public static IReadOnlyList<ScrapedProduct> Extract(IDocument document, Uri baseUri)
    {
        var results = new List<ScrapedProduct>();
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            try
            {
                using var json = JsonDocument.Parse(script.TextContent, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                Visit(json.RootElement, baseUri, results);
            }
            catch (JsonException)
            {
                // Algunas tiendas publican JSON-LD inválido; se ignora ese bloque.
            }
        }
        return results;
    }

    private static void Visit(JsonElement element, Uri baseUri, List<ScrapedProduct> results)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray()) Visit(child, baseUri, results);
                return;
            case JsonValueKind.Object:
                break;
            default:
                return;
        }

        if (IsType(element, "Product") && TryReadProduct(element, baseUri) is { } product)
        {
            results.Add(product);
            return;
        }

        foreach (var key in new[] { "@graph", "itemListElement", "item", "mainEntity" })
        {
            if (element.TryGetProperty(key, out var child)) Visit(child, baseUri, results);
        }
    }

    private static ScrapedProduct? TryReadProduct(JsonElement product, Uri baseUri)
    {
        var name = GetString(product, "name");
        var url = GetString(product, "url") ?? GetString(product, "@id");
        if (name is null || url is null) return null;

        if (!product.TryGetProperty("offers", out var offers)) return null;
        // "offers" puede ser un Offer, un AggregateOffer o un arreglo de Offer.
        var offer = offers.ValueKind == JsonValueKind.Array ? offers.EnumerateArray().FirstOrDefault() : offers;
        if (offer.ValueKind != JsonValueKind.Object) return null;

        var price = GetDecimal(offer, "price") ?? GetDecimal(offer, "lowPrice");
        if (price is null) return null;

        var availability = GetString(offer, "availability");
        var absoluteUrl = new Uri(baseUri, url).ToString();
        var sku = GetString(product, "sku") ?? GetString(product, "productID");

        return new ScrapedProduct
        {
            ExternalId = sku ?? new Uri(absoluteUrl).AbsolutePath.Trim('/'),
            Title = TextNormalizer.CleanWhitespace(name),
            Url = absoluteUrl,
            Price = price.Value,
            ListPrice = GetDecimal(offer, "highPrice"),
            IsAvailable = availability is null || availability.Contains("InStock", StringComparison.OrdinalIgnoreCase),
            Brand = product.TryGetProperty("brand", out var brand)
                ? brand.ValueKind == JsonValueKind.Object ? GetString(brand, "name") : brand.ValueKind == JsonValueKind.String ? brand.GetString() : null
                : null,
            Model = GetString(product, "mpn") ?? GetString(product, "model"),
            Ean = GetString(product, "gtin13") ?? GetString(product, "gtin") ?? GetString(product, "gtin12") ?? GetString(product, "gtin8"),
            Category = GetString(product, "category"),
            ImageUrl = ReadImage(product, baseUri),
        };
    }

    private static string? ReadImage(JsonElement product, Uri baseUri)
    {
        if (!product.TryGetProperty("image", out var image)) return null;
        var value = image.ValueKind switch
        {
            JsonValueKind.String => image.GetString(),
            JsonValueKind.Array => image.EnumerateArray().Select(i => i.ValueKind == JsonValueKind.String ? i.GetString() : GetString(i, "url")).FirstOrDefault(),
            JsonValueKind.Object => GetString(image, "url"),
            _ => null,
        };
        return value is null ? null : new Uri(baseUri, value).ToString();
    }

    private static bool IsType(JsonElement element, string type)
    {
        if (!element.TryGetProperty("@type", out var value)) return false;
        return value.ValueKind switch
        {
            JsonValueKind.String => string.Equals(value.GetString(), type, StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Array => value.EnumerateArray().Any(v => string.Equals(v.GetString(), type, StringComparison.OrdinalIgnoreCase)),
            _ => false,
        };
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value)) return null;
        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static decimal? GetDecimal(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number > 0 ? number : null;
        if (value.ValueKind != JsonValueKind.String) return null;

        // En JSON-LD el precio suele venir como "12990" o "12990.00" (punto decimal).
        var text = value.GetString();
        if (decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
            return parsed;
        return TextNormalizer.ParseClpPrice(text);
    }
}
