namespace PrecioHerramientas.Core.Services;

public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    /// <summary>Cantidad de productos que se procesan antes de guardar en la base de datos.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Al terminar una ejecución, las ofertas no vistas se marcan como no disponibles
    /// solo si la ejecución encontró al menos esta fracción de las ofertas disponibles
    /// previas. Evita "vaciar" una tienda cuando el scraper se rompe a medias.
    /// </summary>
    public double MinCoverageToMarkUnavailable { get; set; } = 0.5;
}
