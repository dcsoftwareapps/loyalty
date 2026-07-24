using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using LoyaltyCloud.Admin.Auth;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoyaltyCloud.Admin.Services;

public sealed class AdminApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IConfiguration _configuration;

    public AdminApiClient(
        IHttpClientFactory httpClientFactory,
        AuthenticationStateProvider authenticationStateProvider,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _authenticationStateProvider = authenticationStateProvider;
        _configuration = configuration;
    }

    public async Task<Result<TResponse>> GetAsync<TResponse>(
        string pathAndQuery,
        CancellationToken ct = default) =>
        await SendAsync<TResponse>(HttpMethod.Get, pathAndQuery, body: null, ct);

    public async Task<Result<TResponse>> PostAsync<TResponse>(
        string pathAndQuery,
        CancellationToken ct = default) =>
        await SendAsync<TResponse>(HttpMethod.Post, pathAndQuery, body: null, ct);

    public async Task<Result<TResponse>> PutAsync<TResponse>(
        string pathAndQuery,
        CancellationToken ct = default) =>
        await SendAsync<TResponse>(HttpMethod.Put, pathAndQuery, body: null, ct);

    public async Task<Result<TResponse>> PostAsJsonAsync<TRequest, TResponse>(
        string pathAndQuery,
        TRequest body,
        CancellationToken ct = default) =>
        await SendAsync<TResponse>(HttpMethod.Post, pathAndQuery, body, ct);

    private async Task<Result<TResponse>> SendAsync<TResponse>(
        HttpMethod method,
        string pathAndQuery,
        object? body,
        CancellationToken ct)
    {
        var request = await CreateSignedRequestAsync(method, pathAndQuery, body, ct);
        if (request.IsFailure)
            return Result.Fail<TResponse>(request.Error);

        var client = _httpClientFactory.CreateClient("LoyaltyCloudApi");
        using var response = await client.SendAsync(request.Value, ct);
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct);
            return value is null
                ? Result.Fail<TResponse>("La API no devolvio una respuesta valida.")
                : Result.Ok(value);
        }

        return Result.Fail<TResponse>(await ReadApiErrorAsync(response, ct));
    }

    private async Task<Result<HttpRequestMessage>> CreateSignedRequestAsync(
        HttpMethod method,
        string pathAndQuery,
        object? body,
        CancellationToken ct)
    {
        var principal = (await _authenticationStateProvider.GetAuthenticationStateAsync()).User;
        if (principal.Identity?.IsAuthenticated != true)
            return Result.Fail<HttpRequestMessage>("Sesion no autenticada.");

        var tenantSlug = principal.FindFirstValue(AdminClaimTypes.TenantSlug);
        if (string.IsNullOrWhiteSpace(tenantSlug))
            return Result.Fail<HttpRequestMessage>("No se encontro tenant en la sesion actual.");

        var operatorId = principal.FindFirstValue(AdminClaimTypes.Name)
            ?? principal.FindFirstValue(AdminClaimTypes.Subject)
            ?? "admin-panel";

        var sharedSecret = _configuration["AdminApi:SharedSecret"];
        if (string.IsNullOrWhiteSpace(sharedSecret))
            return Result.Fail<HttpRequestMessage>("Falta AdminApi:SharedSecret para invocar la API.");

        pathAndQuery = NormalizePathAndQuery(pathAndQuery);
        var bodyBytes = body is null
            ? []
            : JsonSerializer.SerializeToUtf8Bytes(body, JsonOptions);
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var signature = AdminApiSignature.CreateSignature(
            sharedSecret,
            method.Method,
            pathAndQuery,
            timestamp,
            tenantSlug,
            operatorId,
            bodyBytes);

        var request = new HttpRequestMessage(method, pathAndQuery);
        if (body is not null)
        {
            request.Content = new ByteArrayContent(bodyBytes);
            request.Content.Headers.ContentType = new("application/json");
        }

        request.Headers.Add(AdminApiSignature.TenantSlugHeader, tenantSlug);
        request.Headers.Add(AdminApiSignature.OperatorHeader, operatorId);
        request.Headers.Add(AdminApiSignature.TimestampHeader, timestamp);
        request.Headers.Add(AdminApiSignature.SignatureHeader, signature);
        return Result.Ok(request);
    }

    private static string NormalizePathAndQuery(string pathAndQuery) =>
        pathAndQuery.StartsWith("/", StringComparison.Ordinal)
            ? pathAndQuery
            : $"/{pathAndQuery}";

    private static async Task<string> ReadApiErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions, ct);
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
                return problem.Detail;
            if (!string.IsNullOrWhiteSpace(problem?.Title))
                return problem.Title;
        }
        catch
        {
            // Fall through to raw content.
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(content)
            ? $"La API respondio {(int)response.StatusCode} {response.ReasonPhrase}."
            : content;
    }
}
