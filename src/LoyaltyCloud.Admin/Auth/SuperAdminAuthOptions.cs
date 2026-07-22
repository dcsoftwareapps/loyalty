namespace LoyaltyCloud.Admin.Auth;

public sealed class SuperAdminAuthOptions
{
    public const string SectionName = "SuperAdmin";

    public string? Username { get; set; }
    public string? PasswordHash { get; set; }
    public int SessionHours { get; set; } = 8;
}
