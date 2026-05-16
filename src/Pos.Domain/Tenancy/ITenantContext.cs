namespace Pos.Domain.Tenancy;

public interface ITenantContext
{
    Guid TenantId { get; }
    Guid? UserId { get; }
    Guid? ShopId { get; }
    bool IsSystem { get; }
    bool IsResolved { get; }
    void Set(Guid tenantId, Guid? userId = null, Guid? shopId = null, bool isSystem = false);
}
