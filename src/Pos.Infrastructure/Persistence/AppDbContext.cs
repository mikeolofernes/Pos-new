using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.Domain.Audit;
using Pos.Domain.Catalog;
using Pos.Domain.Common;
using Pos.Domain.Identity;
using Pos.Domain.Inventory;
using Pos.Domain.Sales;
using Pos.Domain.Shops;
using Pos.Domain.Tenancy;

namespace Pos.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    private readonly ITenantContext _tenant;

    public AppDbContext(DbContextOptions<AppDbContext> opts, ITenantContext tenant) : base(opts)
        => _tenant = tenant;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<Register> Registers => Set<Register>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserShopRole> UserShopRoles => Set<UserShopRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Barcode> Barcodes => Set<Barcode>();
    public DbSet<Tax> Taxes => Set<Tax>();
    public DbSet<QuickSelect> QuickSelects => Set<QuickSelect>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    // EF Core re-evaluates these properties against the executing DbContext instance at
    // query time (it replaces Expression.Constant(this) with the live context). Accessing
    // _tenant here — rather than capturing _tenant directly in the expression — is what
    // makes the per-request tenant/shop filter work correctly across model caching.
    internal Guid CurrentTenantId => _tenant.TenantId;
    internal Guid? CurrentShopId => _tenant.ShopId;

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);
        mb.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Capture `this` (the DbContext), not _tenant directly. EF Core substitutes the
        // DbContext constant with the actual executing instance at query time, so each
        // request reads the correct per-request CurrentTenantId / CurrentShopId.
        var self = Expression.Constant(this, typeof(AppDbContext));

        foreach (var et in mb.Model.GetEntityTypes())
        {
            // Skip optimistic-concurrency column for now — PG 'xmin' is a system column
            // and can't be written as a user column. Re-enable later via UseXminAsConcurrencyToken().
            if (et.ClrType.GetProperty("Xmin") is not null)
                mb.Entity(et.ClrType).Ignore("Xmin");

            // Soft-delete + tenant + (optional) shop filter
            var clrType = et.ClrType;
            var isTenantOwned = typeof(ITenantEntity).IsAssignableFrom(clrType);
            var isShopOwned = typeof(IShopEntity).IsAssignableFrom(clrType);
            var isStockBalance = clrType == typeof(StockBalance);

            if (isTenantOwned || isStockBalance)
            {
                var p = Expression.Parameter(clrType, "e");

                Expression body = Expression.Equal(
                    Expression.Property(p, "TenantId"),
                    Expression.Convert(
                        Expression.Property(self, nameof(CurrentTenantId)),
                        typeof(Guid)));

                if (isTenantOwned)
                {
                    var notDeleted = Expression.Equal(
                        Expression.Property(p, nameof(IEntity.DeletedAt)),
                        Expression.Constant(null, typeof(DateTimeOffset?)));
                    body = Expression.AndAlso(body, notDeleted);
                }

                // Shop scope: only filter when the request carries a shop;
                // management screens without a shop see all rows in the tenant.
                if (isShopOwned)
                {
                    var ctxShopId = Expression.Property(self, nameof(CurrentShopId));
                    var hasShop = Expression.NotEqual(ctxShopId, Expression.Constant(null, typeof(Guid?)));
                    var shopMatch = Expression.Equal(
                        Expression.Property(p, "ShopId"),
                        Expression.Property(ctxShopId, "Value"));
                    body = Expression.AndAlso(body, Expression.OrElse(Expression.Not(hasShop), shopMatch));
                }

                mb.Entity(clrType).HasQueryFilter(Expression.Lambda(body, p));
            }
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default) entry.Entity.CreatedAt = now;
                if (entry.Entity is ITenantEntity te && te.TenantId == Guid.Empty)
                    te.TenantId = _tenant.TenantId;
                if (entry.Entity is IShopEntity se && se.ShopId == Guid.Empty && _tenant.ShopId is { } sid)
                    se.ShopId = sid;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        return await base.SaveChangesAsync(ct);
    }
}
