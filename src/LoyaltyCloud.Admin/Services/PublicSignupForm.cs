namespace LoyaltyCloud.Admin.Services;

public sealed class PublicSignupForm
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string BusinessDisplayName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "America/Tijuana";
    public string SignupAttemptId { get; set; } = string.Empty;
}
