using LoyaltyCloud.Domain.Common;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Domain.Entities;

public sealed class TenantAdminUser : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string NormalizedUsername { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public TenantUserRole Role { get; private set; } = TenantUserRole.Admin;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    public Tenant? Tenant { get; private set; }

    private TenantAdminUser() { }

    public TenantAdminUser(
        Guid id,
        Guid tenantId,
        string username,
        string passwordHash,
        DateTime createdAtUtc,
        bool isActive = true,
        TenantUserRole role = TenantUserRole.Admin) : base(id)
    {
        TenantId = tenantId == Guid.Empty
            ? throw new ArgumentException("TenantId requerido.", nameof(tenantId))
            : tenantId;
        Username = Tenant.Require(username, nameof(username), 150);
        NormalizedUsername = NormalizeUsername(username);
        PasswordHash = Tenant.Require(passwordHash, nameof(passwordHash), 1000);
        Role = role;
        CreatedAt = createdAtUtc;
        IsActive = isActive;
    }

    public void ChangeRole(TenantUserRole role)
    {
        Role = role;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void RecordLogin(DateTime loggedInAtUtc)
    {
        LastLoginAt = loggedInAtUtc;
    }

    public void ChangePasswordHash(string passwordHash)
    {
        PasswordHash = Tenant.Require(passwordHash, nameof(passwordHash), 1000);
    }

    public static string NormalizeUsername(string username) =>
        Tenant.Require(username, nameof(username), 150).ToUpperInvariant();
}
