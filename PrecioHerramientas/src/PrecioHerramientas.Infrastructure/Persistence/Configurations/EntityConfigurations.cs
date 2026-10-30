using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrecioHerramientas.Core.Entities;

namespace PrecioHerramientas.Infrastructure.Persistence.Configurations;

internal sealed class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> b)
    {
        b.Property(x => x.Key).HasMaxLength(50);
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.BaseUrl).HasMaxLength(300);
        b.Property(x => x.LogoUrl).HasMaxLength(500);
        b.HasIndex(x => x.Key).IsUnique();
    }
}

internal sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> b)
    {
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.Slug).HasMaxLength(100);
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.Slug).HasMaxLength(100);
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.Property(x => x.Name).HasMaxLength(300);
        b.Property(x => x.Slug).HasMaxLength(140);
        b.Property(x => x.Model).HasMaxLength(60);
        b.Property(x => x.Ean).HasMaxLength(14);
        b.Property(x => x.ImageUrl).HasMaxLength(1000);
        b.Property(x => x.SearchText).HasMaxLength(600);

        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => x.Ean).IsUnique().HasFilter("ean IS NOT NULL");
        b.HasIndex(x => new { x.BrandId, x.Model }).IsUnique().HasFilter("brand_id IS NOT NULL AND model IS NOT NULL");
        b.HasIndex(x => x.SearchText).HasMethod("gin").HasOperators("gin_trgm_ops");
        b.HasIndex(x => x.BestPrice);

        b.HasOne(x => x.Brand).WithMany(x => x.Products).HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> b)
    {
        b.Property(x => x.ExternalId).HasMaxLength(100);
        b.Property(x => x.Url).HasMaxLength(1000);
        b.Property(x => x.Title).HasMaxLength(300);
        b.Property(x => x.BrandRaw).HasMaxLength(100);
        b.Property(x => x.ModelRaw).HasMaxLength(100);
        b.Property(x => x.Ean).HasMaxLength(50);
        b.Property(x => x.ImageUrl).HasMaxLength(1000);

        b.HasIndex(x => new { x.StoreId, x.ExternalId }).IsUnique();
        b.HasIndex(x => x.ProductId);
        b.HasIndex(x => x.LastPriceChangeAt);

        b.HasOne(x => x.Store).WithMany(x => x.Offers).HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Product).WithMany(x => x.Offers).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PricePointConfiguration : IEntityTypeConfiguration<PricePoint>
{
    public void Configure(EntityTypeBuilder<PricePoint> b)
    {
        b.HasIndex(x => new { x.OfferId, x.CapturedAt });
        b.HasOne(x => x.Offer).WithMany(x => x.PriceHistory).HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ScrapeRunConfiguration : IEntityTypeConfiguration<ScrapeRun>
{
    public void Configure(EntityTypeBuilder<ScrapeRun> b)
    {
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Error).HasMaxLength(4000);
        b.HasIndex(x => new { x.StoreId, x.StartedAt });
        b.HasOne(x => x.Store).WithMany(x => x.ScrapeRuns).HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
    }
}
