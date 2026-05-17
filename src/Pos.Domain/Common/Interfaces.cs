namespace Pos.Domain.Common;

public interface IEntity
{
    Guid Id { get; set; }
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
}

public interface ITenantEntity : IEntity
{
    Guid TenantId { get; set; }
}

/// <summary>
/// Marker for entities that belong to a single shop within a tenant.
/// AppDbContext auto-populates ShopId from ITenantContext on insert and
/// filters reads by the current shop.
/// </summary>
public interface IShopEntity : ITenantEntity
{
    Guid ShopId { get; set; }
}

public abstract class TenantEntity : ITenantEntity
{
    public Guid Id { get; set; } = Pos.BuildingBlocks.UlidGuid.NewUlidGuid();
    public Guid TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public uint Xmin { get; set; }   // PG system column for optimistic concurrency
}
