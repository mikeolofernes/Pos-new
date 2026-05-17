using System.Security.Claims;
using Pos.Domain.Tenancy;

namespace Pos.Api.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx, ITenantContext tenantCtx)
    {
        // 1) JWT claim
        var tidClaim = ctx.User.FindFirst("tid")?.Value;
        var subClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? ctx.User.FindFirst("sub")?.Value;

        Guid? shopId = null;
        if (ctx.Request.Headers.TryGetValue("X-Shop-Id", out var shopHdr) &&
            Guid.TryParse(shopHdr.ToString(), out var sid))
        {
            shopId = sid;
        }

        if (Guid.TryParse(tidClaim, out var tid))
        {
            Guid? uid = Guid.TryParse(subClaim, out var u) ? u : null;
            tenantCtx.Set(tid, uid, shopId);
        }
        else
        {
            // Header path (service-to-service or pre-auth endpoints)
            if (ctx.Request.Headers.TryGetValue("X-Tenant-Id", out var hdr) &&
                Guid.TryParse(hdr.ToString(), out var hTid))
            {
                tenantCtx.Set(hTid, shopId: shopId);
            }
        }

        await _next(ctx);
    }
}
