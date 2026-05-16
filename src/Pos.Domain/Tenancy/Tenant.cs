using Pos.BuildingBlocks;
using Pos.Domain.Common;

namespace Pos.Domain.Tenancy;

public enum TenantStatus : short { Trial = 1, Active = 2, Suspended = 3, Cancelled = 4 }
public enum TenantIsolationMode : short { Pooled = 1, Siloed = 2 }

public class Tenant : IEntity
{
    public Guid Id { get; set; } = UlidGuid.NewUlidGuid();
    public string Slug { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string CountryCode { get; set; } = "US";
    public string DefaultCurrency { get; set; } = "USD";
    public string TimeZoneId { get; set; } = "UTC";
    public TenantStatus Status { get; set; } = TenantStatus.Trial;
    public TenantIsolationMode IsolationMode { get; set; } = TenantIsolationMode.Pooled;
    public string? DedicatedConnectionString { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class Subscription : IEntity
{
    public Guid Id { get; set; } = UlidGuid.NewUlidGuid();
    public Guid TenantId { get; set; }
    public string PlanCode { get; set; } = "free";
    public DateOnly RenewsAt { get; set; }
    public short Status { get; set; }          // 1=Active,2=PastDue,3=Cancelled
    public int MaxShops { get; set; } = 1;
    public int MaxUsers { get; set; } = 5;
    public long MaxMonthlyTx { get; set; } = 1000;
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class FeatureFlag : ITenantEntity
{
    public Guid Id { get; set; } = UlidGuid.NewUlidGuid();
    public Guid TenantId { get; set; }
    public string Key { get; set; } = default!;
    public bool Enabled { get; set; }
    public string? Json { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
