using PrecioHerramientas.Core.Entities;

namespace PrecioHerramientas.Core.Abstractions;

public interface IStoreRepository
{
    Task<Store?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<Store>> GetAllAsync(CancellationToken ct = default);
    void Add(Store store);
}

public interface IProductRepository
{
    Task<Product?> FindByEanAsync(string ean, CancellationToken ct = default);
    Task<Product?> FindByBrandAndModelAsync(int brandId, string model, CancellationToken ct = default);
    /// <summary>
    /// Producto de la marca cuyo modelo aparece dentro de <paramref name="compactTitle"/>
    /// (título en mayúsculas sin espacios ni signos). Se prefiere el modelo más largo.
    /// </summary>
    Task<Product?> FindByBrandAndModelInTextAsync(int brandId, string compactTitle, int minModelLength, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    Task<Brand?> FindBrandBySlugAsync(string slug, CancellationToken ct = default);
    Task<Category?> FindCategoryBySlugAsync(string slug, CancellationToken ct = default);
    void Add(Product product);
    void Add(Brand brand);
    void Add(Category category);

    /// <summary>Recalcula mejor precio y cantidad de ofertas disponibles de los productos indicados.</summary>
    Task RefreshAggregatesAsync(IReadOnlyCollection<int> productIds, CancellationToken ct = default);
}

public interface IOfferRepository
{
    /// <summary>
    /// Ofertas de una tienda con esos ids externos, en todas sus sucursales, con su producto
    /// cargado. La clave es <see cref="OfferKeys.For(string, string?)"/>.
    /// </summary>
    Task<Dictionary<string, Offer>> GetByExternalIdsAsync(int storeId, IReadOnlyCollection<string> externalIds, CancellationToken ct = default);
    Task<int> CountAvailableAsync(int storeId, CancellationToken ct = default);

    /// <summary>Ofertas disponibles de la tienda que no se vieron desde <paramref name="seenBefore"/>.</summary>
    Task<IReadOnlyList<Offer>> GetAvailableNotSeenSinceAsync(int storeId, DateTime seenBefore, CancellationToken ct = default);
    void Add(Offer offer);
    void Add(PricePoint point);
}

public interface IScrapeRunRepository
{
    Task<ScrapeRun?> GetAsync(long id, CancellationToken ct = default);
    void Add(ScrapeRun run);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Libera las entidades rastreadas para que lotes grandes no acumulen memoria.</summary>
    void ClearTracking();
}
