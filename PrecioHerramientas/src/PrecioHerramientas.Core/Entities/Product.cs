namespace PrecioHerramientas.Core.Entities;

/// <summary>
/// Producto canónico: agrupa las publicaciones (<see cref="Offer"/>) del mismo
/// artículo en distintas tiendas.
/// </summary>
public class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }

    public int? BrandId { get; set; }
    public Brand? Brand { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>Modelo / número de parte normalizado (mayúsculas, sin separadores).</summary>
    public string? Model { get; set; }

    /// <summary>Código de barras EAN-13/GTIN normalizado a 13 dígitos.</summary>
    public string? Ean { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>Texto normalizado (minúsculas, sin tildes) para búsqueda.</summary>
    public string SearchText { get; set; } = "";

    // Datos desnormalizados para listar y ordenar sin recorrer las ofertas.
    public decimal? BestPrice { get; set; }
    public int OfferCount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Offer> Offers { get; set; } = new List<Offer>();
}
