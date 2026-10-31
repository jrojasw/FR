using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Http;

namespace PrecioHerramientas.Scrapers.Platforms;

/// <summary>
/// Tiendas sobre VTEX (Easy y otras): usa la API pública de catálogo
/// <c>/api/catalog_system/pub/products/search</c>, que devuelve JSON con precio,
/// precio normal, stock, marca, referencia y EAN. No requiere navegador.
/// </summary>
public abstract class VtexScraperBase(IHttpClientFactory httpClientFactory, IOptions<ScraperOptions> options, ILogger logger)
    : StoreScraperBase(options, logger)
{
    /// <summary>VTEX entrega como máximo 50 productos por página y no pagina más allá del 2.500.</summary>
    private const int PageSize = 50;
    private const int MaxOffset = 2500;

    /// <summary>
    /// Host de la API de catálogo. Por defecto el de la tienda, pero muchas tiendas VTEX
    /// con frontend propio ya no la exponen en su dominio y solo responde en
    /// <c>https://{cuenta}.vtexcommercestable.com.br</c>. Configurable con <c>ApiBaseUrl</c>.
    /// </summary>
    protected virtual string DefaultApiBaseUrl => Store.BaseUrl;

    protected Uri ApiBaseUri => new(Options.ApiBaseUrl is { Length: > 0 } url ? url : DefaultApiBaseUrl);

    protected override async IAsyncEnumerable<ScrapedProduct> ScrapeQueryAsync(string query, QueryContext context, [EnumeratorCancellation] CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(HttpClientNames.Scraper);
        for (int page = 0; page < Options.MaxPagesPerQuery; page++)
        {
            int from = page * PageSize, to = from + PageSize - 1;
            if (to >= MaxOffset) yield break;

            var url = BuildSearchUrl(query, from, to);
            List<VtexProduct>? products = null;
            using (var response = await client.GetAsync(url, ct))
            {
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
                {
                    if (page == 0) context.Failure = $"HTTP {(int)response.StatusCode} en {url}";
                    yield break;
                }
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                try
                {
                    products = await JsonSerializer.DeserializeAsync<List<VtexProduct>>(stream, JsonOptions, ct);
                }
                catch (JsonException)
                {
                    // Suele ser una página HTML (bloqueo o redirección) en lugar del JSON de VTEX.
                    if (page == 0) context.Failure = $"la respuesta de {url} no es JSON de VTEX";
                }
            }

            if (products is null || products.Count == 0) yield break;
            foreach (var product in products)
            foreach (var item in Map(product))
                yield return item;

            if (products.Count < PageSize) yield break;
            await PoliteDelayAsync(ct);
        }
    }

    /// <summary>
    /// Si la consulta empieza con "/" se interpreta como ruta de categoría
    /// (<c>fq=C:/1/23/</c>); en otro caso, como texto de búsqueda.
    /// </summary>
    protected virtual string BuildSearchUrl(string query, int from, int to)
    {
        var filter = query.StartsWith('/')
            ? $"fq=C:{Uri.EscapeDataString(query)}"
            : $"ft={Uri.EscapeDataString(query)}";
        return new Uri(ApiBaseUri, $"/api/catalog_system/pub/products/search?{filter}&_from={from}&_to={to}").AbsoluteUri;
    }

    public IEnumerable<ScrapedProduct> Map(VtexProduct product)
    {
        if (product.Items is null) yield break;
        // La URL pública se arma con linkText: "link" puede apuntar al dominio interno de VTEX.
        var url = !string.IsNullOrWhiteSpace(product.LinkText)
            ? new Uri(BaseUri, $"/{product.LinkText}/p").AbsoluteUri
            : !string.IsNullOrWhiteSpace(product.Link) ? new Uri(BaseUri, new Uri(BaseUri, product.Link).PathAndQuery).AbsoluteUri : null;
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

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

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
}
