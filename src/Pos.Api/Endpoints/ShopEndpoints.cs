using MediatR;
using Pos.Api.Common;
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

        g.MapPost("/", async (CreateShopRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateShopCommand(body), ct))
                    .ToHttp(s => Results.Created($"/api/v1/shops/{s.Id}", s)))
         .RequireAuthorization(Permissions.ShopsWrite);

        g.MapPut("/{id:guid}", async (Guid id, UpdateShopRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateShopCommand(id, body), ct)).ToHttp())
         .RequireAuthorization(Permissions.ShopsWrite);

        g.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeleteShopCommand(id), ct)).ToHttp())
         .RequireAuthorization(Permissions.ShopsWrite);

        return app;
    }
}
