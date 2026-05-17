using Microsoft.EntityFrameworkCore;
using Pos.Domain.Audit;
using Pos.Domain.Catalog;
using Pos.Domain.Identity;
using Pos.Domain.Inventory;
using Pos.Domain.Sales;
using Pos.Domain.Shops;
using Pos.Domain.Tenancy;

namespace Pos.Application.Common.Abstractions;

public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<FeatureFlag> FeatureFlags { get; }

    DbSet<Shop> Shops { get; }
    DbSet<Register> Registers { get; }
    DbSet<Warehouse> Warehouses { get; }

    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserShopRole> UserShopRoles { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Device> Devices { get; }

    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<Barcode> Barcodes { get; }
    DbSet<QuickSelect> QuickSelects { get; }

    DbSet<InventoryMovement> InventoryMovements { get; }
    DbSet<StockBalance> StockBalances { get; }

    DbSet<Sale> Sales { get; }
    DbSet<SaleItem> SaleItems { get; }
    DbSet<SalePayment> SalePayments { get; }
    DbSet<Shift> Shifts { get; }

    DbSet<AuditEvent> AuditEvents { get; }
    DbSet<OutboxMessage> Outbox { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
