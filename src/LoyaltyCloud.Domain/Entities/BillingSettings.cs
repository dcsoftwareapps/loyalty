using LoyaltyCloud.Domain.Common;

namespace LoyaltyCloud.Domain.Entities;

public sealed class BillingSettings : Entity
{
    public const string SingletonCode = "default";
    public string Code { get; private set; } = SingletonCode;
    public string Currency { get; private set; } = "MXN";
    public decimal TaxRate { get; private set; } = 16m;
    public bool PricesIncludeTax { get; private set; }
    public int GracePeriodDays { get; private set; } = 7;
    public bool CardPaymentsEnabled { get; private set; }
    public bool BankTransferEnabled { get; private set; }
    public bool RequireTransferReceipt { get; private set; }
    public bool AutomaticRenewalEnabled { get; private set; }
    public bool CfdiEnabled { get; private set; }
    public string? BankName { get; private set; }
    public string? BeneficiaryName { get; private set; }
    public string? Clabe { get; private set; }
    public string? BankTransferInstructions { get; private set; }
    public string? SupportEmail { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private BillingSettings() { }
    public BillingSettings(Guid id, DateTime nowUtc) : base(id) => UpdatedAt = nowUtc;

    public void Update(string currency, decimal taxRate, bool pricesIncludeTax, int gracePeriodDays,
        bool cardPaymentsEnabled, bool bankTransferEnabled, bool requireTransferReceipt,
        string? bankName, string? beneficiaryName, string? clabe, string? instructions,
        string? supportEmail, DateTime nowUtc)
    {
        currency = currency.Trim().ToUpperInvariant();
        if (currency.Length != 3) throw new ArgumentException("Currency debe ser ISO de tres letras.");
        if (taxRate is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(taxRate));
        if (gracePeriodDays is < 0 or > 90) throw new ArgumentOutOfRangeException(nameof(gracePeriodDays));
        Currency = currency; TaxRate = taxRate; PricesIncludeTax = pricesIncludeTax;
        GracePeriodDays = gracePeriodDays; CardPaymentsEnabled = cardPaymentsEnabled;
        BankTransferEnabled = bankTransferEnabled; RequireTransferReceipt = requireTransferReceipt;
        BankName = Clean(bankName, 150); BeneficiaryName = Clean(beneficiaryName, 200);
        Clabe = Clean(clabe, 18); BankTransferInstructions = Clean(instructions, 2000);
        SupportEmail = Clean(supportEmail, 320); UpdatedAt = nowUtc;
    }
    private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
