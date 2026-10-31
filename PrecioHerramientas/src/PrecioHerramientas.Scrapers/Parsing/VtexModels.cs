using System.Text.Json.Serialization;
using PrecioHerramientas.Core.Scraping;

namespace PrecioHerramientas.Scrapers.Parsing;

/// <summary>
/// Convierte productos VTEX en <see cref="ScrapedProduct"/>. La forma del producto es la
/// misma en la API de catálogo, en Intelligent Search y en la consulta GraphQL
/// <c>productSearch</c> de VTEX IO.
/// </summary>
public static class VtexProductMapper
{
    public static IEnumerable<ScrapedProduct> Map(VtexProduct product, Uri baseUri)
    {
        if (product.Items is null) yield break;
        // La URL pública se arma con linkText: "link" puede apuntar al dominio interno de VTEX.
        var url = !string.IsNullOrWhiteSpace(product.LinkText)
            ? new Uri(baseUri, $"/{product.LinkText}/p").AbsoluteUri
            : !string.IsNullOrWhiteSpace(product.Link) ? new Uri(baseUri, new Uri(baseUri, product.Link).PathAndQuery).AbsoluteUri : null;
        if (url is null) yield break;

        var category = product.Categories?.FirstOrDefault()?.Trim('/');
        foreach (var item in product.Items)
        {
            var offer = item.Sellers?.FirstOrDefault(s => s.SellerDefault)?.CommertialOffer
                ?? item.Sellers?.FirstOrDefault()?.CommertialOffer;
            if (offer is null || offer.Price is not > 0 || string.IsNullOrWhiteSpace(item.ItemId)) continue;

            // Productos con variantes (p. ej. tamaños) traen un ítem por SKU.
            var title = product.Items.Count > 1 && !string.IsNullOrWhiteSpace(item.NameComplete)
                ? item.NameComplete!
                : product.ProductName ?? item.NameComplete ?? "";

            yield return new ScrapedProduct
            {
                ExternalId = item.ItemId!,
                Title = title,
                Url = url,
                Price = offer.Price.Value,
                ListPrice = offer.ListPrice is > 0 ? offer.ListPrice : offer.PriceWithoutDiscount,
                IsAvailable = offer.IsAvailable ?? offer.AvailableQuantity > 0,
                Brand = product.Brand,
                Model = product.ProductReference,
                Ean = item.Ean,
                Category = category,
                ImageUrl = item.Images?.FirstOrDefault()?.ImageUrl,
            };
        }
    }
}

public sealed class VtexProduct
{
    public string? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? Brand { get; set; }
    public string? ProductReference { get; set; }
    public string? Link { get; set; }
    public string? LinkText { get; set; }
    public List<string>? Categories { get; set; }
    public List<VtexItem>? Items { get; set; }
}

public sealed class VtexItem
{
    public string? ItemId { get; set; }
    public string? NameComplete { get; set; }
    public string? Ean { get; set; }
    public List<VtexImage>? Images { get; set; }
    public List<VtexSeller>? Sellers { get; set; }
}

public sealed class VtexImage
{
    public string? ImageUrl { get; set; }
}

public sealed class VtexSeller
{
    public bool SellerDefault { get; set; }
    public VtexCommercialOffer? CommertialOffer { get; set; }
}

public sealed class VtexCommercialOffer
{
    public decimal? Price { get; set; }
    public decimal? ListPrice { get; set; }
    public decimal? PriceWithoutDiscount { get; set; }
    public int AvailableQuantity { get; set; }

    [JsonPropertyName("IsAvailable")]
    public bool? IsAvailable { get; set; }
}
