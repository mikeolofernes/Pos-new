using MediatR;
using Pos.Api.Common;
using Pos.Application.Features.Tenants;
using Pos.Domain.Identity;

namespace Pos.Api.Endpoints;

public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/tenants").WithTags("Tenants").RequireAuthorization();

        // Listing / creating tenants is a cross-tenant operation. Gate it on
        // SettingsManage for now — production should require a true system role.
        g.MapGet("/", async (ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ListTenantsQuery(), ct)))
         .RequireAuthorization(Permissions.SettingsManage);

        g.MapPost("/", async (CreateTenantRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateTenantCommand(body), ct))
                    .ToHttp(t => Results.Created($"/api/v1/tenants/{t.Id}", t)))
         .AllowAnonymous();   // bootstrap path — new orgs sign up without an existing token

        return app;
    }
}
