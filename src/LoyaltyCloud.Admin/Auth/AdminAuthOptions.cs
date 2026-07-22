namespace LoyaltyCloud.Admin.Auth;

/// <summary>
/// Opciones de cookie del Admin. Las credenciales se validan contra
/// TenantAdminUser; Admin:Auth:Username y Admin:Auth:Password quedaron fuera
/// del flujo activo.
/// </summary>
public sealed class AdminAuthOptions
{
    public const string SectionName = "Admin:Auth";

    /// <summary>Duración de la cookie de sesión, en horas.</summary>
    public int SessionHours { get; init; } = 8;
}
