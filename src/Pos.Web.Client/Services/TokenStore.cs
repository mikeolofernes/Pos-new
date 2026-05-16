using Blazored.LocalStorage;
using Pos.Shared.Contracts;

namespace Pos.Web.Client.Services;

public class TokenStore
{
    private const string Key = "pos.auth";
    private readonly ILocalStorageService _ls;
    private LoginResponse? _cache;

    public TokenStore(ILocalStorageService ls) => _ls = ls;

    public async Task<LoginResponse?> GetAsync()
    {
        if (_cache is not null) return _cache;
        _cache = await _ls.GetItemAsync<LoginResponse?>(Key);
        return _cache;
    }

    public async Task SetAsync(LoginResponse value)
    {
        _cache = value;
        await _ls.SetItemAsync(Key, value);
    }

    public async Task ClearAsync()
    {
        _cache = null;
        await _ls.RemoveItemAsync(Key);
    }
}
