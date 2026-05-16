using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Catalog;

namespace Pos.Infrastructure.Persistence.Configurations;

public class CategoryConfig : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> e)
    {
        e.ToTable("categories");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique();
        e.Property(x => x.Name).HasMaxLength(128).IsRequired();
        e.Property(x => x.Slug).HasMaxLength(128).IsRequired();
    }
}

public class ProductConfig : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> e)
    {
        e.ToTable("products");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique();
        e.HasIndex(x => new { x.TenantId, x.Name });
        e.Ignore(x => x.DefaultPrice);
        e.Property(x => x.Sku).HasMaxLength(64).IsRequired();
        e.Property(x => x.Name).HasMaxLength(256).IsRequired();
        e.Property(x => x.Description).HasMaxLength(2000);
        e.Property(x => x.Type).HasConversion<short>();
        e.Property(x => x.DefaultPriceAmount).HasColumnType("numeric(19,4)");
        e.Property(x => x.DefaultPriceCurrency).HasMaxLength(3).IsRequired();
        e.Property(x => x.DefaultCost).HasColumnType("numeric(19,4)");
        e.Property(x => x.TaxCode).HasMaxLength(32).IsRequired();
    }
}

public class ProductVariantConfig : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> e)
    {
        e.ToTable("product_variants");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique();
        e.HasIndex(x => x.ProductId);
        e.Property(x => x.Sku).HasMaxLength(64).IsRequired();
        e.Property(x => x.Name).HasMaxLength(256).IsRequired();
        e.Property(x => x.AttributesJson).HasColumnType("jsonb");
        e.Property(x => x.PriceOverride).HasColumnType("numeric(19,4)");
        e.Property(x => x.CostOverride).HasColumnType("numeric(19,4)");
    }
}

public class BarcodeConfig : IEntityTypeConfiguration<Barcode>
{
    public void Configure(EntityTypeBuilder<Barcode> e)
    {
        e.ToTable("barcodes");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        e.HasIndex(x => x.ProductId);
        e.Property(x => x.Code).HasMaxLength(64).IsRequired();
        e.Property(x => x.Symbology).HasMaxLength(16).IsRequired();
    }
}

public class TaxConfig : IEntityTypeConfiguration<Tax>
{
    public void Configure(EntityTypeBuilder<Tax> e)
    {
        e.ToTable("taxes");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        e.Property(x => x.Code).HasMaxLength(32).IsRequired();
        e.Property(x => x.Name).HasMaxLength(128).IsRequired();
        e.Property(x => x.Rate).HasColumnType("numeric(9,6)");
    }
}
