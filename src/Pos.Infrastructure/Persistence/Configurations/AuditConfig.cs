using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Audit;

namespace Pos.Infrastructure.Persistence.Configurations;

public class AuditEventConfig : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> e)
    {
        e.ToTable("audit_events");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.TenantId, x.OccurredAt });
        e.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
        e.Property(x => x.Action).HasMaxLength(64).IsRequired();
        e.Property(x => x.EntityType).HasMaxLength(64).IsRequired();
        e.Property(x => x.PayloadJson).HasColumnType("jsonb");
        e.Property(x => x.Ip).HasMaxLength(45);
        e.Property(x => x.UserAgent).HasMaxLength(512);
    }
}

public class OutboxConfig : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> e)
    {
        e.ToTable("outbox_messages");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.ProcessedAt, x.OccurredAt });
        e.Property(x => x.Type).HasMaxLength(128).IsRequired();
        e.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
    }
}
