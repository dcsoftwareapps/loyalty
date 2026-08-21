namespace LoyaltyCloud.Infrastructure.Configuration;
public sealed class StripeOptions
{
    public const string SectionName = "Stripe";
    public bool Enabled { get; init; }
    public string? SecretKey { get; init; }
    public string? PublishableKey { get; init; }
    public string? WebhookSecret { get; init; }
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(WebhookSecret);
}
