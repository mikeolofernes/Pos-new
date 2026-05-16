using System.Text.Json;
using MediatR;
using Pos.Application.Features.Pos;
using Pos.BuildingBlocks;

namespace Pos.Application.Features.Sync;

public sealed record SyncEnvelope(
    string IdempotencyKey, string Resource, string Op,
    JsonElement Payload, DateTimeOffset CreatedAt,
    Guid DeviceId, long ClientSeq);

public sealed record SyncBatchCommand(IReadOnlyList<SyncEnvelope> Envelopes) : IRequest<SyncBatchResult>;

public sealed record SyncEnvelopeResult(string IdempotencyKey, string Status, string? ErrorCode, string? Message, JsonElement? Canonical);
public sealed record SyncBatchResult(IReadOnlyList<SyncEnvelopeResult> Results, DateTimeOffset ServerTime);

public class SyncBatchHandler : IRequestHandler<SyncBatchCommand, SyncBatchResult>
{
    private readonly IMediator _mediator;

    public SyncBatchHandler(IMediator mediator) => _mediator = mediator;

    public async Task<SyncBatchResult> Handle(SyncBatchCommand cmd, CancellationToken ct)
    {
        var results = new List<SyncEnvelopeResult>(cmd.Envelopes.Count);

        foreach (var env in cmd.Envelopes)
        {
            try
            {
                switch (env.Resource)
                {
                    case "sale":
                    {
                        var req = env.Payload.Deserialize<CreateSaleRequest>(JsonOpts.Default)!;
                        var res = await _mediator.Send(new CreateSaleCommand(req, env.IdempotencyKey), ct);
                        if (res.IsSuccess)
                            results.Add(new(env.IdempotencyKey, "accepted", null, null,
                                JsonSerializer.SerializeToElement(res.Value, JsonOpts.Default)));
                        else
                            results.Add(new(env.IdempotencyKey,
                                res.Error.Type == ErrorType.Conflict ? "conflict" : "rejected",
                                res.Error.Code, res.Error.Message, null));
                        break;
                    }
                    default:
                        results.Add(new(env.IdempotencyKey, "rejected", "sync.resource.unknown",
                            $"Unknown resource '{env.Resource}'", null));
                        break;
                }
            }
            catch (Exception ex)
            {
                results.Add(new(env.IdempotencyKey, "rejected", "sync.exception", ex.Message, null));
            }
        }

        return new SyncBatchResult(results, DateTimeOffset.UtcNow);
    }
}

internal static class JsonOpts
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web);
}
