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
        if (!IsLoyaltyCloudApiRequest(request.RequestUri)) return await base.SendAsync(request,cancellationToken);
        var origin = _sessions.Current;
        await _sessions.EnsureAccessAsync(cancellationToken);
        var session=_sessions.Current;
        if (origin is not null && (session is null || !origin.IsSameSession(session)))
            return new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = request };
        if (session is not null) request.Headers.Authorization=new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var response=await base.SendAsync(request,cancellationToken);
        if (response.StatusCode==HttpStatusCode.Unauthorized && session is not null)
        {
            var current = _sessions.Current;
            if (current is null || !session.IsSameSession(current)) return response;
            if (await _sessions.RefreshAfterUnauthorizedAsync(session.AccessToken,cancellationToken))
            {
                var renewed = _sessions.Current;
                if (renewed is null || !session.IsSameSession(renewed)) return response;
                // A 401 originates before execution. Reuse the SAME buffered payload/key once.
                using var retry=new HttpRequestMessage(request.Method,request.RequestUri);
                foreach(var header in request.Headers) retry.Headers.TryAddWithoutValidation(header.Key,header.Value);
                if (request.Content is not null)
                {
                    retry.Content=new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(cancellationToken));
                    foreach(var header in request.Content.Headers) retry.Content.Headers.TryAddWithoutValidation(header.Key,header.Value);
                }
                current = _sessions.Current;
                if (current is null || !session.IsSameSession(current)) return response;
                // Never obtain credentials from a later login, even if it races this check.
                var retryToken=renewed.AccessToken;
                retry.Headers.Authorization=new AuthenticationHeaderValue("Bearer",retryToken);
                response.Dispose();
                response=await base.SendAsync(retry,cancellationToken);
                if (response.StatusCode==HttpStatusCode.Unauthorized) await _sessions.ClearAfterUnauthorizedAsync(retryToken);
            }
        }

        return response;
    }

    private bool IsLoyaltyCloudApiRequest(Uri? requestUri)
    {
        if (requestUri is null)
            return false;

        var absolute = requestUri.IsAbsoluteUri ? requestUri : new Uri(_options.BaseUri, requestUri);
        return absolute.Scheme.Equals(_options.BaseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && absolute.Host.Equals(_options.BaseUri.Host, StringComparison.OrdinalIgnoreCase)
            && absolute.Port == _options.BaseUri.Port;
    }
}
