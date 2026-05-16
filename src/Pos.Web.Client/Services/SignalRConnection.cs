using Microsoft.AspNetCore.SignalR.Client;

namespace Pos.Web.Client.Services;

public class SignalRConnection : IAsyncDisposable
{
    private readonly TokenStore _store;
    private HubConnection? _hub;

    public SignalRConnection(TokenStore store) => _store = store;

    public event Action<string, object?>? OnEvent;

    public async Task StartAsync(Uri baseUri)
    {
        if (_hub is not null) return;
        var tokens = await _store.GetAsync();
        if (tokens is null) return;

        _hub = new HubConnectionBuilder()
            .WithUrl(new Uri(baseUri, "/hubs/pos"), opt =>
            {
                opt.AccessTokenProvider = async () => (await _store.GetAsync())?.AccessToken;
            })
            .WithAutomaticReconnect()
            .Build();

        _hub.On<object>("saleCommitted", payload => OnEvent?.Invoke("saleCommitted", payload));
        _hub.On<object>("stockChanged", payload => OnEvent?.Invoke("stockChanged", payload));

        await _hub.StartAsync();
    }

    public async Task JoinShopAsync(Guid shopId)
    {
        if (_hub is { State: HubConnectionState.Connected })
            await _hub.SendAsync("JoinShop", shopId.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null) await _hub.DisposeAsync();
    }
}
