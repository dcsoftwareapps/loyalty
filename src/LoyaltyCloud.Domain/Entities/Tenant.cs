using System.Text.RegularExpressions;
using LoyaltyCloud.Domain.Common;

namespace LoyaltyCloud.Domain.Entities;

public sealed partial class Tenant : Entity
{
    public string Slug { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public TenantBranding? Branding { get; private set; }
    public TenantSubscription? Subscription { get; private set; }
    public IReadOnlyCollection<TenantAdminUser> AdminUsers => _adminUsers.AsReadOnly();

    private readonly List<TenantAdminUser> _adminUsers = new();

    private Tenant() { }

    public Tenant(
        Guid id,
        string slug,
        string displayName,
        string timeZoneId,
        DateTime createdAtUtc,
        bool isActive = true) : base(id)
    {
        Slug = NormalizeSlug(slug);
        DisplayName = Require(displayName, nameof(displayName), 200);
        TimeZoneId = Require(timeZoneId, nameof(timeZoneId), 100);
        CreatedAt = createdAtUtc;
        IsActive = isActive;
    }

    public void Rename(string displayName, string timeZoneId, DateTime updatedAtUtc)
    {
        DisplayName = Require(displayName, nameof(displayName), 200);
        TimeZoneId = Require(timeZoneId, nameof(timeZoneId), 100);
        UpdatedAt = updatedAtUtc;
    }

    public void Activate(DateTime updatedAtUtc)
    {
        IsActive = true;
        UpdatedAt = updatedAtUtc;
    }

    public void Deactivate(DateTime updatedAtUtc)
    {
        IsActive = false;
        UpdatedAt = updatedAtUtc;
    }

    public static string NormalizeSlug(string slug)
    {
        var normalized = Require(slug, nameof(slug), 100).ToLowerInvariant();
        if (!SlugRegex().IsMatch(normalized))
            throw new ArgumentException("Slug debe ser URL-safe en minusculas.", nameof(slug));

        return normalized;
    }

    internal static string Require(string value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} requerido.", paramName);

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ArgumentException($"{paramName} no puede exceder {maxLength} caracteres.", paramName);

        return trimmed;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();
}
