using LoyaltyCloud.Domain.Common;

namespace LoyaltyCloud.Domain.Entities;

public sealed class SelfServiceSignupAttempt : Entity
{
    public string AttemptId { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
    public string? PasswordVerificationHash { get; private set; }
    public Guid? TenantId { get; private set; }
    public string? TenantSlug { get; private set; }
    public Guid? AdminUserId { get; private set; }
    public DateTime? TrialEndsAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public Tenant? Tenant { get; private set; }

    private SelfServiceSignupAttempt() { }

    public SelfServiceSignupAttempt(
        Guid id,
        string attemptId,
        string requestFingerprint,
        string passwordVerificationHash,
        DateTime createdAtUtc) : base(id)
    {
        AttemptId = Require(attemptId, nameof(attemptId), 100);
        RequestFingerprint = Require(requestFingerprint, nameof(requestFingerprint), 64);
        PasswordVerificationHash = Require(passwordVerificationHash, nameof(passwordVerificationHash), 1000);
        CreatedAtUtc = createdAtUtc;
    }

    public bool IsCompleted => CompletedAtUtc.HasValue;

    public void Complete(
        Guid tenantId,
        string tenantSlug,
        Guid adminUserId,
        DateTime trialEndsAtUtc,
        DateTime completedAtUtc)
    {
        if (IsCompleted)
            throw new InvalidOperationException("El intento de signup ya fue completado.");

        TenantId = tenantId == Guid.Empty ? throw new ArgumentException("TenantId requerido.", nameof(tenantId)) : tenantId;
        TenantSlug = Require(tenantSlug, nameof(tenantSlug), 50);
        AdminUserId = adminUserId == Guid.Empty ? throw new ArgumentException("AdminUserId requerido.", nameof(adminUserId)) : adminUserId;
        TrialEndsAtUtc = trialEndsAtUtc;
        CompletedAtUtc = completedAtUtc;
        PasswordVerificationHash = null;
    }

    private static string Require(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{parameterName} requerido.", parameterName);

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ArgumentException($"{parameterName} no puede exceder {maxLength} caracteres.", parameterName);

        return trimmed;
    }
}
