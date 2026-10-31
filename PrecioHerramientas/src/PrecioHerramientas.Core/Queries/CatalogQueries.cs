namespace PrecioHerramientas.Core.Queries;

public enum ProductSort
{
    Relevance,
    PriceAsc,
    PriceDesc,
    Name,
    Newest,
}

public sealed record ProductSearchQuery
{
    public string? Text { get; init; }
    public string? BrandSlug { get; init; }
    public string? CategorySlug { get; init; }
    public string? StoreKey { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public bool OnlyAvailable { get; init; } = true;
    public ProductSort Sort { get; init; } = ProductSort.Relevance;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 24;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record ProductListItem(
    int Id, string Slug, string Name, string? BrandName, string? ImageUrl, decimal? BestPrice, int OfferCount);

public sealed record FacetItem(string Slug, string Name, int Count);

public sealed record OfferView(
    string StoreKey, string StoreName, string? StoreLogoUrl, string Url, string Title,
    decimal Price, decimal? ListPrice, bool IsAvailable, DateTime LastSeenAt, DateTime LastPriceChangeAt,
    string? BranchName = null)
{
    /// <summary>Descuento sobre el precio normal; no se informa si es menor a 1 %.</summary>
    public int? DiscountPercent => ListPrice is { } list && list > Price && (int)Math.Round((list - Price) / list * 100) is var pct and >= 1
        ? pct
        : null;
}

public sealed record ProductDetail(
    int Id, string Slug, string Name, string? BrandName, string? BrandSlug, string? CategoryName, string? CategorySlug,
    string? Model, string? Ean, string? ImageUrl, IReadOnlyList<OfferView> Offers,
    decimal? HistoricalMinPrice, DateTime? HistoricalMinDate);

public sealed record PricePointView(DateTime Date, decimal Price, bool IsAvailable);

public sealed record PriceSeries(string StoreKey, string StoreName, IReadOnlyList<PricePointView> Points);

public sealed record PriceDrop(
    string ProductSlug, string ProductName, string? ImageUrl, string StoreName,
    decimal Price, decimal PreviousPrice, DateTime ChangedAt)
{
    public int DropPercent => PreviousPrice > 0 ? (int)Math.Round((PreviousPrice - Price) / PreviousPrice * 100) : 0;
}

public sealed record StoreOverview(
    string Key, string Name, string BaseUrl, string? LogoUrl, bool IsActive, int AvailableOffers,
    DateTime? LastRunAt, string? LastRunStatus, int? LastRunItems);

/// <summary>Consultas de solo lectura que usa el sitio público.</summary>
public interface ICatalogQueries
{
    Task<PagedResult<ProductListItem>> SearchAsync(ProductSearchQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<FacetItem>> GetBrandsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<FacetItem>> GetCategoriesAsync(CancellationToken ct = default);
    Task<ProductDetail?> GetProductAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<PriceSeries>> GetPriceHistoryAsync(int productId, int days, CancellationToken ct = default);
    Task<IReadOnlyList<PriceDrop>> GetRecentPriceDropsAsync(int take, int withinDays = 7, CancellationToken ct = default);
    Task<IReadOnlyList<StoreOverview>> GetStoresAsync(CancellationToken ct = default);
}
