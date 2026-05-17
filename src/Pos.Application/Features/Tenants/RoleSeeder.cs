using Pos.Application.Common.Abstractions;
using Pos.Domain.Identity;

namespace Pos.Application.Features.Tenants;

/// <summary>System roles created for every tenant. Single source of truth for role definitions.</summary>
public static class RoleSeeder
{
    public const string AdminName = "Admin";
    public const string ShopAdminName = "ShopAdmin";
    public const string CashierName = "Cashier";

    public static readonly string[] ShopAdminPermissions =
    {
        Permissions.SalesCreate, Permissions.SalesRefund, Permissions.SalesVoid,
        Permissions.ProductsRead, Permissions.ProductsWrite,
        Permissions.InventoryRead, Permissions.InventoryWrite,
        Permissions.ShopsRead, Permissions.ShiftsManage, Permissions.ReportsRead
    };

    public static readonly string[] CashierPermissions =
    {
        Permissions.SalesCreate, Permissions.ProductsRead,
        Permissions.InventoryRead, Permissions.ShopsRead,
        Permissions.ShiftsManage
    };

    public static (Role Admin, Role ShopAdmin, Role Cashier) SeedSystemRoles(IAppDbContext db, Guid tenantId)
    {
        var admin = new Role { TenantId = tenantId, Name = AdminName,
            Permissions = Permissions.All, IsSystem = true };
        var shopAdmin = new Role { TenantId = tenantId, Name = ShopAdminName,
            Permissions = ShopAdminPermissions, IsSystem = true };
        var cashier = new Role { TenantId = tenantId, Name = CashierName,
            Permissions = CashierPermissions, IsSystem = true };
        db.Roles.AddRange(admin, shopAdmin, cashier);
        return (admin, shopAdmin, cashier);
    }
}
