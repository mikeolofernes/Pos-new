using Pos.BuildingBlocks;
using Pos.Domain.Common;

namespace Pos.Domain.Identity;

public class User : ITenantEntity
{
    public Guid Id { get; set; } = UlidGuid.NewUlidGuid();
    public Guid TenantId { get; set; }
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string DisplayName { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public bool MfaEnabled { get; set; }
    public string? MfaSecret { get; set; }
    public string? PinHash { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class Role : ITenantEntity
{
    public Guid Id { get; set; } = UlidGuid.NewUlidGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public bool IsSystem { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class UserShopRole : ITenantEntity
{
    public Guid Id { get; set; } = UlidGuid.NewUlidGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid ShopId { get; set; }
    public Guid RoleId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class RefreshToken : ITenantEntity
{
    public Guid Id { get; set; } = UlidGuid.NewUlidGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid DeviceId { get; set; }
    public string TokenHash { get; set; } = default!;   // sha256
    public Guid FamilyId { get; set; }                   // detect token reuse
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class Device : ITenantEntity
{
    public Guid Id { get; set; } = UlidGuid.NewUlidGuid();
    public Guid TenantId { get; set; }
    public Guid? ShopId { get; set; }
    public string Name { get; set; } = default!;
    public string? Fingerprint { get; set; }
    public string Type { get; set; } = "browser";      // browser, tablet, terminal, agent
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public static class Permissions
{
    public const string SalesCreate    = "sales.create";
    public const string SalesRefund    = "sales.refund";
    public const string SalesVoid      = "sales.void";
    public const string ProductsRead   = "products.read";
    public const string ProductsWrite  = "products.write";
    public const string InventoryRead  = "inventory.read";
    public const string InventoryWrite = "inventory.write";
    public const string ShopsRead      = "shops.read";
    public const string ShopsWrite     = "shops.write";
    public const string ShiftsManage   = "shifts.manage";
    public const string ReportsRead    = "reports.read";
    public const string UsersManage    = "users.manage";
    public const string SettingsManage = "settings.manage";

    public static readonly string[] All =
    {
        SalesCreate, SalesRefund, SalesVoid,
        ProductsRead, ProductsWrite,
        InventoryRead, InventoryWrite,
        ShopsRead, ShopsWrite,
        ShiftsManage, ReportsRead, UsersManage, SettingsManage
    };
}
