using MediatR;
using Pos.Application.Features.Sync;

namespace Pos.Api.Endpoints;

public static class SyncEndpoints
{
    public sealed record SyncBatchBody(IReadOnlyList<SyncEnvelope> Envelopes);

    public static IEndpointRouteBuilder MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/sync").WithTags("Sync").RequireAuthorization();

        g.MapPost("/batch", async (SyncBatchBody body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new SyncBatchCommand(body.Envelopes), ct)));

        return app;
    }
}
