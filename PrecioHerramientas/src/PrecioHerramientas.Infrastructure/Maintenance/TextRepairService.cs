using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PrecioHerramientas.Core.Entities;
using PrecioHerramientas.Core.Services;
using PrecioHerramientas.Core.Text;
using PrecioHerramientas.Infrastructure.Persistence;

namespace PrecioHerramientas.Infrastructure.Maintenance;

/// <summary>
/// Repara con <see cref="TextRepair"/> los textos guardados. La vista previa y la aplicación
/// usan el mismo cálculo: la vista previa descarta los cambios en vez de guardarlos.
/// </summary>
internal sealed class TextRepairService(AppDbContext db) : ITextRepairService
{
    public async Task<TextRepairReport> PreviewAsync(CancellationToken ct = default)
    {
        try
        {
            return await PlanAsync(ct);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    public async Task<TextRepairReport> ApplyAsync(CancellationToken ct = default)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var report = await PlanAsync(ct);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return report;
    }

    // Caracteres con que empieza el mojibake de texto en español: Ã (á é í ó ú ñ…),
    // Â (° ¿ ¡ º), â (″ – “ ” €), y Ä/Å por si hay letras de otros idiomas.
    // Npgsql traduce Regex.IsMatch al operador ~ de PostgreSQL.
    private const string Markers = "[ÃÂâÄÅ]";

    private async Task<TextRepairReport> PlanAsync(CancellationToken ct)
    {
        var changes = new List<TextChange>();
        var merges = new List<string>();

        void Track(string table, long id, string field, string? before, string? after)
        {
            if (before is not null && after is not null && before != after)
                changes.Add(new TextChange(table, id, field, before, after));
        }

        // ---------- Marcas y categorías (antes que productos: el texto de búsqueda usa la marca) ----------
        var brands = await db.Brands.Where(b => Regex.IsMatch(b.Name, Markers)).ToListAsync(ct);
        foreach (var brand in brands)
        {
            var fixedName = TextRepair.Fix(brand.Name)!;
            if (fixedName == brand.Name) continue;
            var slug = TextNormalizer.Slugify(fixedName, 60);
            var existing = await db.Brands.FirstOrDefaultAsync(b => b.Slug == slug && b.Id != brand.Id, ct);
            Track("brands", brand.Id, "name", brand.Name, fixedName);
            if (existing is null)
            {
                brand.Name = Cut(fixedName, FieldLimits.BrandName);
                brand.Slug = slug;
                continue;
            }

            // Ya existe la marca bien escrita: sus productos pasan a ella.
            merges.Add($"Marca \"{brand.Name}\" → \"{existing.Name}\"");
            foreach (var product in await db.Products.Where(p => p.BrandId == brand.Id).ToListAsync(ct))
            {
                var twin = product.Model is null ? null
                    : await db.Products.FirstOrDefaultAsync(p => p.BrandId == existing.Id && p.Model == product.Model, ct);
                if (twin is null) product.BrandId = existing.Id;
                else await MergeProductAsync(product, twin, merges, ct);
            }
            db.Brands.Remove(brand);
        }

        var categories = await db.Categories.Where(c => Regex.IsMatch(c.Name, Markers)).ToListAsync(ct);
        foreach (var category in categories)
        {
            var fixedName = TextRepair.Fix(category.Name)!;
            if (fixedName == category.Name) continue;
            var slug = TextNormalizer.Slugify(fixedName, 60);
            var existing = await db.Categories.FirstOrDefaultAsync(c => c.Slug == slug && c.Id != category.Id, ct);
            Track("categories", category.Id, "name", category.Name, fixedName);
            if (existing is null)
            {
                category.Name = Cut(fixedName, FieldLimits.CategoryName);
                category.Slug = slug;
                continue;
            }
            merges.Add($"Categoría \"{category.Name}\" → \"{existing.Name}\"");
            await db.Products.Where(p => p.CategoryId == category.Id).ForEachAsync(p => p.CategoryId = existing.Id, ct);
            db.Categories.Remove(category);
        }

        // ---------- Productos: nombre, slug y texto de búsqueda ----------
        var products = await db.Products.Include(p => p.Brand)
            .Where(p => Regex.IsMatch(p.Name, Markers) || Regex.IsMatch(p.Slug, Markers))
            .ToListAsync(ct);
        var takenSlugs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var product in products.Where(p => db.Entry(p).State != EntityState.Deleted))
        {
            var fixedName = Cut(TextRepair.Fix(product.Name)!, FieldLimits.ProductName);
            if (fixedName == product.Name) continue;
            Track("products", product.Id, "name", product.Name, fixedName);
            product.Name = fixedName;

            // El slug salió del nombre dañado ("va-lvula"): se regenera si queda libre.
            var slug = TextNormalizer.Slugify(fixedName);
            for (int i = 2; takenSlugs.Contains(slug) || await db.Products.AnyAsync(p => p.Slug == slug && p.Id != product.Id, ct); i++)
                slug = $"{TextNormalizer.Slugify(fixedName)}-{i}";
            if (slug != product.Slug)
            {
                Track("products", product.Id, "slug", product.Slug, slug);
                product.Slug = slug;
            }
            takenSlugs.Add(slug);

            product.SearchText = Cut(TextNormalizer.ToSearchText($"{fixedName} {product.Brand?.Name} {product.Model} {product.Ean}"), FieldLimits.SearchText);
            product.UpdatedAt = DateTime.UtcNow;
        }

        // ---------- Ofertas ----------
        var offers = await db.Offers
            .Where(o => Regex.IsMatch(o.Title, Markers)
                || (o.BrandRaw != null && Regex.IsMatch(o.BrandRaw, Markers))
                || (o.ModelRaw != null && Regex.IsMatch(o.ModelRaw, Markers))
                || (o.Seller != null && Regex.IsMatch(o.Seller, Markers))
                || (o.BranchName != null && Regex.IsMatch(o.BranchName, Markers)))
            .ToListAsync(ct);
        foreach (var offer in offers)
        {
            string? Repair(string field, string? value, int max)
            {
                var fixedValue = TextRepair.Fix(value);
                if (fixedValue is null || fixedValue == value) return value;
                fixedValue = Cut(fixedValue, max);
                Track("offers", offer.Id, field, value, fixedValue);
                return fixedValue;
            }
            offer.Title = Repair("title", offer.Title, FieldLimits.OfferTitle)!;
            offer.BrandRaw = Repair("brand_raw", offer.BrandRaw, FieldLimits.BrandRaw);
            offer.ModelRaw = Repair("model_raw", offer.ModelRaw, FieldLimits.ModelRaw);
            offer.Seller = Repair("seller", offer.Seller, FieldLimits.Seller);
            offer.BranchName = Repair("branch_name", offer.BranchName, FieldLimits.BranchName);
        }

        var ordered = changes
            .OrderBy(c => c.Table == "products" && c.Field == "name" ? 0 : c.Table == "products" ? 1 : c.Table == "offers" ? 2 : 3)
            .ThenBy(c => c.Id)
            .ToList();
        return new TextRepairReport(ordered, merges);
    }

    /// <summary>Junta un producto duplicado en otro: ofertas y clics pasan al que se conserva.</summary>
    private async Task MergeProductAsync(Product from, Product into, List<string> merges, CancellationToken ct)
    {
        merges.Add($"Producto \"{from.Name}\" (#{from.Id}) → \"{into.Name}\" (#{into.Id})");
        await db.Offers.Where(o => o.ProductId == from.Id).ForEachAsync(o => o.ProductId = into.Id, ct);
        await db.Clicks.Where(c => c.ProductId == from.Id).ForEachAsync(c => c.ProductId = into.Id, ct);
        into.ImageUrl ??= from.ImageUrl;
        into.Ean ??= from.Ean;
        db.Products.Remove(from);
    }

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];
}
