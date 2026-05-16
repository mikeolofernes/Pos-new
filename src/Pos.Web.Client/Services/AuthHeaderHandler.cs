using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Pos.Shared.Contracts;

namespace Pos.Web.Client.Services;

public class AuthHeaderHandler : DelegatingHandler
{
    private readonly TokenStore _store;
    private readonly IHttpClientFactory _factory;
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);

    public AuthHeaderHandler(TokenStore store, IHttpClientFactory factory)
    {
        _store = store;
        _factory = factory;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var tokens = await _store.GetAsync();
        if (tokens is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await base.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized && tokens is not null)
        {
            response.Dispose();
            var refreshed = await TryRefreshAsync(tokens, ct);
            if (refreshed is not null)
            {
                var retry = await CloneAsync(request);
                retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
                return await base.SendAsync(retry, ct);
            }
        }

        return response;
    }

    private async Task<LoginResponse?> TryRefreshAsync(LoginResponse current, CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct);
        try
        {
            var fresh = await _store.GetAsync();
            if (fresh is not null && fresh.AccessExpiresAt > DateTimeOffset.UtcNow.AddSeconds(5))
                return fresh;

            var http = _factory.CreateClient("auth-raw");
            var resp = await http.PostAsJsonAsync("/api/v1/auth/refresh",
                new RefreshRequest(current.RefreshToken), ct);
            if (!resp.IsSuccessStatusCode) return null;
            var newTokens = await resp.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct);
            if (newTokens is not null) await _store.SetAsync(newTokens);
            return newTokens;
        }
        catch
        {
            return null;
        }
        finally { _refreshLock.Release(); }
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri) { Version = req.Version };
        foreach (var h in req.Headers) clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
        if (req.Content is not null)
        {
            var bytes = await req.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);
            foreach (var h in req.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }
        return clone;
    }
}
