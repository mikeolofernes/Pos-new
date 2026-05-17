using Pos.Domain.Common;

namespace Pos.Domain.Catalog;

public enum ProductType : short { Standard = 1, Variant = 2, Bundle = 3, Service = 4 }

public class Category : TenantEntity, IShopEntity
{
    public Guid ShopId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
}

public class Product : TenantEntity, IShopEntity
{
    public Guid ShopId { get; set; }
    public string Sku { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public ProductType Type { get; set; } = ProductType.Standard;
    public bool TrackInventory { get; set; } = true;
    public bool TrackBatches { get; set; }
    public bool TrackSerials { get; set; }
    public decimal DefaultPriceAmount { get; set; }
    public decimal? DefaultCost { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ProductVariant : TenantEntity
{
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string AttributesJson { get; set; } = "{}";
    public decimal? PriceOverride { get; set; }
    public decimal? CostOverride { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Barcode : TenantEntity
{
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string Code { get; set; } = default!;
    public string Symbology { get; set; } = "EAN13";
    public bool IsPrimary { get; set; }
}
