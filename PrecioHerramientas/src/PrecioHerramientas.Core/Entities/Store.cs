namespace PrecioHerramientas.Core.Entities;

/// <summary>Tienda online de la que se recolectan precios.</summary>
public class Store
{
    public int Id { get; set; }

    /// <summary>Clave estable que identifica al scraper (p. ej. "easy").</summary>
    public required string Key { get; set; }

    public required string Name { get; set; }
    public required string BaseUrl { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Offer> Offers { get; set; } = new List<Offer>();
    public ICollection<ScrapeRun> ScrapeRuns { get; set; } = new List<ScrapeRun>();
}
