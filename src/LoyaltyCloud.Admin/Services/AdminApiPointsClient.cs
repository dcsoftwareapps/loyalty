using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using LoyaltyCloud.Admin.Auth;
using LoyaltyCloud.Application.Points.Commands.AddPoints;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoyaltyCloud.Admin.Services;

public sealed class AdminApiPointsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IConfiguration _configuration;

    public AdminApiPointsClient(
        IHttpClientFactory httpClientFactory,
        AuthenticationStateProvider authenticationStateProvider,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _authenticationStateProvider = authenticationStateProvider;
        _configuration = configuration;
    }

    public async Task<Result<AddPointsResponse>> AddPointsAsync(
        string serialNumber,
        decimal purchaseAmount,
        CancellationToken cancellationToken = default)
    {
        var principal = (await _authenticationStateProvider.GetAuthenticationStateAsync()).User;
        if (principal.Identity?.IsAuthenticated != true)
            return Result.Fail<AddPointsResponse>("Sesion no autenticada.");

        var tenantSlug = principal.FindFirstValue(AdminClaimTypes.TenantSlug);
        if (string.IsNullOrWhiteSpace(tenantSlug))
            return Result.Fail<AddPointsResponse>("No se encontro tenant en la sesion actual.");

        var operatorId = principal.FindFirstValue(AdminClaimTypes.Name)
            ?? principal.FindFirstValue(AdminClaimTypes.Subject)
            ?? "admin-panel";

        var sharedSecret = _configuration["AdminApi:SharedSecret"];
        if (string.IsNullOrWhiteSpace(sharedSecret))
            return Result.Fail<AddPointsResponse>("Falta AdminApi:SharedSecret para invocar la API.");

        var body = JsonSerializer.SerializeToUtf8Bytes(
            new AddPointsApiRequest(serialNumber, purchaseAmount),
            JsonOptions);
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        const string path = "/api/points";
        var signature = AdminApiSignature.CreateSignature(
            sharedSecret,
            HttpMethod.Post.Method,
            path,
            timestamp,
            tenantSlug,
            operatorId,
            body);

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new("application/json");
        request.Headers.Add(AdminApiSignature.TenantSlugHeader, tenantSlug);
        request.Headers.Add(AdminApiSignature.OperatorHeader, operatorId);
        request.Headers.Add(AdminApiSignature.TimestampHeader, timestamp);
        request.Headers.Add(AdminApiSignature.SignatureHeader, signature);

        var client = _httpClientFactory.CreateClient("LoyaltyCloudApi");
        using var response = await client.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<AddPointsResponse>(JsonOptions, cancellationToken);
            return result is null
                ? Result.Fail<AddPointsResponse>("La API no devolvio una respuesta valida.")
                : Result.Ok(result);
        }

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions, cancellationToken);
        return Result.Fail<AddPointsResponse>(
            problem?.Detail
            ?? problem?.Title
            ?? $"La API rechazo la compra con status {(int)response.StatusCode}.");
    }

    private sealed record AddPointsApiRequest(string SerialNumber, decimal PurchaseAmount);
}
