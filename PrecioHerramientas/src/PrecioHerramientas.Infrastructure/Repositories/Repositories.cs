using Microsoft.EntityFrameworkCore;
using PrecioHerramientas.Core.Abstractions;
using PrecioHerramientas.Core.Entities;
using PrecioHerramientas.Infrastructure.Persistence;

namespace PrecioHerramientas.Infrastructure.Repositories;

internal sealed class StoreRepository(AppDbContext db) : IStoreRepository
{
    public Task<Store?> GetByKeyAsync(string key, CancellationToken ct = default) =>
        db.Stores.FirstOrDefaultAsync(s => s.Key == key, ct);

    public async Task<IReadOnlyList<Store>> GetAllAsync(CancellationToken ct = default) =>
        await db.Stores.OrderBy(s => s.Name).ToListAsync(ct);

    public void Add(Store store) => db.Stores.Add(store);
}

internal sealed class ProductRepository(AppDbContext db) : IProductRepository
{
    public Task<Product?> FindByEanAsync(string ean, CancellationToken ct = default) =>
        db.Products.FirstOrDefaultAsync(p => p.Ean == ean, ct);

    public Task<Product?> FindByBrandAndModelAsync(int brandId, string model, CancellationToken ct = default) =>
        db.Products.FirstOrDefaultAsync(p => p.BrandId == brandId && p.Model == model, ct);

    public Task<Product?> FindByBrandAndModelInTextAsync(int brandId, string compactTitle, int minModelLength, CancellationToken ct = default) =>
        db.Products
            .Where(p => p.BrandId == brandId && p.Model != null && p.Model.Length >= minModelLength && compactTitle.Contains(p.Model))
            .OrderByDescending(p => p.Model!.Length)
            .ThenBy(p => p.Id)
            .FirstOrDefaultAsync(ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) =>
        db.Products.AnyAsync(p => p.Slug == slug, ct);

    public Task<Brand?> FindBrandBySlugAsync(string slug, CancellationToken ct = default) =>
        db.Brands.FirstOrDefaultAsync(b => b.Slug == slug, ct);

    public Task<Category?> FindCategoryBySlugAsync(string slug, CancellationToken ct = default) =>
        db.Categories.FirstOrDefaultAsync(c => c.Slug == slug, ct);

    public void Add(Product product) => db.Products.Add(product);
    public void Add(Brand brand) => db.Brands.Add(brand);
    public void Add(Category category) => db.Categories.Add(category);

    public async Task RefreshAggregatesAsync(IReadOnlyCollection<int> productIds, CancellationToken ct = default)
    {
        if (productIds.Count == 0) return;

        var aggregates = await db.Offers
            .Where(o => productIds.Contains(o.ProductId) && o.IsAvailable)
            .GroupBy(o => o.ProductId)
            .Select(g => new { ProductId = g.Key, Best = g.Min(o => o.Price), Count = g.Count() })
            .ToDictionaryAsync(x => x.ProductId, ct);

        var toUpdate = await db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var product in toUpdate)
        {
            aggregates.TryGetValue(product.Id, out var agg);
            var best = agg?.Best;
            var count = agg?.Count ?? 0;
            if (product.BestPrice == best && product.OfferCount == count) continue;
            product.BestPrice = best;
            product.OfferCount = count;
            product.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
    }
}

internal sealed class OfferRepository(AppDbContext db) : IOfferRepository
{
    public async Task<Dictionary<string, Offer>> GetByExternalIdsAsync(int storeId, IReadOnlyCollection<string> externalIds, CancellationToken ct = default) =>
        await db.Offers
            .Include(o => o.Product)
            .Where(o => o.StoreId == storeId && externalIds.Contains(o.ExternalId))
            .ToDictionaryAsync(o => o.ExternalId, ct);

    public Task<int> CountAvailableAsync(int storeId, CancellationToken ct = default) =>
        db.Offers.CountAsync(o => o.StoreId == storeId && o.IsAvailable, ct);

    public async Task<IReadOnlyList<Offer>> GetAvailableNotSeenSinceAsync(int storeId, DateTime seenBefore, CancellationToken ct = default) =>
        await db.Offers.Where(o => o.StoreId == storeId && o.IsAvailable && o.LastSeenAt < seenBefore).ToListAsync(ct);

    public void Add(Offer offer) => db.Offers.Add(offer);
    public void Add(PricePoint point) => db.PricePoints.Add(point);
}

internal sealed class ScrapeRunRepository(AppDbContext db) : IScrapeRunRepository
{
    public Task<ScrapeRun?> GetAsync(long id, CancellationToken ct = default) =>
        db.ScrapeRuns.FirstOrDefaultAsync(r => r.Id == id, ct);

    public void Add(ScrapeRun run) => db.ScrapeRuns.Add(run);
}

internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
    public void ClearTracking() => db.ChangeTracker.Clear();
}
