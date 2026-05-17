using MediatR;
using Pos.Api.Common;
using Pos.Application.Features.Categories;
using Pos.Domain.Identity;

namespace Pos.Api.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/categories").WithTags("Categories").RequireAuthorization();

        g.MapGet("/", async (ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ListCategoriesQuery(), ct)))
         .RequireAuthorization(Permissions.ProductsRead);

        g.MapPost("/", async (CreateCategoryRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateCategoryCommand(body), ct))
                    .ToHttp(c => Results.Created($"/api/v1/categories/{c.Id}", c)))
         .RequireAuthorization(Permissions.ProductsWrite);

        g.MapPut("/{id:guid}", async (Guid id, UpdateCategoryRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateCategoryCommand(id, body), ct)).ToHttp())
         .RequireAuthorization(Permissions.ProductsWrite);

        g.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeleteCategoryCommand(id), ct)).ToHttp())
         .RequireAuthorization(Permissions.ProductsWrite);

        return app;
    }
}
