using Microsoft.EntityFrameworkCore;
using PrecioHerramientas.Core.Entities;
using PrecioHerramientas.Core.Queries;
using PrecioHerramientas.Core.Text;
using PrecioHerramientas.Infrastructure.Persistence;

namespace PrecioHerramientas.Infrastructure.Queries;

internal sealed class CatalogQueries(AppDbContext db) : ICatalogQueries
{
    private const int MaxPageSize = 96;

    public async Task<PagedResult<ProductListItem>> SearchAsync(ProductSearchQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var tokens = TextNormalizer.Tokenize(query.Text);

        IQueryable<Product> products = db.Products.AsNoTracking();

        if (query.OnlyAvailable)
            products = products.Where(p => p.OfferCount > 0);

        // Los términos ya vienen normalizados a [a-z0-9], así que no hay comodines que escapar.
        foreach (var token in tokens)
        {
            var pattern = $"%{token}%";
            products = products.Where(p => EF.Functions.Like(p.SearchText, pattern));
        }

        if (!string.IsNullOrWhiteSpace(query.BrandSlug))
            products = products.Where(p => p.Brand != null && p.Brand.Slug == query.BrandSlug);

        if (!string.IsNullOrWhiteSpace(query.CategorySlug))
            products = products.Where(p => p.Category != null && p.Category.Slug == query.CategorySlug);

        if (!string.IsNullOrWhiteSpace(query.StoreKey))
            products = products.Where(p => p.Offers.Any(o => o.IsAvailable && o.Store.Key == query.StoreKey));

        if (query.MinPrice is { } min)
            products = products.Where(p => p.BestPrice >= min);

        if (query.MaxPrice is { } max)
            products = products.Where(p => p.BestPrice <= max);

        var total = await products.CountAsync(ct);

        products = query.Sort switch
        {
            ProductSort.PriceAsc => products.OrderBy(p => p.BestPrice == null).ThenBy(p => p.BestPrice).ThenBy(p => p.Id),
            ProductSort.PriceDesc => products.OrderBy(p => p.BestPrice == null).ThenByDescending(p => p.BestPrice).ThenBy(p => p.Id),
            ProductSort.Name => products.OrderBy(p => p.Name).ThenBy(p => p.Id),
            ProductSort.Newest => products.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
            // Relevancia: primero lo que empieza con el primer término, luego lo que está en más tiendas.
            _ when tokens.Count > 0 => products
                .OrderByDescending(p => p.SearchText.StartsWith(tokens[0]))
                .ThenByDescending(p => p.OfferCount)
                .ThenBy(p => p.BestPrice)
                .ThenBy(p => p.Id),
            _ => products.OrderByDescending(p => p.OfferCount).ThenBy(p => p.BestPrice).ThenBy(p => p.Id),
        };

        var items = await products
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListItem(p.Id, p.Slug, p.Name, p.Brand != null ? p.Brand.Name : null, p.ImageUrl, p.BestPrice, p.OfferCount))
            .ToListAsync(ct);

