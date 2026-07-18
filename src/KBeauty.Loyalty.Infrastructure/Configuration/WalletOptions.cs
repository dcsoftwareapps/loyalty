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

    /// <summary>
    /// En Development fuerza APNs real con credenciales locales.
    /// Si es false, se usa NoOpApnService y no se envian pushes reales.
    /// </summary>
    public bool UseRealApns { get; init; }
}
