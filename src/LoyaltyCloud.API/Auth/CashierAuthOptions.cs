namespace LoyaltyCloud.API.Auth;

public sealed class CashierAuthOptions
{
    public const string SectionName = "CashierAuth";

    public string Issuer { get; set; } = "LoyaltyCloud";
    public string Audience { get; set; } = "LoyaltyCloud.Cashier";
    public int AccessTokenMinutes { get; set; } = 60;
    public string? SigningKey { get; set; }
}
