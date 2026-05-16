using MediatR;
using Pos.Application.Features.Shops;
using Pos.Domain.Identity;

namespace Pos.Api.Endpoints;

public static class ShopEndpoints
{
    public static IEndpointRouteBuilder MapShopEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/shops").WithTags("Shops").RequireAuthorization();

        g.MapGet("/", async (ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ListShopsQuery(), ct)))
         .RequireAuthorization(Permissions.ShopsRead);

        return app;
    }
}
