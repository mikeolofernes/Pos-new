using System.Net.Http.Json;
using Pos.Shared.Contracts;

namespace Pos.Web.Client.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    public ApiClient(HttpClient http) => _http = http;

    public HttpClient Raw => _http;

    // ---- Auth ----
    public async Task<LoginResponse?> LoginAsync(LoginRequest body, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("/api/v1/auth/login", body, ct);
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct);
    }

    // ---- Tenants ----
    public async Task<IReadOnlyList<TenantDto>> ListTenantsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<IReadOnlyList<TenantDto>>("/api/v1/tenants", ct)
            ?? Array.Empty<TenantDto>();

    public async Task<TenantDto?> CreateTenantAsync(CreateTenantRequest body, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("/api/v1/tenants", body, ct);
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadFromJsonAsync<TenantDto>(cancellationToken: ct);
    }

    // ---- Shops ----
    public async Task<IReadOnlyList<ShopDto>> ListShopsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<IReadOnlyList<ShopDto>>("/api/v1/shops", ct)
            ?? Array.Empty<ShopDto>();

    public async Task<ShopDto?> CreateShopAsync(CreateShopRequest body, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("/api/v1/shops", body, ct);
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadFromJsonAsync<ShopDto>(cancellationToken: ct);
    }

    public async Task<ShopDto?> UpdateShopAsync(Guid id, UpdateShopRequest body, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"/api/v1/shops/{id}", body, ct);
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadFromJsonAsync<ShopDto>(cancellationToken: ct);
    }

    public async Task<bool> DeleteShopAsync(Guid id, CancellationToken ct = default) =>
        (await _http.DeleteAsync($"/api/v1/shops/{id}", ct)).IsSuccessStatusCode;

    // ---- Users ----
    public async Task<IReadOnlyList<UserDto>> ListUsersAsync(string? q = null, CancellationToken ct = default)
    {
        var url = "/api/v1/users" + (string.IsNullOrWhiteSpace(q) ? "" : $"?q={Uri.EscapeDataString(q)}");
        return await _http.GetFromJsonAsync<IReadOnlyList<UserDto>>(url, ct) ?? Array.Empty<UserDto>();
    }

    public async Task<UserDto?> CreateUserAsync(CreateUserRequest body, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("/api/v1/users", body, ct);
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadFromJsonAsync<UserDto>(cancellationToken: ct);
    }

    public async Task<UserDto?> UpdateUserAsync(Guid id, UpdateUserRequest body, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"/api/v1/users/{id}", body, ct);
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadFromJsonAsync<UserDto>(cancellationToken: ct);
    }

    public async Task<bool> DeleteUserAsync(Guid id, CancellationToken ct = default) =>
        (await _http.DeleteAsync($"/api/v1/users/{id}", ct)).IsSuccessStatusCode;

    // ---- Categories ----
    public async Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<IReadOnlyList<CategoryDto>>("/api/v1/categories", ct)
            ?? Array.Empty<CategoryDto>();

    public async Task<CategoryDto?> CreateCategoryAsync(CreateCategoryRequest body, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("/api/v1/categories", body, ct);
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadFromJsonAsync<CategoryDto>(cancellationToken: ct);
    }

    public async Task<CategoryDto?> UpdateCategoryAsync(Guid id, UpdateCategoryRequest body, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"/api/v1/categories/{id}", body, ct);
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadFromJsonAsync<CategoryDto>(cancellationToken: ct);
    }

    public async Task<bool> DeleteCategoryAsync(Guid id, CancellationToken ct = default) =>
        (await _http.DeleteAsync($"/api/v1/categories/{id}", ct)).IsSuccessStatusCode;

    // ---- Quick selects (POS tiles) ----
    public async Task<IReadOnlyList<QuickSelectDto>> ListQuickSelectsAsync(Guid? shopId = null, CancellationToken ct = default)
    {
        var url = "/api/v1/quick-selects" + (shopId is null ? "" : $"?shopId={shopId}");
        return await _http.GetFromJsonAsync<IReadOnlyList<QuickSelectDto>>(url, ct) ?? Array.Empty<QuickSelectDto>();
    }

    public async Task<QuickSelectDto?> CreateQuickSelectAsync(CreateQuickSelectRequest body, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("/api/v1/quick-selects", body, ct);
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadFromJsonAsync<QuickSelectDto>(cancellationToken: ct);
    }

    public async Task<QuickSelectDto?> UpdateQuickSelectAsync(Guid id, UpdateQuickSelectRequest body, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"/api/v1/quick-selects/{id}", body, ct);
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadFromJsonAsync<QuickSelectDto>(cancellationToken: ct);
    }

    public async Task<bool> DeleteQuickSelectAsync(Guid id, CancellationToken ct = default) =>
        (await _http.DeleteAsync($"/api/v1/quick-selects/{id}", ct)).IsSuccessStatusCode;

    // ---- Products ----
    public async Task<PageOf<ProductDto>?> ListProductsAsync(string? q = null, int page = 1, int pageSize = 100, CancellationToken ct = default)
    {
        var url = $"/api/v1/products?page={page}&pageSize={pageSize}"
                  + (string.IsNullOrWhiteSpace(q) ? "" : $"&q={Uri.EscapeDataString(q)}");
        return await _http.GetFromJsonAsync<PageOf<ProductDto>>(url, ct);
    }

    public async Task<ProductDto?> CreateProductAsync(CreateProductRequest body, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("/api/v1/products", body, ct);
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadFromJsonAsync<ProductDto>(cancellationToken: ct);
    }

    public async Task<ProductDto?> UpdateProductAsync(Guid id, UpdateProductRequest body, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"/api/v1/products/{id}", body, ct);
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadFromJsonAsync<ProductDto>(cancellationToken: ct);
    }

    public async Task<bool> DeleteProductAsync(Guid id, CancellationToken ct = default) =>
        (await _http.DeleteAsync($"/api/v1/products/{id}", ct)).IsSuccessStatusCode;

    // ---- Inventory ----
    public async Task<IReadOnlyList<BalanceDto>> GetBalanceAsync(Guid productId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<IReadOnlyList<BalanceDto>>($"/api/v1/inventory/{productId}/balance", ct)
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
        if (!r.IsSuccessStatusCode) return null;
        var doc = await r.Content.ReadFromJsonAsync<ShiftIdResponse>(cancellationToken: ct);
        return doc?.Id;
    }

    public async Task<CloseShiftResponse?> CloseShiftAsync(Guid shiftId, decimal closingDeclared, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync($"/api/v1/pos/shifts/{shiftId}/close?closingDeclared={closingDeclared}", new { }, ct);
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadFromJsonAsync<CloseShiftResponse>(cancellationToken: ct);
    }

    // ---- Sync ----
    public async Task<SyncBatchResponse?> SyncBatchAsync(IReadOnlyList<SyncEnvelopeOut> envelopes, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("/api/v1/sync/batch", new { Envelopes = envelopes }, ct);
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadFromJsonAsync<SyncBatchResponse>(cancellationToken: ct);
    }

    private sealed record ShiftIdResponse(Guid Id);
}