        return new PagedResult<ProductListItem>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<FacetItem>> GetBrandsAsync(CancellationToken ct = default) =>
        await db.Products.AsNoTracking()
            .Where(p => p.OfferCount > 0 && p.Brand != null)
            .GroupBy(p => new { p.Brand!.Slug, p.Brand.Name })
            .OrderBy(g => g.Key.Name)
            .Select(g => new FacetItem(g.Key.Slug, g.Key.Name, g.Count()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<FacetItem>> GetCategoriesAsync(CancellationToken ct = default) =>
        await db.Products.AsNoTracking()
            .Where(p => p.OfferCount > 0 && p.Category != null)
            .GroupBy(p => new { p.Category!.Slug, p.Category.Name })
            .OrderBy(g => g.Key.Name)
            .Select(g => new FacetItem(g.Key.Slug, g.Key.Name, g.Count()))
            .ToListAsync(ct);

    public async Task<ProductDetail?> GetProductAsync(string slug, CancellationToken ct = default)
    {
        var product = await db.Products.AsNoTracking()
            .Where(p => p.Slug == slug)
            .Select(p => new
            {
                p.Id, p.Slug, p.Name, p.Model, p.Ean, p.ImageUrl,
                BrandName = p.Brand != null ? p.Brand.Name : null,
                BrandSlug = p.Brand != null ? p.Brand.Slug : null,
                CategoryName = p.Category != null ? p.Category.Name : null,
                CategorySlug = p.Category != null ? p.Category.Slug : null,
            })
            .FirstOrDefaultAsync(ct);
        if (product is null) return null;

        var offers = await db.Offers.AsNoTracking()
            .Where(o => o.ProductId == product.Id && o.Store.IsActive)
            .OrderByDescending(o => o.IsAvailable)
            .ThenBy(o => o.Price)
            .Select(o => new OfferView(o.Store.Key, o.Store.Name, o.Store.LogoUrl, o.Url, o.Title,
                o.Price, o.ListPrice, o.IsAvailable, o.LastSeenAt, o.LastPriceChangeAt,
                o.BranchCode == "" ? null : o.BranchName ?? o.BranchCode))
            .ToListAsync(ct);

        var historicalMin = await db.PricePoints.AsNoTracking()
            .Where(pp => pp.Offer.ProductId == product.Id && pp.IsAvailable)
            .OrderBy(pp => pp.Price)
            .ThenBy(pp => pp.CapturedAt)
            .Select(pp => new { pp.Price, pp.CapturedAt })
            .FirstOrDefaultAsync(ct);

        return new ProductDetail(product.Id, product.Slug, product.Name, product.BrandName, product.BrandSlug,
            product.CategoryName, product.CategorySlug, product.Model, product.Ean, product.ImageUrl, offers,
            historicalMin?.Price, historicalMin?.CapturedAt);
    }

    public async Task<IReadOnlyList<PriceSeries>> GetPriceHistoryAsync(int productId, int days, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 3650));

        var offers = await db.Offers.AsNoTracking()
            .Where(o => o.ProductId == productId && o.Store.IsActive)
            .Select(o => new { o.Id, o.Store.Key, o.Store.Name, o.Price, o.IsAvailable, o.LastSeenAt })
            .ToListAsync(ct);
        var offerIds = offers.Select(o => o.Id).ToList();

        var inRange = await db.PricePoints.AsNoTracking()
            .Where(pp => offerIds.Contains(pp.OfferId) && pp.CapturedAt >= since)
            .Select(pp => new { pp.OfferId, pp.CapturedAt, pp.Price, pp.IsAvailable })
            .ToListAsync(ct);

        // Último valor anterior a la ventana, para que la serie no empiece "en blanco".
        var before = await db.PricePoints.AsNoTracking()
            .Where(pp => offerIds.Contains(pp.OfferId) && pp.CapturedAt < since)
            .GroupBy(pp => pp.OfferId)
            .Select(g => g.OrderByDescending(pp => pp.CapturedAt)
                .Select(pp => new { pp.OfferId, pp.CapturedAt, pp.Price, pp.IsAvailable })
                .First())
            .ToListAsync(ct);

        // Serie de cada oferta (en tiendas con sucursales, una por sucursal).
        var perOffer = new Dictionary<int, List<PricePointView>>();
        foreach (var offer in offers)
        {
            var points = new List<PricePointView>();
            var start = before.FirstOrDefault(b => b.OfferId == offer.Id);
            if (start is not null)
                points.Add(new PricePointView(since, start.Price, start.IsAvailable));

            points.AddRange(inRange
                .Where(pp => pp.OfferId == offer.Id)
                .OrderBy(pp => pp.CapturedAt)
                .Select(pp => new PricePointView(pp.CapturedAt, pp.Price, pp.IsAvailable)));

            // El historial guarda solo cambios: se agrega el último valor visto para extender la línea.
            if (points.Count > 0 && offer.LastSeenAt > points[^1].Date)
                points.Add(new PricePointView(offer.LastSeenAt, offer.Price, offer.IsAvailable));

            if (points.Count > 0) perOffer[offer.Id] = points;
        }

        // Una línea por tienda: si tiene varias sucursales, el menor precio con stock entre ellas.
        return offers
            .Where(o => perOffer.ContainsKey(o.Id))
            .GroupBy(o => new { o.Key, o.Name })
            .OrderBy(g => g.Key.Name)
            .Select(g => new PriceSeries(g.Key.Key, g.Key.Name,
                g.Count() == 1 ? perOffer[g.First().Id] : LowestAcross(g.Select(o => perOffer[o.Id]).ToList())))
            .ToList();
    }

