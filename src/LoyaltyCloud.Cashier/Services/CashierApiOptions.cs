namespace LoyaltyCloud.Cashier.Services;

public sealed record CashierApiOptions(string EnvironmentName, Uri BaseUri)
{
    public static CashierApiOptions Default =>
#if LOYALTYCLOUD_PROD
        new("Production", new Uri("https://api.loyaltycloud.net"));
#elif LOYALTYCLOUD_STG
        new("Staging", new Uri("https://loyaltycloud-api-stg-01.azurewebsites.net"));
#else
        new("Staging", new Uri("https://loyaltycloud-api-stg-01.azurewebsites.net"));
#endif
}
