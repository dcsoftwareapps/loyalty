using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Security.KeyVault.Secrets;
using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KBeauty.Loyalty.Infrastructure.Services;

/// <summary>
/// Cliente HTTP/2 para Apple Push Notification Service.
/// Genera JWT ES256 con la llave .p8 y lo cachea ~50 min (Apple expira a 60 min).
/// </summary>
/// <remarks>
/// Para pases, Apple solo necesita un push background con cuerpo vacío <c>{}</c>;
/// el push dispara que Wallet vuelva a llamar el endpoint del pase y refresque
/// los campos.
/// </remarks>
internal sealed class ApnService : IApnService
{
    private const string SecretPrivateKey = "kbeauty-apn-private-key";
    private const string SecretKeyId = "kbeauty-apn-key-id";
    private const string SecretTeamId = "kbeauty-apn-team-id";

    private readonly HttpClient _http;
    private readonly SecretClient _kv;
    private readonly ApplePassOptions _options;
    private readonly ILogger<ApnService> _logger;

    // Cache del JWT — protegido con SemaphoreSlim para evitar regenerar en paralelo.
    private string? _cachedJwt;
    private DateTime _jwtExpiresAt = DateTime.MinValue;
    private readonly SemaphoreSlim _jwtLock = new(1, 1);

    private static readonly TimeSpan JwtLifetime = TimeSpan.FromMinutes(50);

    public ApnService(
        HttpClient http,
        SecretClient kv,
        IOptions<ApplePassOptions> options,
        ILogger<ApnService> logger)
    {
        _http = http;
        _kv = kv;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendPassUpdateAsync(string pushToken, PassUpdateReason reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pushToken))
            throw new ArgumentException("PushToken requerido.", nameof(pushToken));

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

        using var response = await _http.SendAsync(request, ct);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogDebug("APN push OK ({Reason}) → {Token:0.10}…", reason, pushToken);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogWarning(
            "APN push falló para token {Token:0.10}…: {Status} {Body}",
            pushToken, response.StatusCode, body);

        // No lanzamos — el caller (handler) trata el push como best-effort.
    }

    private async Task<string> GetJwtAsync(CancellationToken ct)
    {
        if (_cachedJwt is not null && DateTime.UtcNow < _jwtExpiresAt)
            return _cachedJwt;

        await _jwtLock.WaitAsync(ct);
        try
        {
            if (_cachedJwt is not null && DateTime.UtcNow < _jwtExpiresAt)
                return _cachedJwt;

            var keyPem = (await _kv.GetSecretAsync(SecretPrivateKey, cancellationToken: ct)).Value.Value;
            var keyId = (await _kv.GetSecretAsync(SecretKeyId, cancellationToken: ct)).Value.Value;
            var teamId = (await _kv.GetSecretAsync(SecretTeamId, cancellationToken: ct)).Value.Value;

            _cachedJwt = BuildJwt(keyPem, keyId, teamId);
            _jwtExpiresAt = DateTime.UtcNow.Add(JwtLifetime);
            return _cachedJwt;
        }
        finally
        {
            _jwtLock.Release();
        }
    }

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
}
