using Microsoft.EntityFrameworkCore;
using PrecioHerramientas.Core.Queries;
using PrecioHerramientas.Infrastructure.Persistence;

namespace PrecioHerramientas.Infrastructure.Queries;

internal sealed class ClickReports(AppDbContext db) : IClickReports
{
    public async Task<ClickSummary> GetSummaryAsync(DateTime? since, int topProducts, CancellationToken ct = default)
    {
        var clicks = db.Clicks.AsNoTracking();
        if (since is { } from) clicks = clicks.Where(c => c.ClickedAt >= from);

        var byStoreCounts = await clicks
            .GroupBy(c => c.StoreId)
            .Select(g => new { StoreId = g.Key, Clicks = g.Count(), Last = g.Max(c => c.ClickedAt) })
            .ToListAsync(ct);

        // Todas las tiendas, también las que aún no tienen clics.
        var stores = await db.Stores.AsNoTracking().OrderBy(s => s.Name).Select(s => new { s.Id, s.Key, s.Name }).ToListAsync(ct);
        var byStore = stores
            .Select(s => byStoreCounts.FirstOrDefault(c => c.StoreId == s.Id) is { } c
                ? new StoreClicks(s.Key, s.Name, c.Clicks, c.Last)
                : new StoreClicks(s.Key, s.Name, 0, null))
            .OrderByDescending(s => s.Clicks)
            .ThenBy(s => s.StoreName)
            .ToList();

        var top = await clicks
            .Where(c => c.ProductId != null)
            .GroupBy(c => c.ProductId!.Value)
            .Select(g => new { ProductId = g.Key, Clicks = g.Count(), Last = g.Max(c => c.ClickedAt) })
            .OrderByDescending(x => x.Clicks)
            .ThenByDescending(x => x.Last)
            .Take(topProducts)
            .Join(db.Products, x => x.ProductId, p => p.Id, (x, p) => new ProductClicks(p.Id, p.Slug, p.Name, x.Clicks, x.Last))
            .ToListAsync(ct);

        return new ClickSummary(byStore.Sum(s => s.Clicks), byStore, top.OrderByDescending(t => t.Clicks).ThenByDescending(t => t.LastClickAt).ToList());
    }
}
