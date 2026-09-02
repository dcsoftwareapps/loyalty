using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

internal sealed class GoogleWalletClient : IGoogleWalletClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly IGoogleWalletCredentialsProvider _credentialsProvider;
    private readonly GoogleWalletJwtFactory _jwtFactory;
    private readonly GoogleWalletObjectMapper _mapper;
    private readonly GoogleWalletOptions _options;
    private readonly IDateTimeProvider _dt;
    private readonly ILogger<GoogleWalletClient> _logger;
    private string? _accessToken;
    private DateTime _accessTokenExpiresAtUtc;

    public GoogleWalletClient(
        HttpClient http,
        IGoogleWalletCredentialsProvider credentialsProvider,
        GoogleWalletJwtFactory jwtFactory,
        GoogleWalletObjectMapper mapper,
        IOptions<GoogleWalletOptions> options,
        IDateTimeProvider dt,
        ILogger<GoogleWalletClient> logger)
    {
        _http = http;
        _credentialsProvider = credentialsProvider;
        _jwtFactory = jwtFactory;
        _mapper = mapper;
        _options = options.Value;
        _dt = dt;
        _logger = logger;
    }

    public async Task EnsureLoyaltyClassAsync(GoogleWalletClassData walletClass, CancellationToken ct = default)
    {
        var existing = await SendAsync(HttpMethod.Get, $"loyaltyClass/{Uri.EscapeDataString(walletClass.Id)}", null, ct);
        if (existing.StatusCode == HttpStatusCode.OK)
        {
            var patched = await SendAsync(
                new HttpMethod("PATCH"),
                $"loyaltyClass/{Uri.EscapeDataString(walletClass.Id)}",
                _mapper.ToClassPayload(
                    walletClass,
                    includeProgramLogo: string.IsNullOrWhiteSpace(walletClass.WideLogoUri)),
                ct);
            if (patched.StatusCode == HttpStatusCode.OK)
                return;

            throw await CreateExceptionAsync("actualizar LoyaltyClass", patched, ct);
        }

        if (existing.StatusCode == HttpStatusCode.NotFound)
        {
            var created = await SendAsync(HttpMethod.Post, "loyaltyClass", _mapper.ToClassPayload(walletClass), ct);
            if (created.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
                return;

            if (created.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogInformation("Google Wallet LoyaltyClass {ClassId} already exists after create conflict.", walletClass.Id);
                return;
            }

            throw await CreateExceptionAsync("crear LoyaltyClass", created, ct);
        }

        throw await CreateExceptionAsync("consultar LoyaltyClass", existing, ct);
    }

    public async Task CreateOrUpdateObjectAsync(GoogleWalletObjectData walletObject, CancellationToken ct = default)
    {
        var existing = await SendAsync(HttpMethod.Get, $"loyaltyObject/{Uri.EscapeDataString(walletObject.Id)}", null, ct);
        if (existing.StatusCode == HttpStatusCode.NotFound)
        {
            var created = await SendAsync(HttpMethod.Post, "loyaltyObject", _mapper.ToObjectPayload(walletObject), ct);
            if (created.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
                return;

            if (created.StatusCode != HttpStatusCode.Conflict)
                throw await CreateExceptionAsync("crear LoyaltyObject", created, ct);
        }
        else if (existing.StatusCode != HttpStatusCode.OK)
        {
            throw await CreateExceptionAsync("consultar LoyaltyObject", existing, ct);
        }

        var updated = await SendAsync(
            new HttpMethod("PATCH"),
            $"loyaltyObject/{Uri.EscapeDataString(walletObject.Id)}",
            _mapper.ToObjectPayload(walletObject),
            ct);
        if (updated.StatusCode is HttpStatusCode.OK)
            return;

        throw await CreateExceptionAsync("actualizar LoyaltyObject", updated, ct);
    }

    public async Task EnsureGiftCardClassAsync(GoogleGiftCardClassData walletClass, CancellationToken ct = default)
    {
        var existing = await SendAsync(HttpMethod.Get, $"genericClass/{Uri.EscapeDataString(walletClass.Id)}", null, ct);
        if (existing.StatusCode == HttpStatusCode.OK)
        {
            var patched = await SendAsync(
                new HttpMethod("PATCH"),
                $"genericClass/{Uri.EscapeDataString(walletClass.Id)}",
                new { issuerName = walletClass.IssuerName },
                ct);
            if (patched.StatusCode == HttpStatusCode.OK) return;
            throw await CreateExceptionAsync("actualizar GenericClass Gift Card", patched, ct);
        }
        if (existing.StatusCode != HttpStatusCode.NotFound) throw await CreateExceptionAsync("consultar GenericClass Gift Card", existing, ct);
        var payload = new { id = walletClass.Id, issuerName = walletClass.IssuerName };
        var created = await SendAsync(HttpMethod.Post, "genericClass", payload, ct);
        if (created.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Created or HttpStatusCode.Conflict))
            throw await CreateExceptionAsync("crear GenericClass Gift Card", created, ct);
    }

    public async Task CreateOrUpdateGiftCardObjectAsync(GoogleGiftCardObjectData value, CancellationToken ct = default)
    {
        var payload = new
        {
            id = value.Id, classId = value.ClassId, state = value.Status == "Active" ? "ACTIVE" : "INACTIVE",
            cardTitle = new { defaultValue = new { language = "es", value = value.DisplayName } },
            header = new { defaultValue = new { language = "es", value = $"{value.Balance:N2} {value.Currency}" } },
            subheader = new { defaultValue = new { language = "es", value = value.RecipientName } },
            barcode = new { type = "QR_CODE", value = value.Code, alternateText = value.Code },
            hexBackgroundColor = value.HexBackgroundColor,
            logo = string.IsNullOrWhiteSpace(value.LogoUri) ? null : new { sourceUri = new { uri = value.LogoUri }, contentDescription = new { defaultValue = new { language = "es", value = value.DisplayName } } },
            heroImage = string.IsNullOrWhiteSpace(value.HeroImageUri) ? null : new { sourceUri = new { uri = value.HeroImageUri }, contentDescription = new { defaultValue = new { language = "es", value = value.DisplayName } } },
            textModulesData = new[] { new { id = "balance", header = "Saldo disponible", body = $"{value.Balance:N2} {value.Currency}" }, new { id = "status", header = "Estado", body = value.Status }, new { id = "expiry", header = "Vigencia", body = value.ExpiresAtUtc?.ToString("yyyy-MM-dd") ?? "Sin expiración" } }
        };
        var existing = await SendAsync(HttpMethod.Get, $"genericObject/{Uri.EscapeDataString(value.Id)}", null, ct);
        if (existing.StatusCode == HttpStatusCode.NotFound)
        {
            var created = await SendAsync(HttpMethod.Post, "genericObject", payload, ct);
            if (created.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created) return;
            if (created.StatusCode != HttpStatusCode.Conflict) throw await CreateExceptionAsync("crear GenericObject Gift Card", created, ct);
        }
        else if (existing.StatusCode != HttpStatusCode.OK) throw await CreateExceptionAsync("consultar GenericObject Gift Card", existing, ct);
        var updated = await SendAsync(new HttpMethod("PATCH"), $"genericObject/{Uri.EscapeDataString(value.Id)}", payload, ct);
        if (updated.StatusCode != HttpStatusCode.OK) throw await CreateExceptionAsync("actualizar GenericObject Gift Card", updated, ct);
    }

    public async Task AddMessageAsync(
        string objectId,
        string header,
        string body,
        string messageId,
        CancellationToken ct = default)
    {
        var payload = new
        {
            message = new
            {
                header,
                body,
                id = messageId,
                messageType = "TEXT_AND_NOTIFY"
            }
        };

        var response = await SendAsync(
            HttpMethod.Post,
            $"loyaltyObject/{Uri.EscapeDataString(objectId)}/addMessage",
            payload,
            ct);
        if (response.StatusCode is HttpStatusCode.OK)
            return;

        throw await CreateExceptionAsync("agregar mensaje a LoyaltyObject", response, ct);
    }
    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativeUrl,
        object? payload,
        CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        using var request = new HttpRequestMessage(method, $"{_options.ApiBaseUrl.TrimEnd('/')}/{relativeUrl}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (payload is not null)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return await _http.SendAsync(request, ct);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var now = _dt.UtcNow;
        if (!string.IsNullOrWhiteSpace(_accessToken) && _accessTokenExpiresAtUtc > now.AddMinutes(5))
            return _accessToken;

        var credentials = await _credentialsProvider.GetAsync(ct);
        var assertion = _jwtFactory.CreateOAuthAssertion(credentials, now);

        using var request = new HttpRequestMessage(HttpMethod.Post, credentials.TokenUri)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion
            })
        };

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw await CreateExceptionAsync("obtener access token Google Wallet", response, ct);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        _accessToken = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("La respuesta OAuth de Google no incluyo access_token.");

        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expiresInElement)
            ? expiresInElement.GetInt32()
            : 3600;
        _accessTokenExpiresAtUtc = now.AddSeconds(expiresIn);

        return _accessToken;
    }

    private static async Task<InvalidOperationException> CreateExceptionAsync(
        string operation,
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        return new InvalidOperationException(
            $"Error al {operation}. Status={(int)response.StatusCode} {response.ReasonPhrase}. Body={body}");
    }
}
