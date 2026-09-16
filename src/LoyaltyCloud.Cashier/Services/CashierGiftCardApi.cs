using System.Net.Http.Json;
using System.Text.Json;

namespace LoyaltyCloud.Cashier.Services;

public sealed class CashierGiftCardApi(AuthenticatedCashierApiClient api) : ICashierGiftCardApi
{
    public async Task<GiftCardResult<GiftCardIssueOptions>> GetIssueOptionsAsync()
    {
        try
        {
            using var response = await api.GetAsync("api/giftcards/issue/options");
            return await ReadAsync<GiftCardIssueOptions>(response);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or NotSupportedException)
        {
            return Unknown<GiftCardIssueOptions>();
        }
    }

    public Task<GiftCardResult<GiftCardIssueReceipt>> IssueAsync(GiftCardIssueRequest request) =>
        SendAsync<GiftCardIssueReceipt, GiftCardIssueRequest>("api/giftcards/issue", request);

    public Task<GiftCardResult<CashierGiftCard>> LookupAsync(GiftCardLookup lookup) =>
        SendAsync<CashierGiftCard, GiftCardLookup>("api/giftcards/lookup", lookup);

    public Task<GiftCardResult<GiftCardReceipt>> RedeemAsync(string code, GiftCardRedemption request) =>
        SendAsync<GiftCardReceipt, GiftCardRedemption>($"api/giftcards/{Uri.EscapeDataString(code)}/redeem", request);

    private async Task<GiftCardResult<T>> SendAsync<T, TRequest>(string path, TRequest request)
    {
        try
        {
            using var response = await api.PostAsJsonAsync(path, request);
            if (response.IsSuccessStatusCode)
                return await ReadAsync<T>(response);
            return await ReadAsync<T>(response);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or NotSupportedException)
        {
            return Unknown<T>();
        }
    }

    private static async Task<GiftCardResult<T>> ReadAsync<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<T>();
            return body is null ? Unknown<T>() : new(body);
        }
        if ((int)response.StatusCode == 401)
            return GiftCardResult<T>.Fail("Unauthorized", "Tu sesión expiró. Inicia sesión con la misma cuenta para continuar.");
        if ((int)response.StatusCode >= 500) return Unknown<T>();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var code = document.RootElement.TryGetProperty("code", out var value) ? value.GetString() : null;
        var status = (int)response.StatusCode;
        return (status, code) switch
        {
            (400, "InvalidInput") => GiftCardResult<T>.Fail("InvalidInput", "Revisa los datos ingresados (máximo dos decimales).", true),
            (400, null) => GiftCardResult<T>.Fail("InvalidInput", "Revisa los datos ingresados.", true),
            (404, "NotFound") => GiftCardResult<T>.Fail("NotFound", "No encontramos esa tarjeta de regalo.", true),
            (403, "Unavailable") => GiftCardResult<T>.Fail("Unavailable", "El módulo de tarjetas de regalo no está disponible para este negocio."),
            (422, "Inactive") => GiftCardResult<T>.Fail("Inactive", "Esta tarjeta de regalo no está activa.", true),
            (422, "Expired") => GiftCardResult<T>.Fail("Expired", "Esta tarjeta de regalo expiró.", true),
            (422, "InsufficientBalance") => GiftCardResult<T>.Fail("InsufficientBalance", "El saldo disponible no alcanza para ese monto.", true),
            (422, "PartialRedemptionNotAllowed") => GiftCardResult<T>.Fail("PartialRedemptionNotAllowed", "Este negocio solo permite canjear el saldo completo.", true),
            (409, "ConcurrencyConflict") => GiftCardResult<T>.Fail("ConcurrencyConflict", "El saldo cambió durante el canje. Consulta la tarjeta nuevamente.", true),
            (409, "IdempotencyConflict") => GiftCardResult<T>.Fail("IdempotencyConflict", "Esta operación requiere revisión: la clave corresponde a otra operación. Contacta al administrador."),
            _ => Unknown<T>()
        };
    }

    private static GiftCardResult<T> Unknown<T>() => GiftCardResult<T>.Fail("Uncertain",
        "No pudimos confirmar la respuesta. Revisa tu conexión e intenta de nuevo.");
}
