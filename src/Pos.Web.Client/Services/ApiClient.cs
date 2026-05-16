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

    // ---- Shops ----
    public async Task<IReadOnlyList<ShopDto>> ListShopsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<IReadOnlyList<ShopDto>>("/api/v1/shops", ct)
            ?? Array.Empty<ShopDto>();

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
