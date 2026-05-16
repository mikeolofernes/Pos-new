using Microsoft.JSInterop;

namespace Pos.Web.Client.Services;

/// <summary>
/// Thin wrapper around a JS-side IndexedDB helper (see wwwroot/js/idb.js).
/// Provides put/get/getAll/delete for offline catalog snapshot and outbox.
/// </summary>
public class IndexedDbInterop : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _module;

    public IndexedDbInterop(IJSRuntime js)
    {
        _module = new(() => js.InvokeAsync<IJSObjectReference>("import", "./js/idb.js").AsTask());
    }

    public async Task PutAsync<T>(string store, string key, T value)
    {
        var m = await _module.Value;
        await m.InvokeVoidAsync("put", store, key, value);
    }

    public async Task<T?> GetAsync<T>(string store, string key)
    {
        var m = await _module.Value;
        return await m.InvokeAsync<T?>("get", store, key);
    }

    public async Task<List<T>> GetAllAsync<T>(string store)
    {
        var m = await _module.Value;
        return await m.InvokeAsync<List<T>>("getAll", store);
    }

    public async Task DeleteAsync(string store, string key)
    {
        var m = await _module.Value;
        await m.InvokeVoidAsync("del", store, key);
    }

    public async Task ClearAsync(string store)
    {
        var m = await _module.Value;
        await m.InvokeVoidAsync("clear", store);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module.IsValueCreated)
        {
            var m = await _module.Value;
            await m.DisposeAsync();
        }
    }
}
