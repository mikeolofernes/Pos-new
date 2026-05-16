using Pos.Domain.Tenancy;

namespace Pos.Infrastructure.Tenancy;

public class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; } = Guid.Empty;
    public Guid? UserId { get; private set; }
    public Guid? ShopId { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsResolved { get; private set; }

    public void Set(Guid tenantId, Guid? userId = null, Guid? shopId = null, bool isSystem = false)
    {
        TenantId = tenantId;
        UserId = userId;
        ShopId = shopId;
        IsSystem = isSystem;
        IsResolved = true;
    }
}
