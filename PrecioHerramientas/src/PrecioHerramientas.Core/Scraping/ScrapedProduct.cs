namespace PrecioHerramientas.Core.Scraping;

/// <summary>Producto tal como lo entrega un scraper, antes de normalizar y agrupar.</summary>
public sealed record ScrapedProduct
{
    public required string ExternalId { get; init; }
    public required string Title { get; init; }
    public required string Url { get; init; }
    public required decimal Price { get; init; }
    public decimal? ListPrice { get; init; }
    public bool IsAvailable { get; init; } = true;
    public string? Brand { get; init; }
    public string? Model { get; init; }
    public string? Ean { get; init; }
    public string? Category { get; init; }
    public string? ImageUrl { get; init; }
}

/// <summary>Datos estáticos de la tienda que maneja un scraper.</summary>
public sealed record StoreDescriptor(string Key, string Name, string BaseUrl, string? LogoUrl = null);
