using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Infrastructure.Services;

/// <summary>
/// Cliente HTTP/2 para Apple Push Notification Service.
/// Genera JWT ES256 con la llave .p8 y comparte su caché de ~50 min entre instancias.
/// </summary>
/// <remarks>
/// Para pases, Apple solo necesita un push background con cuerpo vacío <c>{}</c>;
/// el push dispara que Wallet vuelva a llamar el endpoint del pase y refresque
/// los campos.
/// </remarks>
internal sealed class ApnService : IApnService
{
    private readonly HttpClient _http;
    private readonly IAppleWalletSecretsProvider _secrets;
    private readonly ApplePassOptions _options;
    private readonly ILogger<ApnService> _logger;

    private readonly ApnProviderTokenCache _tokens;

    public ApnService(
        HttpClient http,
        IAppleWalletSecretsProvider secrets,
        IOptions<ApplePassOptions> options,
        ILogger<ApnService> logger,
        ApnProviderTokenCache tokens)
    {
        _http = http;
        _secrets = secrets;
        _options = options.Value;
        _logger = logger;
        _tokens = tokens;
    }

    /// <inheritdoc />
    public async Task<ApnPushResult> SendPassUpdateAsync(string pushToken, PassUpdateReason reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pushToken))
            throw new ArgumentException("PushToken requerido.", nameof(pushToken));

        try
        {
            var jwt = await GetJwtAsync(ct);
            var url = $"{_options.ApnHost.TrimEnd('/')}/3/device/{pushToken}";

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Version = HttpVersion.Version20,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("bearer", jwt);
            request.Headers.TryAddWithoutValidation("apns-topic", _options.PassTypeIdentifier);
            request.Headers.TryAddWithoutValidation("apns-push-type", "background");
            request.Headers.TryAddWithoutValidation("apns-priority", "5");

            _logger.LogInformation(
                "APNs request sending. reason={Reason}, host={Host}, topic={Topic}, pushType=background, priority=5, token={Token}.",
                reason,
                _options.ApnHost,
                _options.PassTypeIdentifier,
                SafePushToken(pushToken));

            using var response = await _http.SendAsync(request, ct);
            var body = response.IsSuccessStatusCode
                ? null
                : await response.Content.ReadAsStringAsync(ct);
            var apnReason = TryReadApnReason(body);

            _logger.LogInformation(
                "APNs response received. reason={Reason}, status={Status}, apnsReason={ApnsReason}, token={Token}.",
                reason,
                response.StatusCode,
                apnReason ?? "none",
                SafePushToken(pushToken));

            if (response.StatusCode == HttpStatusCode.OK)
            {
                _logger.LogDebug("APN push OK ({Reason}) → {Token:0.10}…", reason, pushToken);
                return ApnPushResult.Accepted((int)response.StatusCode);
            }

            _logger.LogWarning(
                "APN push rejected for token {Token}: status={Status}, apnsReason={ApnsReason}, body={Body}",
                SafePushToken(pushToken),
                response.StatusCode,
                apnReason ?? "none",
                body);

            return IsTransient(response.StatusCode, apnReason)
                ? ApnPushResult.Transient((int)response.StatusCode, apnReason ?? response.StatusCode.ToString())
                : ApnPushResult.Permanent((int)response.StatusCode, apnReason ?? response.StatusCode.ToString());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "APNs transient failure. reason={Reason}, token={Token}.",
                reason,
                SafePushToken(pushToken));
            return ApnPushResult.Transient(null, "Timeout");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "APNs transient network failure. reason={Reason}, token={Token}.",
                reason,
                SafePushToken(pushToken));
            return ApnPushResult.Transient(null, ex.GetType().Name);
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode, string? apnReason) =>
        statusCode == (HttpStatusCode)429 || (int)statusCode >= 500;

    private static string? TryReadApnReason(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("reason", out var reason)
                ? reason.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Task<string> GetJwtAsync(CancellationToken ct) =>
        _tokens.GetOrCreateAsync(async token =>
        {
            // This factory uses the current service's scoped provider only for this call.
            var keyPem = await _secrets.GetApnPrivateKeyPemAsync(token);
            var keyId = await _secrets.GetApnKeyIdAsync(token);
            var teamId = await _secrets.GetApnTeamIdAsync(token);
            return BuildJwt(keyPem, keyId, teamId);
        }, ct);

    /// <summary>
    /// Construye un JWT ES256 según especificación de Apple para APN:
    /// header <c>{ alg: ES256, kid: &lt;key-id&gt; }</c>,
    /// payload <c>{ iss: &lt;team-id&gt;, iat: &lt;unix-now&gt; }</c>,
    /// firma con ECDSA P-256 sobre <c>SHA-256(header.payload)</c>.
    /// </summary>
    private static string BuildJwt(string privateKeyPem, string keyId, string teamId)
    {
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var header = new { alg = "ES256", kid = keyId, typ = "JWT" };
        var payload = new { iss = teamId, iat = nowUnix };

        var headerBytes = JsonSerializer.SerializeToUtf8Bytes(header);
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);

        var headerB64 = Base64UrlEncode(headerBytes);
        var payloadB64 = Base64UrlEncode(payloadBytes);
        var signingInput = $"{headerB64}.{payloadB64}";

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKeyPem);

        var signature = ecdsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"{signingInput}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string SafePushToken(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? "empty"
            : value.Length <= 12
                ? $"{value[..Math.Min(value.Length, 4)]}..."
                : $"{value[..6]}...{value[^6..]}";
}
