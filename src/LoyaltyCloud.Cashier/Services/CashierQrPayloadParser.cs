namespace LoyaltyCloud.Cashier.Services;

public static class CashierQrPayloadParser
{
    public static string? ExtractCustomerSerial(string? payload)
    {
        var value = payload?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
