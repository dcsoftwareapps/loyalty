using System.Net.Http.Json;

namespace LoyaltyCloud.Cashier.Services;

public sealed class AuthenticatedCashierApiClient
{
    private readonly HttpClient _http;

    public AuthenticatedCashierApiClient(HttpClient http)
    {
        _http = http;
    }

    public Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken ct = default) =>
        _http.GetAsync(requestUri, ct);

    public Task<HttpResponseMessage> PostAsJsonAsync<TValue>(
        string requestUri,
        TValue value,
        CancellationToken ct = default) =>
        _http.PostAsJsonAsync(requestUri, value, ct);
}
