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
}
