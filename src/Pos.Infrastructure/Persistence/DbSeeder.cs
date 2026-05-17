using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.Domain.Catalog;
using Pos.Domain.Identity;
using Pos.Domain.Shops;
using Pos.Domain.Tenancy;

namespace Pos.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Slug == "demo", ct)) return;

        var tenant = new Tenant
        {
            Slug = "demo",
            Name = "Demo Co.",
            CountryCode = "US",
            DefaultCurrency = "USD",
            TimeZoneId = "UTC",
            Status = TenantStatus.Active,
            IsolationMode = TenantIsolationMode.Pooled
        };
        db.Tenants.Add(tenant);

        var sub = new Subscription { TenantId = tenant.Id, PlanCode = "starter", Status = 1,
            MaxShops = 5, MaxUsers = 25, MaxMonthlyTx = 100_000,
            RenewsAt = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)) };
        db.Subscriptions.Add(sub);

        var shop = new Shop { TenantId = tenant.Id, Code = "MAIN", Name = "Main Store",
            Currency = "USD", TimeZoneId = "UTC", CountryCode = "US" };
        db.Shops.Add(shop);

        var register = new Register { TenantId = tenant.Id, ShopId = shop.Id, Code = "R1", Name = "Register 1" };
        db.Registers.Add(register);

        var warehouse = new Warehouse { TenantId = tenant.Id, ShopId = shop.Id, Code = "WH-MAIN", Name = "Main Warehouse" };
        db.Warehouses.Add(warehouse);

        var adminRole = new Role { TenantId = tenant.Id, Name = "Admin",
            Permissions = Permissions.All, IsSystem = true };
        var cashierRole = new Role { TenantId = tenant.Id, Name = "Cashier",
            Permissions = new[]
            {
                Permissions.SalesCreate, Permissions.ProductsRead,
                Permissions.InventoryRead, Permissions.ShopsRead,
                Permissions.ShiftsManage
            } };
        db.Roles.AddRange(adminRole, cashierRole);

        var admin = new User { TenantId = tenant.Id, Email = "admin@demo.local",
            DisplayName = "Admin", PasswordHash = hasher.Hash("Passw0rd!"), IsActive = true };
        var cashier = new User { TenantId = tenant.Id, Email = "cashier@demo.local",
            DisplayName = "Cashier", PasswordHash = hasher.Hash("Passw0rd!"), IsActive = true };
        db.Users.AddRange(admin, cashier);

        db.UserShopRoles.AddRange(
            new UserShopRole { TenantId = tenant.Id, UserId = admin.Id, ShopId = shop.Id, RoleId = adminRole.Id },
            new UserShopRole { TenantId = tenant.Id, UserId = cashier.Id, ShopId = shop.Id, RoleId = cashierRole.Id });

        db.Taxes.Add(new Tax { TenantId = tenant.Id, Code = "standard",
            Name = "Standard Sales Tax", Rate = 0.0875m, IsInclusive = false, IsActive = true });
        db.Taxes.Add(new Tax { TenantId = tenant.Id, Code = "zero",
            Name = "Zero Rated", Rate = 0m, IsInclusive = false, IsActive = true });

        db.Products.AddRange(
            new Product { TenantId = tenant.Id, ShopId = shop.Id, Sku ="COFFEE-12", Name = "Coffee 12oz",
                DefaultPriceAmount = 4.50m, DefaultPriceCurrency = "USD",
                DefaultCost = 1.10m, TaxCode = "standard", TrackInventory = true },
            new Product { TenantId = tenant.Id, ShopId = shop.Id, Sku ="MUFFIN-CHOC", Name = "Chocolate Muffin",
                DefaultPriceAmount = 3.25m, DefaultPriceCurrency = "USD",
                DefaultCost = 0.90m, TaxCode = "standard", TrackInventory = true },
            new Product { TenantId = tenant.Id, ShopId = shop.Id, Sku ="WATER-500", Name = "Bottled Water 500ml",
                DefaultPriceAmount = 1.75m, DefaultPriceCurrency = "USD",
                DefaultCost = 0.40m, TaxCode = "zero", TrackInventory = true });

        await db.SaveChangesAsync(ct);
    }
}
