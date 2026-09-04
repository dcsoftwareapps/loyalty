namespace LoyaltyCloud.Infrastructure.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public string SmtpHost { get; init; } = "smtp.mx.cloudflare.net";
    public int SmtpPort { get; init; } = 465;
    public string Username { get; init; } = "api_token";
    public string? Password { get; init; }

    // Resolve from transport, not a stale database label. Existing SMTP settings remain valid.
    public string EffectiveProvider => string.Equals(SmtpHost?.Trim(), "smtp.resend.com", StringComparison.OrdinalIgnoreCase)
        ? "Resend"
        : string.Equals(SmtpHost?.Trim(), "smtp.mx.cloudflare.net", StringComparison.OrdinalIgnoreCase)
            ? "Cloudflare (legacy SMTP)" : "SMTP";

    public bool CredentialsConfigured =>
        !string.IsNullOrWhiteSpace(SmtpHost)
        && SmtpPort is > 0 and <= 65535
        && !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrWhiteSpace(Password);
}
