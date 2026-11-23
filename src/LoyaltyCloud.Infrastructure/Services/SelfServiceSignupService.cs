using System.Data;
using System.Security.Cryptography;
using System.Text;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Provisioning;
using LoyaltyCloud.Application.SelfServiceSignup;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class SelfServiceSignupService(
    AppDbContext db,
    ITenantProvisioningService provisioning,
    IPasswordHashingService passwords,
    IDateTimeProvider clock) : ISelfServiceSignupService
{
    private const string DefaultTimeZoneId = "America/Tijuana";
    private const string IdempotencyConflictError =
        "SignupAttemptId ya fue utilizado con una solicitud diferente.";
    private const string IncompleteAttemptError =
        "El intento de signup no pudo resolverse. Intenta nuevamente.";

    public async Task<Result<SelfServiceSignupResult>> SignupAsync(
        SelfServiceSignupCommand command,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(command);
        var fingerprint = CreateFingerprint(normalized);
        var strategy = db.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

                var existing = await db.SelfServiceSignupAttempts
                    .SingleOrDefaultAsync(
                        attempt => attempt.AttemptId == normalized.SignupAttemptId,
                        cancellationToken);

                if (existing is not null)
                {
                    var replay = Replay(existing, fingerprint, normalized.Password);
                    if (replay.IsSuccess)
                        await transaction.CommitAsync(cancellationToken);
                    else
                        await transaction.RollbackAsync(cancellationToken);

                    return replay;
                }

                var attempt = new SelfServiceSignupAttempt(
                    Guid.NewGuid(),
                    normalized.SignupAttemptId,
                    fingerprint,
                    passwords.HashPassword(normalized.Password),
                    clock.UtcNow);
                db.SelfServiceSignupAttempts.Add(attempt);
                await db.SaveChangesAsync(cancellationToken);

                var provisioned = await provisioning.ProvisionAsync(
                    new ProvisionTenantRequest(
                        Slug: normalized.Slug,
                        DisplayName: normalized.BusinessDisplayName,
                        TimeZoneId: normalized.TimeZoneId,
                        AdminUsername: $"{normalized.Slug}-owner",
                        AdminPassword: normalized.Password,
                        PrimaryColor: null,
                        SecondaryColor: null,
                        SupportPhone: null,
                        WhatsAppUrl: null,
                        InstagramUrl: null,
                        TermsUrl: null,
                        AdminEmail: normalized.Email,
                        TrialPolicy: ProvisioningTrialPolicy.OneCalendarMonth),
                    cancellationToken);

                if (provisioned.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    db.ChangeTracker.Clear();
                    return Result.Fail<SelfServiceSignupResult>(provisioned.Error);
                }

                var result = new SelfServiceSignupResult(
                    provisioned.Value.TenantId,
                    provisioned.Value.TenantSlug,
                    provisioned.Value.AdminUserId,
                    provisioned.Value.TrialEndsAtUtc);

                attempt.Complete(
                    result.TenantId,
                    result.TenantSlug,
                    result.AdminUserId,
                    result.TrialEndsAtUtc,
                    clock.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Ok(result);
            });
        }
        catch (DbUpdateException exception) when (IsUniqueAttemptViolation(exception))
        {
            db.ChangeTracker.Clear();
            var existing = await db.SelfServiceSignupAttempts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    attempt => attempt.AttemptId == normalized.SignupAttemptId,
                    cancellationToken);

            return existing is null
                ? Result.Fail<SelfServiceSignupResult>(IncompleteAttemptError)
                : Replay(existing, fingerprint, normalized.Password);
        }
    }

    private Result<SelfServiceSignupResult> Replay(
        SelfServiceSignupAttempt attempt,
        string fingerprint,
        string password)
    {
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(attempt.RequestFingerprint),
                Encoding.ASCII.GetBytes(fingerprint)))
        {
            return Result.Fail<SelfServiceSignupResult>(IdempotencyConflictError);
        }

        if (!attempt.IsCompleted
            || !attempt.TenantId.HasValue
            || !attempt.AdminUserId.HasValue
            || !attempt.TrialEndsAtUtc.HasValue
            || string.IsNullOrWhiteSpace(attempt.TenantSlug))
        {
            return Result.Fail<SelfServiceSignupResult>(IncompleteAttemptError);
        }

        if (!attempt.IsCompleted
            && (string.IsNullOrWhiteSpace(attempt.PasswordVerificationHash)
                || !passwords.VerifyPassword(attempt.PasswordVerificationHash, password)))
        {
            return Result.Fail<SelfServiceSignupResult>(IdempotencyConflictError);
        }

        return Result.Ok(new SelfServiceSignupResult(
            attempt.TenantId.Value,
            attempt.TenantSlug,
            attempt.AdminUserId.Value,
            attempt.TrialEndsAtUtc.Value));
    }

    private static NormalizedSignup Normalize(SelfServiceSignupCommand command) =>
        new(
            Email: command.Email.Trim(),
            Password: command.Password,
            BusinessDisplayName: command.BusinessDisplayName.Trim(),
            Slug: Tenant.NormalizeSlug(command.Slug),
            TimeZoneId: string.IsNullOrWhiteSpace(command.TimeZoneId)
                ? DefaultTimeZoneId
                : command.TimeZoneId.Trim(),
            SignupAttemptId: command.SignupAttemptId.Trim());

    private static string CreateFingerprint(NormalizedSignup request)
    {
        var canonical = string.Join(
            "\n",
            TenantAdminUser.NormalizeEmail(request.Email),
            request.BusinessDisplayName,
            request.Slug,
            request.TimeZoneId);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static bool IsUniqueAttemptViolation(DbUpdateException exception)
    {
        var message = exception.ToString();
        return message.Contains("IX_SelfServiceSignupAttempts_AttemptId", StringComparison.OrdinalIgnoreCase)
            || (message.Contains("SelfServiceSignupAttempts", StringComparison.OrdinalIgnoreCase)
                && (message.Contains("2601", StringComparison.Ordinal)
                    || message.Contains("2627", StringComparison.Ordinal)));
    }

    private sealed record NormalizedSignup(
        string Email,
        string Password,
        string BusinessDisplayName,
        string Slug,
        string TimeZoneId,
        string SignupAttemptId);
}
