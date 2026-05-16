using MediatR;
using Microsoft.AspNetCore.SignalR;
using Pos.Api.Common;
using Pos.Api.Hubs;
using Pos.Application.Features.Pos;
using Pos.Domain.Identity;

namespace Pos.Api.Endpoints;

public static class PosEndpoints
{
    public static IEndpointRouteBuilder MapPosEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/pos").WithTags("POS").RequireAuthorization();

        g.MapPost("/sales", async (
                CreateSaleRequest body,
                HttpContext http,
                ISender sender,
                IHubContext<PosHub> hub,
                CancellationToken ct) =>
            {
                var idem = http.Request.Headers["Idempotency-Key"].ToString();
                if (string.IsNullOrWhiteSpace(idem))
                    return Results.Problem("Idempotency-Key header required", statusCode: 400);

                var res = await sender.Send(new CreateSaleCommand(body, idem), ct);

                if (res.IsSuccess)
                {
                    var tid = http.User.FindFirst("tid")?.Value;
                    if (tid is not null)
                        await hub.Clients.Group($"tenant-{tid}")
                            .SendAsync("saleCommitted", res.Value, ct);
                }

                return res.ToHttp(v => Results.Created($"/api/v1/pos/sales/{v.Id}", v));
            })
         .RequireAuthorization(Permissions.SalesCreate);

        g.MapPost("/shifts/open", async (OpenShiftCommand cmd, ISender sender, CancellationToken ct) =>
                (await sender.Send(cmd, ct)).ToHttp(id => Results.Created($"/api/v1/pos/shifts/{id}", new { id })))
         .RequireAuthorization(Permissions.ShiftsManage);

        g.MapPost("/shifts/{id:guid}/close", async (Guid id, decimal closingDeclared, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CloseShiftCommand(id, closingDeclared), ct)).ToHttp())
         .RequireAuthorization(Permissions.ShiftsManage);

        return app;
    }
}
