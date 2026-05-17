using MediatR;
using Pos.Application.Features.Roles;

namespace Pos.Api.Endpoints;

public static class RoleEndpoints
{
    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/roles").WithTags("Roles").RequireAuthorization();

        g.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListRolesQuery(), ct)));

        return app;
    }
}
