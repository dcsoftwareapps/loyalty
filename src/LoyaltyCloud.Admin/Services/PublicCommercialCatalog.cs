namespace LoyaltyCloud.Admin.Services;

/// <summary>
/// Public commercial offer, independent of tenant billing configuration.
/// Phase 8 must explicitly map and reconcile this offer with Billing/Stripe.
/// No billing IDs, database access or checkout behavior belong here.
/// </summary>
public static class PublicCommercialCatalog
{
    public const string PlanName = "Fundador";
    public const string Currency = "MXN";
    public const decimal MonthlyPrice = 249m;
    public static IReadOnlyList<PublicPeriod> Periods { get; } = Array.AsReadOnly(new[] {
        new PublicPeriod(1, 249m),
        new PublicPeriod(3, 699m),
        new PublicPeriod(6, 1299m),
        new PublicPeriod(12, 2490m)
    });
}
public sealed record PublicPeriod(int Months, decimal Price)
{
    public decimal Savings => PublicCommercialCatalog.MonthlyPrice * Months - Price;
    public bool TwoMonthsFree => Months == 12 && Savings == PublicCommercialCatalog.MonthlyPrice * 2;
}
