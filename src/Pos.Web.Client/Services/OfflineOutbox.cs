using Pos.Shared.Contracts;

namespace Pos.Web.Client.Services;

public sealed record OutboxItem(
    string IdempotencyKey, string Resource, string Op,
    object Payload, DateTimeOffset CreatedAt, long ClientSeq);

/// <summary>
/// Queues outbound mutations (sales, adjustments) in IndexedDB and drains
/// them through /api/v1/sync/batch when online.
/// </summary>
public class OfflineOutbox
{
    private const string Store = "outbox";
    private readonly IndexedDbInterop _idb;
    private readonly ApiClient _api;
    private long _seq;

    public OfflineOutbox(IndexedDbInterop idb, ApiClient api)
    {
        _idb = idb;
        _api = api;
        _seq = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public async Task EnqueueAsync(string resource, string op, object payload)
    {
        var item = new OutboxItem(
            IdempotencyKey: Guid.NewGuid().ToString("N"),
            Resource: resource,
            Op: op,
            Payload: payload,
            CreatedAt: DateTimeOffset.UtcNow,
            ClientSeq: Interlocked.Increment(ref _seq));
        await _idb.PutAsync(Store, item.IdempotencyKey, item);
    }

    public async Task<int> PendingCountAsync() => (await _idb.GetAllAsync<OutboxItem>(Store)).Count;

    /// <summary>
    /// Drains the outbox by batching up to <paramref name="batchSize"/> envelopes
    /// to the server. Successful and conflicted items are removed.
    /// </summary>
    public async Task<int> DrainAsync(Guid deviceId, int batchSize = 50, CancellationToken ct = default)
    {
        var items = await _idb.GetAllAsync<OutboxItem>(Store);
        if (items.Count == 0) return 0;

        int drained = 0;
        foreach (var chunk in items.OrderBy(i => i.ClientSeq).Chunk(batchSize))
        {
            var envelopes = chunk.Select(c => new SyncEnvelopeOut(
                c.IdempotencyKey, c.Resource, c.Op, c.Payload, c.CreatedAt, deviceId, c.ClientSeq)).ToList();

            var resp = await _api.SyncBatchAsync(envelopes, ct);
            if (resp is null) break;

            foreach (var r in resp.Results)
            {
                // accepted / conflict (dedupe) -> remove from outbox
                if (r.Status is "accepted" or "conflict")
                {
                    await _idb.DeleteAsync(Store, r.IdempotencyKey);
                    drained++;
                }
                // rejected items remain for manual review
            }
        }
        return drained;
    }
}
