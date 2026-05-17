using System.Net.Http.Json;
using System.Text.Json;
using Pos.Shared.Contracts;

namespace Pos.Web.Client.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    public ApiClient(HttpClient http) => _http = http;

    public HttpClient Raw => _http;

    /// <summary>Last server error message (from ProblemDetails.detail or status text).</summary>
    public string? LastError { get; private set; }

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    // In Blazor WASM the browser's fetch API disposes BrowserHttpContent as soon as the
    // first read completes. Buffer the body as a plain string immediately so we can branch
    // on the status code and deserialize without hitting a disposed-object exception.
    private static async Task<(bool Ok, string Body, string? Reason)> ReadAsync(
        HttpResponseMessage r, CancellationToken ct)
    {
        var body = await r.Content.ReadAsStringAsync(ct);
        return (r.IsSuccessStatusCode, body, r.ReasonPhrase);
    }

    private static T? Des<T>(string body) where T : class
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        return JsonSerializer.Deserialize<T>(body, _json);
    }

    private static string? ExtractError(string body, string? reason)
    {
        try
        {
            var pd = JsonSerializer.Deserialize<ProblemDetailsLite>(body, _json);
            if (!string.IsNullOrWhiteSpace(pd?.Detail)) return pd.Detail;
            if (!string.IsNullOrWhiteSpace(pd?.Title)) return pd.Title;
        }
        catch { /* not JSON */ }
        return reason;
    }

    private async Task<T?> SafeGetAsync<T>(string url, CancellationToken ct) where T : class
    {
        var r = await _http.GetAsync(url, ct);
        var (ok, body, _) = await ReadAsync(r, ct);
        return ok ? Des<T>(body) : null;
    }

    private async Task<T?> PostAsync<T>(string url, object payload, CancellationToken ct) where T : class
    {
        var r = await _http.PostAsJsonAsync(url, payload, ct);
        var (ok, body, reason) = await ReadAsync(r, ct);
        LastError = ok ? null : ExtractError(body, reason);
        return ok ? Des<T>(body) : null;
    }

    private async Task<T?> PutAsync<T>(string url, object payload, CancellationToken ct) where T : class
    {
        var r = await _http.PutAsJsonAsync(url, payload, ct);
        var (ok, body, reason) = await ReadAsync(r, ct);
        LastError = ok ? null : ExtractError(body, reason);
        return ok ? Des<T>(body) : null;
    }

    private sealed record ProblemDetailsLite(string? Title, string? Detail, int? Status);

    // ---- Auth ----
    public async Task<LoginResponse?> LoginAsync(LoginRequest body, CancellationToken ct = default)
    {
        LastError = null;
        var r = await _http.PostAsJsonAsync("/api/v1/auth/login", body, ct);
        var (ok, rb, reason) = await ReadAsync(r, ct);
        if (!ok) { LastError = ExtractError(rb, reason); return null; }
        return Des<LoginResponse>(rb);
    }

    // ---- Tenants ----
    public async Task<IReadOnlyList<TenantDto>> ListTenantsAsync(CancellationToken ct = default) =>
        await SafeGetAsync<IReadOnlyList<TenantDto>>("/api/v1/tenants", ct) ?? Array.Empty<TenantDto>();

    public async Task<TenantDto?> CreateTenantAsync(CreateTenantRequest body, CancellationToken ct = default) =>
        await PostAsync<TenantDto>("/api/v1/tenants", body, ct);

    // ---- Shops ----
    public async Task<IReadOnlyList<ShopDto>> ListShopsAsync(CancellationToken ct = default) =>
        await SafeGetAsync<IReadOnlyList<ShopDto>>("/api/v1/shops", ct) ?? Array.Empty<ShopDto>();

    public async Task<IReadOnlyList<ShopDto>> ListMyShopsAsync(CancellationToken ct = default) =>
        await SafeGetAsync<IReadOnlyList<ShopDto>>("/api/v1/shops/mine", ct) ?? Array.Empty<ShopDto>();

    public async Task<ShopDto?> CreateShopAsync(CreateShopRequest body, CancellationToken ct = default) =>
        await PostAsync<ShopDto>("/api/v1/shops", body, ct);

    public async Task<ShopDto?> UpdateShopAsync(Guid id, UpdateShopRequest body, CancellationToken ct = default) =>
        await PutAsync<ShopDto>($"/api/v1/shops/{id}", body, ct);

    public async Task<bool> DeleteShopAsync(Guid id, CancellationToken ct = default) =>
        (await _http.DeleteAsync($"/api/v1/shops/{id}", ct)).IsSuccessStatusCode;

    // ---- Users ----
    public async Task<IReadOnlyList<UserDto>> ListUsersAsync(string? q = null, CancellationToken ct = default)
    {
        var url = "/api/v1/users" + (string.IsNullOrWhiteSpace(q) ? "" : $"?q={Uri.EscapeDataString(q)}");
        return await SafeGetAsync<IReadOnlyList<UserDto>>(url, ct) ?? Array.Empty<UserDto>();
    }

    public async Task<UserDto?> CreateUserAsync(CreateUserRequest body, CancellationToken ct = default) =>
        await PostAsync<UserDto>("/api/v1/users", body, ct);

    public async Task<UserDto?> UpdateUserAsync(Guid id, UpdateUserRequest body, CancellationToken ct = default) =>
        await PutAsync<UserDto>($"/api/v1/users/{id}", body, ct);

    public async Task<bool> DeleteUserAsync(Guid id, CancellationToken ct = default) =>
        (await _http.DeleteAsync($"/api/v1/users/{id}", ct)).IsSuccessStatusCode;

    // ---- Roles ----
    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct = default) =>
        await SafeGetAsync<IReadOnlyList<RoleDto>>("/api/v1/roles", ct) ?? Array.Empty<RoleDto>();

    // ---- Categories ----
    public async Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken ct = default) =>
        await SafeGetAsync<IReadOnlyList<CategoryDto>>("/api/v1/categories", ct) ?? Array.Empty<CategoryDto>();

    public async Task<CategoryDto?> CreateCategoryAsync(CreateCategoryRequest body, CancellationToken ct = default) =>
        await PostAsync<CategoryDto>("/api/v1/categories", body, ct);

    public async Task<CategoryDto?> UpdateCategoryAsync(Guid id, UpdateCategoryRequest body, CancellationToken ct = default) =>
        await PutAsync<CategoryDto>($"/api/v1/categories/{id}", body, ct);

    public async Task<bool> DeleteCategoryAsync(Guid id, CancellationToken ct = default) =>
        (await _http.DeleteAsync($"/api/v1/categories/{id}", ct)).IsSuccessStatusCode;

    // ---- Quick selects (POS tiles) ----
    public async Task<IReadOnlyList<QuickSelectDto>> ListQuickSelectsAsync(CancellationToken ct = default) =>
        await SafeGetAsync<IReadOnlyList<QuickSelectDto>>("/api/v1/quick-selects", ct) ?? Array.Empty<QuickSelectDto>();

    public async Task<QuickSelectDto?> CreateQuickSelectAsync(CreateQuickSelectRequest body, CancellationToken ct = default) =>
        await PostAsync<QuickSelectDto>("/api/v1/quick-selects", body, ct);

    public async Task<QuickSelectDto?> UpdateQuickSelectAsync(Guid id, UpdateQuickSelectRequest body, CancellationToken ct = default) =>
        await PutAsync<QuickSelectDto>($"/api/v1/quick-selects/{id}", body, ct);

    public async Task<bool> DeleteQuickSelectAsync(Guid id, CancellationToken ct = default) =>
        (await _http.DeleteAsync($"/api/v1/quick-selects/{id}", ct)).IsSuccessStatusCode;

    // ---- Products ----
    public async Task<PageOf<ProductDto>?> ListProductsAsync(string? q = null, int page = 1, int pageSize = 100, CancellationToken ct = default)
    {
        var url = $"/api/v1/products?page={page}&pageSize={pageSize}"
                  + (string.IsNullOrWhiteSpace(q) ? "" : $"&q={Uri.EscapeDataString(q)}");
        return await SafeGetAsync<PageOf<ProductDto>>(url, ct);
    }

    public async Task<ProductDto?> CreateProductAsync(CreateProductRequest body, CancellationToken ct = default) =>
        await PostAsync<ProductDto>("/api/v1/products", body, ct);

    public async Task<ProductDto?> UpdateProductAsync(Guid id, UpdateProductRequest body, CancellationToken ct = default) =>
        await PutAsync<ProductDto>($"/api/v1/products/{id}", body, ct);

    public async Task<bool> DeleteProductAsync(Guid id, CancellationToken ct = default) =>
        (await _http.DeleteAsync($"/api/v1/products/{id}", ct)).IsSuccessStatusCode;

    // ---- Inventory ----
    public async Task<IReadOnlyList<BalanceDto>> GetBalanceAsync(Guid productId, CancellationToken ct = default) =>
        await SafeGetAsync<IReadOnlyList<BalanceDto>>($"/api/v1/inventory/{productId}/balance", ct)
            ?? Array.Empty<BalanceDto>();

    public async Task<HttpResponseMessage> AdjustStockAsync(AdjustStockRequest body, string idemKey, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/inventory/adjustments")
        {
            Content = JsonContent.Create(body)
        };
        req.Headers.Add("Idempotency-Key", idemKey);
        return await _http.SendAsync(req, ct);
    }

    // ---- POS ----
    public async Task<HttpResponseMessage> CommitSaleAsync(CreateSaleRequest body, string idemKey, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/pos/sales")
        {
            Content = JsonContent.Create(body)
        };
        req.Headers.Add("Idempotency-Key", idemKey);
        return await _http.SendAsync(req, ct);
    }

    public async Task<Guid?> OpenShiftAsync(OpenShiftRequest body, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("/api/v1/pos/shifts/open", body, ct);
        var (ok, rb, _) = await ReadAsync(r, ct);
        return ok ? Des<ShiftIdResponse>(rb)?.Id : null;
    }

    public async Task<CloseShiftResponse?> CloseShiftAsync(Guid shiftId, decimal closingDeclared, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync($"/api/v1/pos/shifts/{shiftId}/close?closingDeclared={closingDeclared}", new { }, ct);
        var (ok, rb, _) = await ReadAsync(r, ct);
        return ok ? Des<CloseShiftResponse>(rb) : null;
    }

    // ---- Sync ----
    public async Task<SyncBatchResponse?> SyncBatchAsync(IReadOnlyList<SyncEnvelopeOut> envelopes, CancellationToken ct = default) =>
        await PostAsync<SyncBatchResponse>("/api/v1/sync/batch", new { Envelopes = envelopes }, ct);

    private sealed record ShiftIdResponse(Guid Id);
}
