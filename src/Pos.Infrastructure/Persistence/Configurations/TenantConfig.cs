using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Tenancy;

namespace Pos.Infrastructure.Persistence.Configurations;

public class TenantConfig : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> e)
    {
        e.ToTable("tenants");
        e.HasKey(x => x.Id);
        e.HasIndex(x => x.Slug).IsUnique();
        e.Property(x => x.Slug).HasMaxLength(64).IsRequired();
        e.Property(x => x.Name).HasMaxLength(256).IsRequired();
        e.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();
        e.Property(x => x.DefaultCurrency).HasMaxLength(3).IsRequired();
        e.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
        e.Property(x => x.Status).HasConversion<short>();
        e.Property(x => x.IsolationMode).HasConversion<short>();
    }
}

public class SubscriptionConfig : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> e)
    {
        e.ToTable("subscriptions");
        e.HasKey(x => x.Id);
        e.HasIndex(x => x.TenantId).IsUnique();
        e.Property(x => x.PlanCode).HasMaxLength(64).IsRequired();
    }
}

public class FeatureFlagConfig : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> e)
    {
        e.ToTable("feature_flags");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        e.Property(x => x.Key).HasMaxLength(128).IsRequired();
        e.Property(x => x.Json).HasColumnType("jsonb");
    }
}
