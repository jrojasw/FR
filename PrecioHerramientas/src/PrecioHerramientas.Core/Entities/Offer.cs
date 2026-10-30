namespace PrecioHerramientas.Core.Entities;

/// <summary>Publicación de un producto en una tienda concreta.</summary>
public class Offer
{
    public int Id { get; set; }

    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>Identificador del producto en la tienda (SKU, id interno).</summary>
    public required string ExternalId { get; set; }

    public required string Url { get; set; }
    public required string Title { get; set; }
    public string? BrandRaw { get; set; }
    public string? ModelRaw { get; set; }
    public string? Ean { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>Precio que paga el cliente hoy (oferta incluida).</summary>
    public decimal Price { get; set; }

    /// <summary>Precio normal/lista, si la tienda lo informa.</summary>
    public decimal? ListPrice { get; set; }

    /// <summary>Precio anterior al último cambio; sirve para destacar bajas de precio.</summary>
    public decimal? PreviousPrice { get; set; }

    public bool IsAvailable { get; set; }

    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime LastPriceChangeAt { get; set; }

    public ICollection<PricePoint> PriceHistory { get; set; } = new List<PricePoint>();
}
