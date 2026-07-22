namespace LoyaltyCloud.Infrastructure.Configuration;

public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    public int GracePeriodDays { get; init; } = 7;

    public int ValidatedGracePeriodDays => Math.Max(0, GracePeriodDays);
}
