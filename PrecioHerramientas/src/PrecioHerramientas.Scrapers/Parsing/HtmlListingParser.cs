using AngleSharp.Dom;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Core.Text;

namespace PrecioHerramientas.Scrapers.Parsing;

/// <summary>Selectores CSS que describen la grilla de productos de una tienda.</summary>
public sealed record ListingSelectors
{
    public const string CardKey = "Card", TitleKey = "Title", LinkKey = "Link", PriceKey = "Price", ListPriceKey = "ListPrice",
        ImageKey = "Image", BrandKey = "Brand", SkuKey = "Sku", SkuAttributeKey = "SkuAttribute", OutOfStockKey = "OutOfStock",
        WaitForKey = "WaitFor";

    public required string Card { get; init; }
    public required string Title { get; init; }
    public required string Link { get; init; }
    public required string Price { get; init; }
    public string? ListPrice { get; init; }
    public string? Image { get; init; }
    public string? Brand { get; init; }

    /// <summary>Elemento (dentro de la tarjeta, o la tarjeta misma si es vacío) que trae el SKU.</summary>
    public string? Sku { get; init; }

    /// <summary>Atributo con el SKU, p. ej. <c>data-sku</c>. Si no se encuentra, se usa la ruta de la URL.</summary>
    public string? SkuAttribute { get; init; }

    /// <summary>Si este selector existe dentro de la tarjeta, el producto se considera agotado.</summary>
    public string? OutOfStock { get; init; }

    /// <summary>Selector a esperar cuando se usa navegador.</summary>
    public string? WaitFor { get; init; }

    /// <summary>Aplica los selectores definidos en configuración sobre los valores por defecto.</summary>
    public ListingSelectors WithOverrides(IReadOnlyDictionary<string, string> overrides)
    {
        string? Get(string key, string? fallback) => overrides.TryGetValue(key, out var value) ? NullIfEmpty(value) : fallback;
        return this with
        {
            Card = Get(CardKey, Card)!,
            Title = Get(TitleKey, Title)!,
            Link = Get(LinkKey, Link)!,
            Price = Get(PriceKey, Price)!,
            ListPrice = Get(ListPriceKey, ListPrice),
            Image = Get(ImageKey, Image),
            Brand = Get(BrandKey, Brand),
            Sku = Get(SkuKey, Sku),
            SkuAttribute = Get(SkuAttributeKey, SkuAttribute),
            OutOfStock = Get(OutOfStockKey, OutOfStock),
            WaitFor = Get(WaitForKey, WaitFor),
        };
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>Convierte una página de listado (HTML) en productos usando <see cref="ListingSelectors"/>.</summary>
public static class HtmlListingParser
{
    public static IReadOnlyList<ScrapedProduct> Parse(IDocument document, Uri baseUri, ListingSelectors selectors, string? category = null)
    {
        var results = new List<ScrapedProduct>();
        foreach (var card in document.QuerySelectorAll(selectors.Card))
        {
            var title = Text(card, selectors.Title);
            var href = Attr(card, selectors.Link, "href");
            var price = Price(card, selectors.Price);
            if (title is null || href is null || price is null) continue;

            var url = new Uri(baseUri, href);
            var image = selectors.Image is null ? null : Attr(card, selectors.Image, "data-src") ?? Attr(card, selectors.Image, "src");

            results.Add(new ScrapedProduct
            {
                ExternalId = ReadSku(card, selectors) ?? url.AbsolutePath.Trim('/'),
                Title = TextNormalizer.CleanWhitespace(title),
                Url = url.GetLeftPart(UriPartial.Path),
                Price = price.Value,
                ListPrice = selectors.ListPrice is null ? null : Price(card, selectors.ListPrice),
                IsAvailable = selectors.OutOfStock is null || card.QuerySelector(selectors.OutOfStock) is null,
                Brand = selectors.Brand is null ? null : Text(card, selectors.Brand),
                Category = category,
                ImageUrl = image is null || image.StartsWith("data:", StringComparison.Ordinal) ? null : new Uri(baseUri, image).ToString(),
            });
        }
        return results;
    }

    private static string? ReadSku(IElement card, ListingSelectors selectors)
    {
        if (selectors.SkuAttribute is null) return null;
        var element = string.IsNullOrEmpty(selectors.Sku) ? card : First(card, selectors.Sku);
        var value = element?.GetAttribute(selectors.SkuAttribute);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? Text(IElement card, string selector)
    {
        var text = First(card, selector)?.TextContent;
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static string? Attr(IElement card, string selector, string attribute)
    {
        // Permite que la propia tarjeta sea el enlace (selector ":scope").
        var element = selector == ":scope" ? card : First(card, selector);
        var value = element?.GetAttribute(attribute);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Con una lista "a, b, c" prueba cada alternativa en orden y devuelve la primera que
    /// exista. (QuerySelector con la lista completa devolvería la primera del documento,
    /// que puede ser p. ej. el precio tachado en vez del precio final.)
    /// </summary>
    private static IElement? First(IElement card, string selectorList)
    {
        foreach (var selector in selectorList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (card.QuerySelector(selector) is { } element) return element;
        }
        return null;
    }

    // Prefiere atributos de máquina (content, data-price) por sobre el texto visible.
    private static decimal? Price(IElement card, string selector)
    {
        var element = First(card, selector);
        if (element is null) return null;

        // Atributos de máquina con punto decimal: data-price-amount="12990.0000" (Magento),
        // content="12990.00" (schema.org). ParseClpPrice tomaría ese punto como de miles.
        foreach (var attribute in new[] { "data-price-amount", "content", "data-price" })
        {
            if (element.GetAttribute(attribute) is { Length: > 0 } raw)
            {
                if (decimal.TryParse(raw, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var value)
                    && !raw.Contains(','))
                    return value > 0 ? value : null;
                return TextNormalizer.ParseClpPrice(raw);
            }
        }
        return TextNormalizer.ParseClpPrice(element.TextContent);
    }
}
