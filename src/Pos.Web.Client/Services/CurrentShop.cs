using Microsoft.JSInterop;

namespace Pos.Web.Client.Services;

/// <summary>
/// Holds the cashier's current shop. Read by AuthHeaderHandler to send
/// X-Shop-Id on every API request, which scopes server-side queries.
/// Persisted to localStorage so it survives page reloads.
/// </summary>
public class CurrentShop
{
    private const string Key = "pos.currentShop";
    private readonly IJSRuntime _js;
    private bool _loaded;
    public Guid? ShopId { get; private set; }
    public event Action? Changed;

    public CurrentShop(IJSRuntime js) => _js = js;

    public async Task LoadAsync()
    {
        if (_loaded) return;
        try
        {
            var v = await _js.InvokeAsync<string?>("localStorage.getItem", Key);
            if (Guid.TryParse(v, out var g)) ShopId = g;
        }
        catch { /* JS not available (prerender) — ignore */ }
        _loaded = true;
    }

    public async Task SetAsync(Guid? shopId)
    {
        ShopId = shopId;
        try
        {
            if (shopId is { } g)
                await _js.InvokeVoidAsync("localStorage.setItem", Key, g.ToString());
            else
                await _js.InvokeVoidAsync("localStorage.removeItem", Key);
        }
        catch { /* JS not available — ignore */ }
        Changed?.Invoke();
    }
}
