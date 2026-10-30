namespace PrecioHerramientas.Core.Entities;

/// <summary>Registro histórico del precio de una oferta.</summary>
public class PricePoint
{
    public long Id { get; set; }

    public int OfferId { get; set; }
    public Offer Offer { get; set; } = null!;

    public decimal Price { get; set; }
    public decimal? ListPrice { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CapturedAt { get; set; }
}
