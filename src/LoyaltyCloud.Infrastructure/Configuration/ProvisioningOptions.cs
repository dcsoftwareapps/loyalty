namespace LoyaltyCloud.Infrastructure.Configuration;

public sealed class ProvisioningOptions
{
    public const string SectionName = "Provisioning";

    public int TrialDays { get; init; } = 14;
}
