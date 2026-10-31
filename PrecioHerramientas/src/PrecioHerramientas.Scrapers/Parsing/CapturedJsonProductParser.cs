using System.Globalization;
using System.Text.Json;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Core.Text;

namespace PrecioHerramientas.Scrapers.Parsing;

/// <summary>
/// Reconoce productos en las respuestas JSON que una tienda pide desde el navegador,
/// según la forma del JSON (no la URL, que cambia más seguido):
/// <list type="bullet">
/// <item>VTEX: arreglos de productos con <c>productName</c> e <c>items</c> (API de catálogo,
/// Intelligent Search <c>{ products: [...] }</c> y GraphQL <c>data.productSearch.products</c>).</item>
/// <item>Oracle Commerce Cloud: búsqueda guiada (<c>resultsList.records[].attributes</c> con
/// claves "product.displayName", "sku.activePrice", ...) y <c>/ccstore/v1/products</c>
/// (<c>items[]</c> con displayName, listPrice, salePrice, route).</item>
/// <item>Next.js de la plataforma Falabella (<c>results[]</c> con displayName y prices).</item>
/// </list>
/// </summary>
public static class CapturedJsonProductParser
{
    private static readonly JsonSerializerOptions VtexJson = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<ScrapedProduct> Parse(string json, Uri baseUri)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return [];
        }

        using (doc)
        {
            // Easy (Next.js): pageProps.serverProductsResponse.productList[].
            if (json.Contains("\"serverProductsResponse\"", StringComparison.Ordinal)
                && EasyNextDataParser.Parse(json, baseUri).Products is { Count: > 0 } easy)
                return easy;

            var products = new List<ScrapedProduct>();
            Visit(doc.RootElement, baseUri, products, depth: 0);
            if (products.Count == 0 && doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("props", out _))
                products.AddRange(NextDataProductParser.Parse(json, baseUri));
            return products;
        }
    }

    private static void Visit(JsonElement element, Uri baseUri, List<ScrapedProduct> products, int depth)
    {
        // Las respuestas del "assembler" de Oracle Commerce anidan los registros a bastante profundidad.
        if (depth > 24) return;
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                if (LooksLikeVtexProducts(element))
                {
                    var vtex = element.Deserialize<List<VtexProduct>>(VtexJson) ?? [];
                    products.AddRange(vtex.SelectMany(p => VtexProductMapper.Map(p, baseUri)));
                    return;
                }
                if (LooksLikeOccItems(element))
                {
                    products.AddRange(element.EnumerateArray().SelectMany(i => ReadOccItem(i, baseUri)));
                    return;
                }
                foreach (var child in element.EnumerateArray()) Visit(child, baseUri, products, depth + 1);
                return;

            case JsonValueKind.Object:
                if (element.TryGetProperty("attributes", out var attributes) && attributes.ValueKind == JsonValueKind.Object
                    && attributes.TryGetProperty("product.displayName", out _))
                {
                    if (ReadOccRecord(element, attributes, baseUri) is { } product) products.Add(product);
                    return;
                }
                foreach (var property in element.EnumerateObject()) Visit(property.Value, baseUri, products, depth + 1);
                return;
        }
    }

    private static bool LooksLikeVtexProducts(JsonElement array) =>
        array.EnumerateArray().Any(p => p.ValueKind == JsonValueKind.Object && p.TryGetProperty("productName", out _) && p.TryGetProperty("items", out _));

    private static bool LooksLikeOccItems(JsonElement array) =>
        array.EnumerateArray().Any(p => p.ValueKind == JsonValueKind.Object && p.TryGetProperty("displayName", out _)
            && (p.TryGetProperty("route", out _) || p.TryGetProperty("repositoryId", out _))
            && (p.TryGetProperty("listPrice", out _) || p.TryGetProperty("salePrice", out _) || p.TryGetProperty("listPrices", out _)));

    // ---------- Oracle Commerce Cloud: búsqueda guiada ----------

    private static ScrapedProduct? ReadOccRecord(JsonElement record, JsonElement a, Uri baseUri)
    {
        var name = Attr(a, "product.displayName") ?? PlainText(Attr(a, "product.longDescription"));
        var id = Attr(a, "sku.repositoryId") ?? Attr(a, "product.repositoryId") ?? Attr(a, "product.id");
        var route = OccRoute(record, a);
        if (name is null || id is null || route is null) return null;

        var sale = Number(Attr(a, "sku.activePrice")) ?? Number(Attr(a, "sku.salePrice")) ?? Number(Attr(a, "product.salePrice"));
        var list = Number(Attr(a, "sku.listPrice")) ?? Number(Attr(a, "product.listPrice"));
        var price = sale is > 0 ? sale : list;
        if (price is not > 0) return null;

        var status = Attr(a, "sku.availabilityStatus") ?? Attr(a, "product.availabilityStatus") ?? Attr(a, "sku.stockStatus");
        return new ScrapedProduct
        {
            ExternalId = id,
            Title = name,
            Url = new Uri(baseUri, route).AbsoluteUri,
            Price = price.Value,
            ListPrice = list > price ? list : null,
            IsAvailable = status is null || !status.Contains("OUT", StringComparison.OrdinalIgnoreCase),
            Brand = Attr(a, "product.brand") ?? FindAttr(a, "marca"),
            Model = FindAttr(a, "model"),
            Ean = FindAttr(a, "ean") ?? FindAttr(a, "gtin"),
            ImageUrl = (Attr(a, "product.primaryFullImageURL") ?? Attr(a, "product.primaryThumbImageURL")) is { } image ? AbsoluteImage(baseUri, image) : null,
        };
    }

    /// <summary>
    /// Ruta pública del producto: <c>product.route</c> ("/taladro-x/product/123"); si no
    /// viene, otro atributo "*.route", o la acción de detalle del registro
    /// (<c>detailsAction.recordState</c>), sin los parámetros de navegación de Endeca.
    /// </summary>
    private static string? OccRoute(JsonElement record, JsonElement a)
    {
        var route = Attr(a, "product.route")
            ?? a.EnumerateObject().Where(p => p.Name.EndsWith(".route", StringComparison.OrdinalIgnoreCase)).Select(p => Attr(a, p.Name)).FirstOrDefault(v => v is not null);
        if (route is null && record.TryGetProperty("detailsAction", out var details) && details.ValueKind == JsonValueKind.Object)
            route = Str(details, "recordState");
        if (route is null) return null;
        var query = route.IndexOf('?');
        return query >= 0 ? route[..query] : route;
    }

    private static string? PlainText(string? html) =>
        html is null ? null : System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ") is var text && text.Trim().Length > 0
            ? TextNormalizer.CleanWhitespace(System.Net.WebUtility.HtmlDecode(text))
            : null;

    /// <summary>Los atributos de la búsqueda guiada vienen como arreglos de un string.</summary>
    private static string? Attr(JsonElement a, string key)
    {
        if (!a.TryGetProperty(key, out var v)) return null;
        var value = v.ValueKind switch
        {
            JsonValueKind.Array => v.EnumerateArray().Select(Scalar).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
            _ => Scalar(v),
        };
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Atributos propios de cada tienda ("product.x_modelo", "sku.x_ean"...).</summary>
    private static string? FindAttr(JsonElement a, string fragment) =>
        a.EnumerateObject()
            .Where(p => p.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            .Select(p => Attr(a, p.Name))
            .FirstOrDefault(v => v is not null);

    // ---------- Oracle Commerce Cloud: /ccstore/v1/products ----------

    private static IEnumerable<ScrapedProduct> ReadOccItem(JsonElement item, Uri baseUri)
    {
        if (item.ValueKind != JsonValueKind.Object) yield break;
        var name = Str(item, "displayName");
        var id = Str(item, "repositoryId") ?? Str(item, "id");
        var route = Str(item, "route");
        if (name is null || id is null || route is null) yield break;

        var sale = Price(item, "salePrice", "salePrices");
        var list = Price(item, "listPrice", "listPrices");
        // Si el producto no trae precio, se usa el del primer SKU que lo tenga.
        if (sale is null && list is null && item.TryGetProperty("childSKUs", out var skus) && skus.ValueKind == JsonValueKind.Array)
        {
            foreach (var sku in skus.EnumerateArray())
            {
                sale = Price(sku, "salePrice", "salePrices");
                list = Price(sku, "listPrice", "listPrices");
                if (sale is not null || list is not null) break;
            }
        }
        var price = sale is > 0 ? sale : list;
        if (price is not > 0) yield break;

        var image = Str(item, "primaryFullImageURL") ?? Str(item, "primaryMediumImageURL") ?? Str(item, "primaryThumbImageURL");
        yield return new ScrapedProduct
        {
            ExternalId = id,
            Title = name,
            Url = new Uri(baseUri, route).AbsoluteUri,
            Price = price.Value,
            ListPrice = list > price ? list : null,
            IsAvailable = !(item.TryGetProperty("active", out var active) && active.ValueKind == JsonValueKind.False),
            Brand = Str(item, "brand"),
            Model = Str(item, "x_modelo") ?? Str(item, "x_model"),
            Ean = Str(item, "x_ean") ?? Str(item, "x_gtin"),
            ImageUrl = image is null ? null : AbsoluteImage(baseUri, image),
        };
    }

    private static decimal? Price(JsonElement e, string single, string byGroup)
    {
        if (e.TryGetProperty(single, out var v) && Number(Scalar(v)) is { } value) return value;
        // Con listas de precios: { "clpPriceGroup": 49990 }.
        if (e.TryGetProperty(byGroup, out var groups) && groups.ValueKind == JsonValueKind.Object)
            return groups.EnumerateObject().Select(g => Number(Scalar(g.Value))).FirstOrDefault(x => x is > 0);
        return null;
    }

    // ---------- utilidades ----------

    private static string AbsoluteImage(Uri baseUri, string image) => new Uri(baseUri, image).AbsoluteUri;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && Scalar(v) is { Length: > 0 } s ? s.Trim() : null;

    private static string? Scalar(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString(),
        JsonValueKind.Number => v.GetRawText(),
        _ => null,
    };

    /// <summary>OCC entrega precios como 49990, 49990.0 o "49990.0" (punto decimal, no de miles).</summary>
    private static decimal? Number(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            return value > 0 ? value : null;
        return TextNormalizer.ParseClpPrice(text);
    }
}
