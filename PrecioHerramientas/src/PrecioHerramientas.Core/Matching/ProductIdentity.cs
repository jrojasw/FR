using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Core.Text;

namespace PrecioHerramientas.Core.Matching;

/// <summary>Atributos normalizados con los que se agrupa un producto entre tiendas.</summary>
public sealed record ProductIdentity(string? Brand, string? Model, string? Ean)
{
    public static ProductIdentity From(ScrapedProduct item)
    {
        var brand = BrandCatalog.Resolve(item.Brand, item.Title);
        var model = TextNormalizer.NormalizeModel(item.Model) ?? ModelExtractor.Extract(item.Title, brand);
        var ean = TextNormalizer.NormalizeEan(item.Ean);
        return new ProductIdentity(brand, model, ean);
    }

    /// <summary>Clave marca+modelo; solo existe cuando se conocen ambos.</summary>
    public string? ModelKey => Brand is not null && Model is not null
        ? $"{TextNormalizer.Slugify(Brand)}:{Model}"
        : null;
}
