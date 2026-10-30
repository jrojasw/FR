using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Abstractions;
using PrecioHerramientas.Core.Entities;
using PrecioHerramientas.Core.Matching;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Core.Text;

namespace PrecioHerramientas.Core.Services;

/// <summary>
/// Incorpora los productos que entrega un scraper: actualiza o crea la oferta de la
/// tienda, la agrupa con el producto canónico (por EAN o marca+modelo) y registra
/// el historial cada vez que cambia el precio o la disponibilidad.
/// </summary>
public sealed class PriceIngestionService(
    IProductRepository products,
    IOfferRepository offers,
    IUnitOfWork unitOfWork,
    IClock clock,
    IOptions<IngestionOptions> options,
    ILogger<PriceIngestionService> logger)
{
    private readonly IngestionOptions _options = options.Value;

    /// <summary>Modelos más cortos (p. ej. "K2") se confunden con otras palabras del título.</summary>
    private const int MinModelLengthInTitle = 5;

    // Dos tiendas procesadas a la vez podrían crear el mismo producto (mismo EAN o
    // marca+modelo) y chocar con los índices únicos; los lotes se guardan de a uno.
    // Basta para una sola instancia de la aplicación, que es el despliegue previsto.
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    public async Task<IngestionStats> IngestAsync(int storeId, IAsyncEnumerable<ScrapedProduct> items, CancellationToken ct = default)
    {
        var stats = new IngestionStats();
        var runStartedAt = clock.UtcNow;
        var availableBefore = await offers.CountAvailableAsync(storeId, ct);

        var batch = new List<ScrapedProduct>(_options.BatchSize);
        await foreach (var item in items.WithCancellation(ct))
        {
            stats.ItemsFound++;
            batch.Add(item);
            if (batch.Count >= _options.BatchSize)
            {
                await LockedAsync(() => ProcessBatchAsync(storeId, batch, stats, ct), ct);
                batch.Clear();
            }
        }
        if (batch.Count > 0)
            await LockedAsync(() => ProcessBatchAsync(storeId, batch, stats, ct), ct);

        await LockedAsync(() => MarkUnseenOffersAsync(storeId, runStartedAt, availableBefore, stats, ct), ct);
        return stats;
    }

    private static async Task LockedAsync(Func<Task> action, CancellationToken ct)
    {
        await WriteLock.WaitAsync(ct);
        try
        {
            await action();
        }
        finally
        {
            WriteLock.Release();
        }
    }

    private async Task ProcessBatchAsync(int storeId, List<ScrapedProduct> batch, IngestionStats stats, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var context = new BatchContext();

        // Si la tienda repite un producto dentro del lote, vale la última aparición.
        var unique = batch
            .Where(i => IsValid(i) || Reject(stats, i))
            .GroupBy(i => i.ExternalId.Trim())
            .Select(g => g.Last())
            .ToList();

        var existing = await offers.GetByExternalIdsAsync(storeId, unique.Select(i => i.ExternalId.Trim()).ToList(), ct);

        foreach (var item in unique)
        {
            var identity = ProductIdentity.From(item);
            if (existing.TryGetValue(item.ExternalId.Trim(), out var offer))
            {
                UpdateOffer(offer, item, now, stats);
                EnrichProduct(offer.Product, item, null);
                context.Touched.Add(offer.Product);
            }
            else
            {
                var product = await ResolveProductAsync(identity, item, context, now, stats, ct);
                CreateOffer(storeId, product, item, now);
                stats.OffersCreated++;
                context.Touched.Add(product);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        await products.RefreshAggregatesAsync(context.Touched.Select(p => p.Id).ToList(), ct);
        unitOfWork.ClearTracking();
    }

    private bool Reject(IngestionStats stats, ScrapedProduct item)
    {
        stats.ItemsRejected++;
        logger.LogDebug("Producto descartado por datos incompletos: {ExternalId} {Title} {Price}", item.ExternalId, item.Title, item.Price);
        return false;
    }

    private static bool IsValid(ScrapedProduct item) =>
        !string.IsNullOrWhiteSpace(item.ExternalId)
        && !string.IsNullOrWhiteSpace(item.Title)
        && !string.IsNullOrWhiteSpace(item.Url)
        && item.Price > 0;

    private void UpdateOffer(Offer offer, ScrapedProduct item, DateTime now, IngestionStats stats)
    {
        offer.Title = TextNormalizer.CleanWhitespace(item.Title);
        offer.Url = item.Url;
        offer.BrandRaw = item.Brand ?? offer.BrandRaw;
        offer.ModelRaw = item.Model ?? offer.ModelRaw;
        offer.Ean = item.Ean ?? offer.Ean;
        offer.ImageUrl = item.ImageUrl ?? offer.ImageUrl;
        offer.LastSeenAt = now;

        var listPrice = NormalizeListPrice(item);
        bool priceChanged = offer.Price != item.Price;
        if (!priceChanged && offer.ListPrice == listPrice && offer.IsAvailable == item.IsAvailable)
            return;

        if (priceChanged)
        {
            offer.PreviousPrice = offer.Price;
            stats.PricesChanged++;
        }
        offer.Price = item.Price;
        offer.ListPrice = listPrice;
        offer.IsAvailable = item.IsAvailable;
        offer.LastPriceChangeAt = now;
        offers.Add(new PricePoint { OfferId = offer.Id, Price = offer.Price, ListPrice = listPrice, IsAvailable = offer.IsAvailable, CapturedAt = now });
    }

    private void CreateOffer(int storeId, Product product, ScrapedProduct item, DateTime now)
    {
        var listPrice = NormalizeListPrice(item);
        var offer = new Offer
        {
            StoreId = storeId,
            Product = product,
            ExternalId = item.ExternalId.Trim(),
            Url = item.Url,
            Title = TextNormalizer.CleanWhitespace(item.Title),
            BrandRaw = item.Brand,
            ModelRaw = item.Model,
            Ean = item.Ean,
            ImageUrl = item.ImageUrl,
            Price = item.Price,
            ListPrice = listPrice,
            IsAvailable = item.IsAvailable,
            FirstSeenAt = now,
            LastSeenAt = now,
            LastPriceChangeAt = now,
        };
        offer.PriceHistory.Add(new PricePoint { Price = item.Price, ListPrice = listPrice, IsAvailable = item.IsAvailable, CapturedAt = now });
        offers.Add(offer);
    }

    // Solo se guarda el precio normal si es mayor al precio actual; si no, no aporta.
    private static decimal? NormalizeListPrice(ScrapedProduct item) =>
        item.ListPrice is { } list && list > item.Price ? list : null;

    private async Task<Product> ResolveProductAsync(
        ProductIdentity identity, ScrapedProduct item, BatchContext context, DateTime now, IngestionStats stats, CancellationToken ct)
    {
        if (identity.Ean is { } ean)
        {
            var byEan = context.ByEan.GetValueOrDefault(ean) ?? await products.FindByEanAsync(ean, ct);
            if (byEan is not null)
            {
                EnrichProduct(byEan, item, null);
                return byEan;
            }
        }

        var brand = await GetOrCreateBrandAsync(identity.Brand, context, ct);

        if (brand is not null && identity.Model is { } model)
        {
            var byModel = context.ByModelKey.GetValueOrDefault(identity.ModelKey!)
                ?? (brand.Id != 0 ? await products.FindByBrandAndModelAsync(brand.Id, model, ct) : null);
            if (byModel is not null)
            {
                // El EAN no existe en otro producto (se buscó arriba), así que se puede asignar.
                EnrichProduct(byModel, item, identity.Ean);
                if (identity.Ean is not null) context.ByEan[identity.Ean] = byModel;
                return byModel;
            }
        }

        // Sin modelo coincidente: ¿el modelo de algún producto de la marca aparece en el título?
        // Cubre tiendas que no informan el modelo o lo escriben distinto ("Mod. TC-AC 200/50").
        if (brand is not null)
        {
            var compactTitle = TextNormalizer.ToCompactUpper(item.Title);
            var byTitle = context.ByModelKey.Values
                    .Where(p => (ReferenceEquals(p.Brand, brand) || (brand.Id != 0 && p.BrandId == brand.Id)) && p.Model is { Length: >= MinModelLengthInTitle } && compactTitle.Contains(p.Model))
                    .MaxBy(p => p.Model!.Length)
                ?? (brand.Id != 0 ? await products.FindByBrandAndModelInTextAsync(brand.Id, compactTitle, MinModelLengthInTitle, ct) : null);
            if (byTitle is not null)
            {
                EnrichProduct(byTitle, item, null);
                return byTitle;
            }
        }

        var name = TextNormalizer.CleanWhitespace(item.Title);
        var product = new Product
        {
            Name = name,
            Slug = await UniqueSlugAsync(name, context, ct),
            Brand = brand,
            Category = await GetOrCreateCategoryAsync(item.Category, context, ct),
            Model = identity.Model,
            Ean = identity.Ean,
            ImageUrl = item.ImageUrl,
            SearchText = TextNormalizer.ToSearchText($"{name} {brand?.Name} {identity.Model} {identity.Ean}"),
            CreatedAt = now,
            UpdatedAt = now,
        };
        products.Add(product);
        stats.ProductsCreated++;

        if (identity.Ean is not null) context.ByEan[identity.Ean] = product;
        if (identity.ModelKey is not null) context.ByModelKey[identity.ModelKey] = product;
        return product;
    }

    private static void EnrichProduct(Product product, ScrapedProduct item, string? ean)
    {
        product.ImageUrl ??= item.ImageUrl;
        if (product.Ean is null && ean is not null)
        {
            product.Ean = ean;
            product.SearchText = $"{product.SearchText} {ean}".Trim();
        }
    }

    private async Task<Brand?> GetOrCreateBrandAsync(string? name, BatchContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var slug = TextNormalizer.Slugify(name, 60);
        if (context.Brands.TryGetValue(slug, out var cached)) return cached;

        var brand = await products.FindBrandBySlugAsync(slug, ct);
        if (brand is null)
        {
            brand = new Brand { Name = name, Slug = slug };
            products.Add(brand);
        }
        return context.Brands[slug] = brand;
    }

    private async Task<Category?> GetOrCreateCategoryAsync(string? raw, BatchContext context, CancellationToken ct)
    {
        // Las tiendas suelen informar rutas ("Herramientas/Eléctricas/Taladros"); se usa el último nivel.
        var name = raw?.Split(new[] { '/', '>', '|' },StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        if (string.IsNullOrWhiteSpace(name)) return null;
        name = TextNormalizer.CleanWhitespace(name);
        var slug = TextNormalizer.Slugify(name, 60);
        if (context.Categories.TryGetValue(slug, out var cached)) return cached;

        var category = await products.FindCategoryBySlugAsync(slug, ct);
        if (category is null)
        {
            category = new Category { Name = name, Slug = slug };
            products.Add(category);
        }
        return context.Categories[slug] = category;
    }

    private async Task<string> UniqueSlugAsync(string name, BatchContext context, CancellationToken ct)
    {
        var baseSlug = TextNormalizer.Slugify(name);
        var slug = baseSlug;
        for (int i = 2; context.Slugs.Contains(slug) || await products.SlugExistsAsync(slug, ct); i++)
            slug = $"{baseSlug}-{i}";
        context.Slugs.Add(slug);
        return slug;
    }

    private async Task MarkUnseenOffersAsync(int storeId, DateTime runStartedAt, int availableBefore, IngestionStats stats, CancellationToken ct)
    {
        if (availableBefore > 0 && stats.ItemsFound < availableBefore * _options.MinCoverageToMarkUnavailable)
        {
            logger.LogWarning(
                "La tienda {StoreId} entregó {Found} productos de {Before} disponibles; no se marcan ofertas como agotadas por baja cobertura",
                storeId, stats.ItemsFound, availableBefore);
            return;
        }

        var now = clock.UtcNow;
        var unseen = await offers.GetAvailableNotSeenSinceAsync(storeId, runStartedAt, ct);
        if (unseen.Count == 0) return;

        foreach (var offer in unseen)
        {
            offer.IsAvailable = false;
            offer.LastPriceChangeAt = now;
            offers.Add(new PricePoint { OfferId = offer.Id, Price = offer.Price, ListPrice = offer.ListPrice, IsAvailable = false, CapturedAt = now });
        }
        stats.OffersMarkedUnavailable = unseen.Count;

        await unitOfWork.SaveChangesAsync(ct);
        await products.RefreshAggregatesAsync(unseen.Select(o => o.ProductId).Distinct().ToList(), ct);
        unitOfWork.ClearTracking();
    }

    /// <summary>Cachés válidas solo dentro de un lote (antes de limpiar el seguimiento de EF).</summary>
    private sealed class BatchContext
    {
        public Dictionary<string, Product> ByEan { get; } = new();
        public Dictionary<string, Product> ByModelKey { get; } = new();
        public Dictionary<string, Brand> Brands { get; } = new();
        public Dictionary<string, Category> Categories { get; } = new();
        public HashSet<string> Slugs { get; } = new();
        public HashSet<Product> Touched { get; } = new(ReferenceEqualityComparer.Instance);
    }
}
