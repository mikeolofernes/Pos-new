using Pos.BuildingBlocks;
using Pos.Domain.Common;

namespace Pos.Domain.Audit;

public class AuditEvent : ITenantEntity
{
    public Guid Id { get; set; } = UlidGuid.NewUlidGuid();
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = default!;       // sale.created, product.updated, ...
    public string EntityType { get; set; } = default!;
    public Guid? EntityId { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public string? PayloadJson { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class OutboxMessage : ITenantEntity
{
    public Guid Id { get; set; } = UlidGuid.NewUlidGuid();
    public Guid TenantId { get; set; }
    public string Type { get; set; } = default!;
    public string PayloadJson { get; set; } = default!;
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
