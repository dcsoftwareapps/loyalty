namespace KBeauty.Loyalty.Infrastructure.Configuration;

/// <summary>Feature flags de integracion Apple Wallet.</summary>
public sealed class WalletOptions
{
    public const string SectionName = "Wallet";

    /// <summary>
    /// En Development fuerza el generador real firmado y secretos locales,
    /// sin depender de Azure Key Vault.
    /// </summary>
    public bool UseRealPassSigning { get; init; }
}
