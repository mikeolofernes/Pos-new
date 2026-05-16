using MediatR;
using Pos.Api.Common;
using Pos.Application.Features.Products;
using Pos.Domain.Identity;

namespace Pos.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/products").WithTags("Products").RequireAuthorization();

        g.MapGet("/", async (string? q, int page, int pageSize, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListProductsQuery(q, page == 0 ? 1 : page, pageSize == 0 ? 50 : pageSize), ct)))
         .RequireAuthorization(Permissions.ProductsRead);

        g.MapPost("/", async (CreateProductRequest body, ISender sender, CancellationToken ct) =>
            (await sender.Send(new CreateProductCommand(body), ct))
                .ToHttp(p => Results.Created($"/api/v1/products/{p.Id}", p)))
         .RequireAuthorization(Permissions.ProductsWrite);

        return app;
    }
}
