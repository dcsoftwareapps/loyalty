using System.Net;
using System.Net.Http.Headers;

namespace LoyaltyCloud.Cashier.Services;

public sealed class CashierAuthorizationHandler : DelegatingHandler
{
    private readonly CashierSessionService _sessions;
    private readonly CashierApiOptions _options;

    public CashierAuthorizationHandler(CashierSessionService sessions, CashierApiOptions options)
    {
        _sessions = sessions;
        _options = options;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var session = _sessions.Current;
        if (session is not null && IsLoyaltyCloudApiRequest(request.RequestUri))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            await _sessions.ClearAfterUnauthorizedAsync();

        return response;
    }

    private bool IsLoyaltyCloudApiRequest(Uri? requestUri)
    {
        if (requestUri is null)
            return false;

        var absolute = requestUri.IsAbsoluteUri ? requestUri : new Uri(_options.BaseUri, requestUri);
        return absolute.Host.Equals(_options.BaseUri.Host, StringComparison.OrdinalIgnoreCase);
    }
}
