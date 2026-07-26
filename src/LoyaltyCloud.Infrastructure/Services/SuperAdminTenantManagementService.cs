using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.SuperAdmin.Commands.RecordManualSubscriptionPayment;
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
    private readonly IMutableTenantContext _tenantContext;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<SuperAdminTenantManagementService> _logger;

    public SuperAdminTenantManagementService(
        AppDbContext db,
        IMutableTenantContext tenantContext,
        IDateTimeProvider clock,
        ILogger<SuperAdminTenantManagementService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
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

        tenant.Subscription.SuspendAdministratively();
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Tenant suspended. TenantId={TenantId}, TenantSlug={TenantSlug}, SuspensionReason={SuspensionReason}",
            tenant.Id,
            tenant.Slug,
            tenant.Subscription.SuspensionReason);
        return Result.Ok();
    }

    public async Task<Result> ReactivateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await LoadTenantAsync(tenantId, cancellationToken);
        if (tenant is null) return Result.Fail("Tenant no encontrado.");
        if (tenant.Subscription is null) return Result.Fail("El tenant no tiene suscripcion configurada.");
        if (tenant.Subscription.Status == TenantSubscriptionStatus.Cancelled)
            return Result.Fail("No se puede reactivar un tenant cancelado.");
        if (tenant.Subscription.Status != TenantSubscriptionStatus.Suspended
            || tenant.Subscription.SuspensionReason != TenantSuspensionReason.Administrative)
            return Result.Fail("Solo se puede reactivar una suspension administrativa.");
        if (!tenant.Subscription.PaidThroughUtc.HasValue || tenant.Subscription.PaidThroughUtc.Value <= _clock.UtcNow)
            return Result.Fail("No se puede reactivar sin una vigencia pagada vigente. Registra un pago primero.");

        try
        {
            tenant.Subscription.Reactivate(_clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Fail(ex.Message);
        }
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

    public async Task<Result> DeleteAsync(
        Guid tenantId,
        string confirmationSlug,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Result.Fail("TenantId requerido.");
        if (string.IsNullOrWhiteSpace(confirmationSlug))
            return Result.Fail("Escribe el slug del tenant para confirmar.");

        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
            return Result.Fail("Tenant no encontrado.");
        if (!string.Equals(tenant.Slug, confirmationSlug.Trim(), StringComparison.Ordinal))
            return Result.Fail("El slug de confirmacion no coincide.");

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(cancellationToken)
                : null;
            try
            {
                if (_db.Database.IsRelational())
                    await DeleteTenantDataWithBulkSqlAsync(tenantId, cancellationToken);
                else
                    await DeleteTenantDataWithTrackedEntitiesAsync(tenantId, tenant.Slug, cancellationToken);

                if (_db.Database.IsRelational())
                {
                    await _db.Tenants
                        .Where(t => t.Id == tenantId)
                        .ExecuteDeleteAsync(cancellationToken);
                }
                else
                {
                    var trackedTenant = await _db.Tenants
                        .SingleAsync(t => t.Id == tenantId, cancellationToken);
                    _db.Tenants.Remove(trackedTenant);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                if (tx is not null)
                    await tx.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Tenant hard deleted. TenantId={TenantId}, TenantSlug={TenantSlug}",
                    tenant.Id,
                    tenant.Slug);

                return Result.Ok();
            }
            catch (Exception ex)
            {
                if (tx is not null)
                    await tx.RollbackAsync(cancellationToken);

                _logger.LogError(
                    ex,
                    "Tenant hard delete failed. TenantId={TenantId}, TenantSlug={TenantSlug}",
                    tenant.Id,
                    tenant.Slug);
                throw;
            }
        });
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
        if (tenant.Subscription.Status != TenantSubscriptionStatus.Trial)
            return Result.Fail("Solo se puede extender el trial de una suscripcion en Trial.");

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

    public async Task<Result<RecordManualSubscriptionPaymentResult>> RecordPaymentAsync(
        Guid tenantId,
        int months,
        CancellationToken cancellationToken = default)
    {
        var tenant = await LoadTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
            return Result.Fail<RecordManualSubscriptionPaymentResult>("Tenant no encontrado.");
        if (tenant.Subscription is null)
            return Result.Fail<RecordManualSubscriptionPaymentResult>("El tenant no tiene suscripcion configurada.");

        DateTime paidThrough;
        try
        {
            paidThrough = tenant.Subscription.RecordManualPayment(months, _clock.UtcNow);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result.Fail<RecordManualSubscriptionPaymentResult>(ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Manual subscription payment recorded. TenantId={TenantId}, TenantSlug={TenantSlug}, Months={Months}, PaidThroughUtc={PaidThroughUtc}",
            tenant.Id,
            tenant.Slug,
            months,
            paidThrough);

        return Result.Ok(new RecordManualSubscriptionPaymentResult(tenant.Id, tenant.Slug, months, paidThrough));
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

    private async Task DeleteTenantDataWithBulkSqlAsync(Guid tenantId, CancellationToken ct)
    {
        await _db.NotificationDeliveries.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.LoyaltyNotifications.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.PointLotConsumptions.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.PointLots.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.Redemptions.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.PointTransactions.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.DeviceRegistrations.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.LoyaltyCards.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.CustomNotificationCampaigns.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.PointCampaigns.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.RewardCatalogItems.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.Customers.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.ProgramConfigs.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.TenantLoyaltyLevels.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.TenantAdminUsers.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.TenantBrandings.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await _db.TenantSubscriptions.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);
    }

    private async Task DeleteTenantDataWithTrackedEntitiesAsync(Guid tenantId, string tenantSlug, CancellationToken ct)
    {
        if (!_tenantContext.HasTenant)
            _tenantContext.SetTenant(tenantId, tenantSlug);

        _db.NotificationDeliveries.RemoveRange(await _db.NotificationDeliveries.ToListAsync(ct));
        _db.LoyaltyNotifications.RemoveRange(await _db.LoyaltyNotifications.ToListAsync(ct));
        _db.PointLotConsumptions.RemoveRange(await _db.PointLotConsumptions.ToListAsync(ct));
        _db.PointLots.RemoveRange(await _db.PointLots.ToListAsync(ct));
        _db.Redemptions.RemoveRange(await _db.Redemptions.ToListAsync(ct));
        _db.PointTransactions.RemoveRange(await _db.PointTransactions.ToListAsync(ct));
        _db.DeviceRegistrations.RemoveRange(await _db.DeviceRegistrations.ToListAsync(ct));
        _db.LoyaltyCards.RemoveRange(await _db.LoyaltyCards.ToListAsync(ct));
        _db.CustomNotificationCampaigns.RemoveRange(await _db.CustomNotificationCampaigns.ToListAsync(ct));
        _db.PointCampaigns.RemoveRange(await _db.PointCampaigns.ToListAsync(ct));
        _db.RewardCatalogItems.RemoveRange(await _db.RewardCatalogItems.ToListAsync(ct));
        _db.Customers.RemoveRange(await _db.Customers.ToListAsync(ct));
        _db.ProgramConfigs.RemoveRange(await _db.ProgramConfigs.ToListAsync(ct));
        _db.TenantLoyaltyLevels.RemoveRange(await _db.TenantLoyaltyLevels.ToListAsync(ct));
        _db.TenantAdminUsers.RemoveRange(await _db.TenantAdminUsers.ToListAsync(ct));
        _db.TenantBrandings.RemoveRange(await _db.TenantBrandings.Where(x => x.TenantId == tenantId).ToListAsync(ct));
        _db.TenantSubscriptions.RemoveRange(await _db.TenantSubscriptions.Where(x => x.TenantId == tenantId).ToListAsync(ct));
        await _db.SaveChangesAsync(ct);
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime()
        };
}
