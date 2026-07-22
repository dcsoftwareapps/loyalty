using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class SuperAdminTenantManagementService : ISuperAdminTenantManagementService
{
    private static readonly TimeSpan MaximumTrialExtension = TimeSpan.FromDays(730);
    private static readonly TimeSpan MaximumGraceExtension = TimeSpan.FromDays(365);

    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<SuperAdminTenantManagementService> _logger;

    public SuperAdminTenantManagementService(
        AppDbContext db,
        IDateTimeProvider clock,
        ILogger<SuperAdminTenantManagementService> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result> SuspendAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await LoadTenantAsync(tenantId, cancellationToken);
        if (tenant is null) return Result.Fail("Tenant no encontrado.");
        if (tenant.Subscription is null) return Result.Fail("El tenant no tiene suscripcion configurada.");
        if (tenant.Subscription.Status == TenantSubscriptionStatus.Cancelled)
            return Result.Fail("No se puede suspender un tenant cancelado.");

        tenant.Subscription.Suspend();
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Tenant suspended. TenantId={TenantId}, TenantSlug={TenantSlug}", tenant.Id, tenant.Slug);
        return Result.Ok();
    }

    public async Task<Result> ReactivateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await LoadTenantAsync(tenantId, cancellationToken);
        if (tenant is null) return Result.Fail("Tenant no encontrado.");
        if (tenant.Subscription is null) return Result.Fail("El tenant no tiene suscripcion configurada.");
        if (tenant.Subscription.Status == TenantSubscriptionStatus.Cancelled)
            return Result.Fail("No se puede reactivar un tenant cancelado.");

        tenant.Subscription.Reactivate();
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Tenant reactivated. TenantId={TenantId}, TenantSlug={TenantSlug}", tenant.Id, tenant.Slug);
        return Result.Ok();
    }

    public async Task<Result> CancelAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await LoadTenantAsync(tenantId, cancellationToken);
        if (tenant is null) return Result.Fail("Tenant no encontrado.");
        if (tenant.Subscription is null) return Result.Fail("El tenant no tiene suscripcion configurada.");

        tenant.Subscription.Cancel();
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Tenant cancelled. TenantId={TenantId}, TenantSlug={TenantSlug}", tenant.Id, tenant.Slug);
        return Result.Ok();
    }

    public async Task<Result> ExtendTrialAsync(
        Guid tenantId,
        DateTime newTrialEndUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedDate = NormalizeUtc(newTrialEndUtc);
        var now = _clock.UtcNow;
        if (normalizedDate <= now)
            return Result.Fail("La nueva fecha de trial debe ser futura.");
        if (normalizedDate > now.Add(MaximumTrialExtension))
            return Result.Fail("La nueva fecha de trial excede el limite permitido.");

        var tenant = await LoadTenantAsync(tenantId, cancellationToken);
        if (tenant is null) return Result.Fail("Tenant no encontrado.");
        if (tenant.Subscription is null) return Result.Fail("El tenant no tiene suscripcion configurada.");

        try
        {
            tenant.Subscription.ExtendTrial(normalizedDate);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result.Fail(ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Tenant trial extended. TenantId={TenantId}, TenantSlug={TenantSlug}, NewTrialEnd={NewTrialEnd}",
            tenant.Id,
            tenant.Slug,
            normalizedDate);
        return Result.Ok();
    }

    public async Task<Result> UpdateGracePeriodAsync(
        Guid tenantId,
        DateTime? newGracePeriodEndUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedDate = newGracePeriodEndUtc.HasValue ? NormalizeUtc(newGracePeriodEndUtc.Value) : (DateTime?)null;
        var now = _clock.UtcNow;
        if (normalizedDate.HasValue && normalizedDate.Value <= now)
            return Result.Fail("La fecha de gracia debe ser futura.");
        if (normalizedDate.HasValue && normalizedDate.Value > now.Add(MaximumGraceExtension))
            return Result.Fail("La fecha de gracia excede el limite permitido.");

        var tenant = await LoadTenantAsync(tenantId, cancellationToken);
        if (tenant is null) return Result.Fail("Tenant no encontrado.");
        if (tenant.Subscription is null) return Result.Fail("El tenant no tiene suscripcion configurada.");
        if (tenant.Subscription.Status != TenantSubscriptionStatus.PastDue)
            return Result.Fail("El periodo de gracia solo aplica a tenants PastDue.");

        try
        {
            tenant.Subscription.ChangeGracePeriod(normalizedDate);
        }
        catch (ArgumentException ex)
        {
            return Result.Fail(ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Tenant grace period changed. TenantId={TenantId}, TenantSlug={TenantSlug}, NewGraceEnd={NewGraceEnd}",
            tenant.Id,
            tenant.Slug,
            normalizedDate);
        return Result.Ok();
    }

    private async Task<Tenant?> LoadTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await _db.Tenants
            .Include(t => t.Subscription)
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime()
        };
}
