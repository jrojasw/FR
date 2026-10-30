namespace PrecioHerramientas.Core.Entities;

public enum ScrapeRunStatus
{
    Running = 0,
    Succeeded = 1,
    Failed = 2,
}

/// <summary>Bitácora de cada ejecución de un scraper.</summary>
public class ScrapeRun
{
    public long Id { get; set; }

    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;

    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public ScrapeRunStatus Status { get; set; }

    public int ItemsFound { get; set; }
    public int OffersCreated { get; set; }
    public int PricesChanged { get; set; }
    public int ItemsRejected { get; set; }
    public int OffersMarkedUnavailable { get; set; }

    public string? Error { get; set; }
}
