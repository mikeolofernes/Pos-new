using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.Application.Features.Tenants;
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
            Status = TenantStatus.Active,
            IsolationMode = TenantIsolationMode.Pooled
        };
        db.Tenants.Add(tenant);

        var sub = new Subscription { TenantId = tenant.Id, PlanCode = "starter", Status = 1,
            MaxShops = 5, MaxUsers = 25, MaxMonthlyTx = 100_000,
            RenewsAt = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)) };
        db.Subscriptions.Add(sub);

        var shop = new Shop { TenantId = tenant.Id, Code = "MAIN", Name = "Main Store", CountryCode = "US" };
        db.Shops.Add(shop);

        var register = new Register { TenantId = tenant.Id, ShopId = shop.Id, Code = "R1", Name = "Register 1" };
        db.Registers.Add(register);

        var warehouse = new Warehouse { TenantId = tenant.Id, ShopId = shop.Id, Code = "WH-MAIN", Name = "Main Warehouse" };
        db.Warehouses.Add(warehouse);

        var (adminRole, _, cashierRole) = RoleSeeder.SeedSystemRoles(db, tenant.Id);

        var admin = new User { TenantId = tenant.Id, RoleId = adminRole.Id, Email = "admin@demo.local",
            DisplayName = "Admin", PasswordHash = hasher.Hash("Passw0rd!"), IsActive = true };
        var cashier = new User { TenantId = tenant.Id, RoleId = cashierRole.Id, Email = "cashier@demo.local",
            DisplayName = "Cashier", PasswordHash = hasher.Hash("Passw0rd!"), IsActive = true };
        db.Users.AddRange(admin, cashier);

        db.UserShopRoles.AddRange(
            new UserShopRole { TenantId = tenant.Id, UserId = admin.Id, ShopId = shop.Id, RoleId = adminRole.Id },
            new UserShopRole { TenantId = tenant.Id, UserId = cashier.Id, ShopId = shop.Id, RoleId = cashierRole.Id });

        db.Products.AddRange(
            new Product { TenantId = tenant.Id, ShopId = shop.Id, Sku ="COFFEE-12", Name = "Coffee 12oz",
                DefaultPriceAmount = 4.50m, DefaultCost = 1.10m, TrackInventory = true },
            new Product { TenantId = tenant.Id, ShopId = shop.Id, Sku ="MUFFIN-CHOC", Name = "Chocolate Muffin",
                DefaultPriceAmount = 3.25m, DefaultCost = 0.90m, TrackInventory = true },
            new Product { TenantId = tenant.Id, ShopId = shop.Id, Sku ="WATER-500", Name = "Bottled Water 500ml",
                DefaultPriceAmount = 1.75m, DefaultCost = 0.40m, TrackInventory = true });

        await db.SaveChangesAsync(ct);
    }
}
