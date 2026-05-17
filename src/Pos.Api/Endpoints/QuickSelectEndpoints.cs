using MediatR;
using Pos.Api.Common;
using Pos.Application.Features.QuickSelects;
using Pos.Domain.Identity;

namespace Pos.Api.Endpoints;

public static class QuickSelectEndpoints
{
    public static IEndpointRouteBuilder MapQuickSelectEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/quick-selects").WithTags("QuickSelects").RequireAuthorization();

        g.MapGet("/", async (Guid? shopId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ListQuickSelectsQuery(shopId), ct)))
         .RequireAuthorization(Permissions.ProductsRead);

        g.MapPost("/", async (CreateQuickSelectRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateQuickSelectCommand(body), ct))
                    .ToHttp(q => Results.Created($"/api/v1/quick-selects/{q.Id}", q)))
         .RequireAuthorization(Permissions.ProductsWrite);

        g.MapPut("/{id:guid}", async (Guid id, UpdateQuickSelectRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateQuickSelectCommand(id, body), ct)).ToHttp())
         .RequireAuthorization(Permissions.ProductsWrite);

        g.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeleteQuickSelectCommand(id), ct)).ToHttp())
         .RequireAuthorization(Permissions.ProductsWrite);

        return app;
    }
}
