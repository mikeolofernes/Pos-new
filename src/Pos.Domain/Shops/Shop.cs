using Pos.Domain.Common;

namespace Pos.Domain.Shops;

public class Shop : TenantEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? PostalCode { get; set; }
    public string CountryCode { get; set; } = "US";
    public string TimeZoneId { get; set; } = "UTC";
    public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; } = true;
}

public class Register : TenantEntity
{
    public Guid ShopId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}

public class Warehouse : TenantEntity
{
    public Guid? ShopId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}
