namespace PrecioHerramientas.Core.Queries;

public sealed record StoreClicks(string StoreKey, string StoreName, int Clicks, DateTime? LastClickAt);

public sealed record ProductClicks(int ProductId, string Slug, string Name, int Clicks, DateTime LastClickAt);

public sealed record ClickSummary(int Total, IReadOnlyList<StoreClicks> ByStore, IReadOnlyList<ProductClicks> TopProducts);

/// <summary>Reportes de clics para el panel de administración.</summary>
public interface IClickReports
{
    /// <param name="since">Solo clics desde esta fecha (UTC); null = todos.</param>
    Task<ClickSummary> GetSummaryAsync(DateTime? since, int topProducts, CancellationToken ct = default);
}
