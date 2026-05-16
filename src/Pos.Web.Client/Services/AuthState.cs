using Pos.Shared.Contracts;

namespace Pos.Web.Client.Services;

public class AuthState
{
    private readonly TokenStore _store;
    public event Action? Changed;

    public AuthState(TokenStore store) => _store = store;

    public LoginResponse? Current { get; private set; }
    public bool IsAuthenticated => Current is not null
                                   && Current.AccessExpiresAt > DateTimeOffset.UtcNow.AddSeconds(-5);

    public async Task LoadAsync()
    {
        Current = await _store.GetAsync();
        Changed?.Invoke();
    }

    public async Task SetAsync(LoginResponse r)
    {
        Current = r;
        await _store.SetAsync(r);
        Changed?.Invoke();
    }

    public async Task ClearAsync()
    {
        Current = null;
        await _store.ClearAsync();
        Changed?.Invoke();
    }

    public bool Has(string permission) =>
        Current?.Permissions?.Contains(permission, StringComparer.OrdinalIgnoreCase) == true;
}
