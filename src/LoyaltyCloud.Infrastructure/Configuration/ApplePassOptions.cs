namespace LoyaltyCloud.Infrastructure.Configuration;

/// <summary>
/// Configuración bindeable desde la sección <c>"Apple"</c> de appsettings/Key Vault.
/// Las claves sensibles (cert, p8, kid, team) viven en Key Vault — aquí solo
/// están los valores no-secretos.
/// </summary>
public sealed class ApplePassOptions
{
    /// <summary>Nombre de la sección en configuration.</summary>
    public const string SectionName = "Apple";

    /// <summary>Pass Type Identifier registrado en Apple Developer.</summary>
    public string PassTypeIdentifier { get; init; } = "pass.com.kbeautymx.loyalty";

    /// <summary>Team Identifier (10 caracteres) de la cuenta Apple Developer.</summary>
    public string TeamIdentifier { get; init; } = string.Empty;

    /// <summary>URL pública del backend — Apple llama aquí para refrescar pases.</summary>
    public string WebServiceURL { get; init; } = string.Empty;

    /// <summary>Nombre de la organización mostrada en Wallet.</summary>
    public string OrganizationName { get; init; } = "KBeauty MX";

    /// <summary>Endpoint productivo de Apple Push Notifications para passes.</summary>
    public string ApnHost { get; init; } = "https://api.push.apple.com";
}
