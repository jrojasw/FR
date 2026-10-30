namespace PrecioHerramientas.Core.Services;

public sealed class IngestionStats
{
    public int ItemsFound { get; set; }
    public int OffersCreated { get; set; }
    public int ProductsCreated { get; set; }
    public int PricesChanged { get; set; }
    public int ItemsRejected { get; set; }
    public int OffersMarkedUnavailable { get; set; }
}
