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
    private const string NotifyOnUpdate = "notifyOnUpdate";
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

    public async Task CreateOrUpdateObjectAsync(GoogleWalletObjectData walletObject, bool notifyOnUpdate = false, CancellationToken ct = default)
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

        var payload = _mapper.ToObjectPayload(walletObject);
        if (notifyOnUpdate)
            payload["notifyPreference"] = NotifyOnUpdate;

        _logger.LogInformation(
            "Google Wallet LoyaltyObject PATCH prepared. ObjectId={ObjectId}, ClassId={ClassId}, PointsBalance={PointsBalance}, NotifyPreference={NotifyPreference}.",
            walletObject.Id,
            walletObject.ClassId,
            walletObject.PointsBalance,
            notifyOnUpdate ? NotifyOnUpdate : "<omitted>");

        var updated = await SendAsync(
            new HttpMethod("PATCH"),
            $"loyaltyObject/{Uri.EscapeDataString(walletObject.Id)}",
            payload,
            ct);
        if (updated.StatusCode is HttpStatusCode.OK)
        {
            _logger.LogInformation(
                "Google Wallet LoyaltyObject PATCH accepted. ObjectId={ObjectId}, StatusCode={StatusCode}.",
                walletObject.Id,
                (int)updated.StatusCode);
            return;
        }

        throw await CreateExceptionAsync("actualizar LoyaltyObject", updated, ct);
    }

    public async Task EnsureGiftCardClassAsync(GoogleGiftCardClassData walletClass, CancellationToken ct = default)
    {
        var existing = await SendAsync(HttpMethod.Get, $"giftCardClass/{Uri.EscapeDataString(walletClass.Id)}", null, ct);
        if (existing.StatusCode == HttpStatusCode.OK)
            return;
        if (existing.StatusCode != HttpStatusCode.NotFound)
            throw await CreateGiftCardExceptionAsync("consultar", "GiftCardClass", walletClass.Id, null, existing, ct);
        var payload = new { id = walletClass.Id, issuerName = walletClass.IssuerName, reviewStatus = "UNDER_REVIEW" };
        var created = await SendAsync(HttpMethod.Post, "giftCardClass", payload, ct);
        if (created.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Created or HttpStatusCode.Conflict))
            throw await CreateGiftCardExceptionAsync("crear", "GiftCardClass", walletClass.Id, null, created, ct);
    }

    public async Task CreateOrUpdateGiftCardObjectAsync(GoogleGiftCardObjectData value, CancellationToken ct = default)
    {
        var updatedAt = DateTime.SpecifyKind(value.BalanceUpdatedAtUtc, DateTimeKind.Utc);
        var micros = checked((long)decimal.Round(value.Balance * 1_000_000m, 0, MidpointRounding.AwayFromZero));
        var payload = new
        {
            id = value.Id, classId = value.ClassId, state = value.Status == "Active" ? "ACTIVE" : "INACTIVE",
            cardNumber = value.Code,
            balance = new { micros = micros.ToString(System.Globalization.CultureInfo.InvariantCulture), currencyCode = value.Currency.Trim().ToUpperInvariant() },
            balanceUpdateTime = new { year = updatedAt.Year, month = updatedAt.Month, day = updatedAt.Day, hours = updatedAt.Hour, minutes = updatedAt.Minute, seconds = updatedAt.Second, utcOffset = "0s" },
            barcode = new { type = "QR_CODE", value = value.Code, alternateText = value.Code }
        };
        var existing = await SendAsync(HttpMethod.Get, $"giftCardObject/{Uri.EscapeDataString(value.Id)}", null, ct);
        if (existing.StatusCode == HttpStatusCode.NotFound)
        {
            var created = await SendAsync(HttpMethod.Post, "giftCardObject", payload, ct);
            if (created.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created) return;
            if (created.StatusCode != HttpStatusCode.Conflict)
                throw await CreateGiftCardExceptionAsync("crear", "GiftCardObject", value.ClassId, value.Id, created, ct);
        }
        else if (existing.StatusCode != HttpStatusCode.OK)
            throw await CreateGiftCardExceptionAsync("consultar", "GiftCardObject", value.ClassId, value.Id, existing, ct);
        var updated = await SendAsync(new HttpMethod("PATCH"), $"giftCardObject/{Uri.EscapeDataString(value.Id)}", payload, ct);
        if (updated.StatusCode != HttpStatusCode.OK)
            throw await CreateGiftCardExceptionAsync("actualizar", "GiftCardObject", value.ClassId, value.Id, updated, ct);
    }

    private static object[] BuildGiftCardTextModules(GoogleGiftCardObjectData value)
    {
        var modules = new List<object>
        {
            new { id = "balance", header = "Saldo disponible", body = $"{value.Balance:N2} {value.Currency}" },
            new { id = "status", header = "Estado", body = value.Status },
            new { id = "expiry", header = "Vigencia", body = value.ExpiresAtUtc?.ToString("yyyy-MM-dd") ?? "Sin expiración" }
        };

        if (!string.IsNullOrWhiteSpace(value.RecipientName))
            modules.Add(new { id = "recipient", header = "Para", body = value.RecipientName.Trim() });

        if (!string.IsNullOrWhiteSpace(value.SenderName))
            modules.Add(new { id = "sender", header = "De", body = value.SenderName.Trim() });

        if (!string.IsNullOrWhiteSpace(value.PersonalMessage))
            modules.Add(new { id = "message", header = "Mensaje", body = value.PersonalMessage.Trim() });

        return modules.ToArray();
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

    private async Task<InvalidOperationException> CreateGiftCardExceptionAsync(
        string operation, string resourceType, string classId, string? objectId,
        HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogWarning(
            "Google Wallet Gift Card request rejected. Operation={Operation}, ResourceType={ResourceType}, StatusCode={StatusCode}, ClassId={ClassId}, ObjectId={ObjectId}, GoogleErrorBody={GoogleErrorBody}.",
            operation, resourceType, (int)response.StatusCode, classId, objectId ?? "<none>", body);
        return new InvalidOperationException(
            $"Error al {operation} {resourceType}. Status={(int)response.StatusCode} {response.ReasonPhrase}. Body={body}");
    }
}
