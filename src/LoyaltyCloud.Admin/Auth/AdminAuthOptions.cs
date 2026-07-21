namespace LoyaltyCloud.Admin.Auth;

/// <summary>
/// Credenciales del único operador del MVP, bindeadas desde la sección
/// <c>Admin:Auth</c> de configuration. En producción el password debe vivir
/// en Key Vault (override por env var <c>Admin__Auth__Password</c>).
/// </summary>
public sealed class AdminAuthOptions
{
    public const string SectionName = "Admin:Auth";

    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    /// <summary>Duración de la cookie de sesión (horas).</summary>
    public int SessionHours { get; init; } = 8;
}
