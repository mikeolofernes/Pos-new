using MediatR;
using Pos.Api.Common;
using Pos.Application.Features.Inventory;
using Pos.Domain.Identity;

namespace Pos.Api.Endpoints;

public static class InventoryEndpoints
{
    public sealed record AdjustBody(Guid ShopId, Guid WarehouseId, Guid ProductId, Guid? VariantId,
        decimal QuantityDelta, decimal UnitCost, string? Reason);

    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/inventory").WithTags("Inventory").RequireAuthorization();

        g.MapGet("/{productId:guid}/balance", async (Guid productId, Guid? variantId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetBalanceQuery(productId, variantId), ct)))
         .RequireAuthorization(Permissions.InventoryRead);

        g.MapPost("/adjustments", async (
                AdjustBody body,
                HttpContext http,
                ISender sender, CancellationToken ct) =>
            {
                var idem = http.Request.Headers["Idempotency-Key"].ToString();
                if (string.IsNullOrWhiteSpace(idem))
                    return Results.Problem("Idempotency-Key header required", statusCode: 400);
                var cmd = new AdjustStockCommand(body.ShopId, body.WarehouseId, body.ProductId, body.VariantId,
                    body.QuantityDelta, body.UnitCost, body.Reason, idem);
                return (await sender.Send(cmd, ct)).ToHttp();
            })
         .RequireAuthorization(Permissions.InventoryWrite);

        return app;
    }
}