    /// <summary>
    /// Combina las series de varias sucursales en una: en cada instante en que alguna cambia,
    /// el menor precio entre las que tienen stock (o el menor, marcado sin stock, si ninguna tiene).
    /// </summary>
    internal static IReadOnlyList<PricePointView> LowestAcross(IReadOnlyList<List<PricePointView>> series)
    {
        var current = new PricePointView?[series.Count];
        var events = series
            .SelectMany((points, i) => points.Select(p => (Index: i, Point: p)))
            .OrderBy(e => e.Point.Date)
            .GroupBy(e => e.Point.Date);

        var merged = new List<PricePointView>();
        foreach (var moment in events)
        {
            foreach (var (index, point) in moment) current[index] = point;
            var known = current.Where(p => p is not null).Select(p => p!).ToList();
            var inStock = known.Where(p => p.IsAvailable).ToList();
            var next = inStock.Count > 0
                ? new PricePointView(moment.Key, inStock.Min(p => p.Price), true)
                : new PricePointView(moment.Key, known.Min(p => p.Price), false);

            if (merged.Count == 0 || merged[^1].Price != next.Price || merged[^1].IsAvailable != next.IsAvailable)
                merged.Add(next);
        }
        // Se conserva el último instante para que la línea llegue hasta hoy.
        if (merged.Count > 0 && series.Max(s => s[^1].Date) is var last && merged[^1].Date < last)
            merged.Add(merged[^1] with { Date = last });
        return merged;
    }

    public async Task<IReadOnlyList<PriceDrop>> GetRecentPriceDropsAsync(int take, int withinDays = 7, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-withinDays);
        return await db.Offers.AsNoTracking()
            .Where(o => o.IsAvailable && o.Store.IsActive && o.PreviousPrice != null && o.PreviousPrice > o.Price && o.LastPriceChangeAt >= since)
            .OrderByDescending(o => (o.PreviousPrice!.Value - o.Price) / o.PreviousPrice!.Value)
            .ThenByDescending(o => o.LastPriceChangeAt)
            .Take(take)
            .Select(o => new PriceDrop(o.Product.Slug, o.Product.Name, o.Product.ImageUrl,
                o.BranchCode == "" ? o.Store.Name : o.Store.Name + " · " + (o.BranchName ?? o.BranchCode),
                o.Price, o.PreviousPrice!.Value, o.LastPriceChangeAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StoreOverview>> GetStoresAsync(CancellationToken ct = default)
    {
        var stores = await db.Stores.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new
            {
                s.Key, s.Name, s.BaseUrl, s.LogoUrl, s.IsActive,
                Available = s.Offers.Count(o => o.IsAvailable),
                LastRun = s.ScrapeRuns.OrderByDescending(r => r.StartedAt)
                    .Select(r => new { r.StartedAt, r.Status, r.ItemsFound })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return stores
            .Select(s => new StoreOverview(s.Key, s.Name, s.BaseUrl, s.LogoUrl, s.IsActive, s.Available,
                s.LastRun?.StartedAt, s.LastRun?.Status.ToString(), s.LastRun?.ItemsFound))
            .ToList();
    }
}
