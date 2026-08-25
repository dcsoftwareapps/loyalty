using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class AppleWalletPassRefreshService : IAppleWalletPassRefreshService
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dt;
    private readonly IApnService _apn;
    private readonly ILogger<AppleWalletPassRefreshService> _logger;

    public AppleWalletPassRefreshService(
        AppDbContext db,
        IDateTimeProvider dt,
        IApnService apn,
        ILogger<AppleWalletPassRefreshService> logger)
    {
        _db = db;
        _dt = dt;
        _apn = apn;
        _logger = logger;
    }

    public async Task<AppleWalletPassRefreshResult> RefreshCardAsync(
        Guid tenantId,
        Guid loyaltyCardId,
        PassUpdateReason reason,
        CancellationToken ct = default)
    {
        var card = await _db.LoyaltyCards
            .Where(c => c.TenantId == tenantId && c.Id == loyaltyCardId && c.IsActive)
            .Select(c => new { c.SerialNumber })
            .FirstOrDefaultAsync(ct);

        if (card is null)
        {
            _logger.LogWarning(
                "Apple Wallet pass refresh skipped because active LoyaltyCard was not found. TenantId={TenantId}, LoyaltyCardId={LoyaltyCardId}, reason={Reason}.",
                tenantId,
                loyaltyCardId,
                reason);

            return new AppleWalletPassRefreshResult(
                tenantId,
                [],
                CardsTouched: 0,
                DevicesFound: 0,
                PushesAttempted: 0,
                PushesAccepted: 0,
                PushesFailed: 0,
                Unsupported: false,
                ApnPushFailureType.Permanent,
                "Tarjeta no encontrada.");
        }

        return await RefreshSerialsAsync(tenantId, [card.SerialNumber], reason, ct);
    }

    public async Task<AppleWalletPassRefreshResult> RefreshTenantInstalledPassesAsync(
        Guid tenantId,
        PassUpdateReason reason,
        CancellationToken ct = default)
    {
        var serials = await _db.DeviceRegistrations
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .Select(d => d.SerialNumber)
            .Distinct()
            .ToListAsync(ct);

        return await RefreshSerialsAsync(tenantId, serials, reason, ct);
    }

    private async Task<AppleWalletPassRefreshResult> RefreshSerialsAsync(
        Guid tenantId,
        IReadOnlyCollection<string> serialNumbers,
        PassUpdateReason reason,
        CancellationToken ct)
    {
        var normalizedSerials = serialNumbers
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        _logger.LogInformation(
            "Apple Wallet pass refresh started. TenantId={TenantId}, reason={Reason}, serials=[{Serials}], apnService={ApnService}.",
            tenantId,
            reason,
            string.Join(", ", normalizedSerials),
            _apn.GetType().Name);

        if (normalizedSerials.Length == 0)
        {
            return new AppleWalletPassRefreshResult(
                tenantId,
                [],
                CardsTouched: 0,
                DevicesFound: 0,
                PushesAttempted: 0,
                PushesAccepted: 0,
                PushesFailed: 0,
                Unsupported: false,
                ApnPushFailureType.None,
                "Sin tarjetas para actualizar.");
        }

        var devices = await _db.DeviceRegistrations
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && normalizedSerials.Contains(d.SerialNumber))
            .Select(d => new { d.SerialNumber, d.PushToken })
            .ToListAsync(ct);

        var cards = await _db.LoyaltyCards
            .Where(c => c.TenantId == tenantId && c.IsActive && normalizedSerials.Contains(c.SerialNumber))
            .ToListAsync(ct);

        foreach (var card in cards)
        {
            var before = card.LastActivityAt;
            card.Touch(_dt);
            _logger.LogInformation(
                "Apple Wallet pass timestamp updated. TenantId={TenantId}, Serial={Serial}, reason={Reason}, LastActivityAtBefore={Before:O}, LastActivityAtAfter={After:O}.",
                tenantId,
                card.SerialNumber,
                reason,
                before,
                card.LastActivityAt);
        }

        await _db.SaveChangesAsync(ct);

        if (devices.Count == 0)
        {
            _logger.LogInformation(
                "Apple Wallet pass refresh found no registered devices. TenantId={TenantId}, reason={Reason}, serials=[{Serials}], CardsTouched={CardsTouched}.",
                tenantId,
                reason,
                string.Join(", ", normalizedSerials),
                cards.Count);

            return new AppleWalletPassRefreshResult(
                tenantId,
                normalizedSerials,
                cards.Count,
                DevicesFound: 0,
                PushesAttempted: 0,
                PushesAccepted: 0,
                PushesFailed: 0,
                Unsupported: false,
                ApnPushFailureType.None,
                "Sin dispositivos registrados.");
        }

        var attempted = 0;
        var accepted = 0;
        var failed = 0;
        var unsupported = false;
        var worstFailure = ApnPushFailureType.None;
        string? failureReason = null;

        foreach (var device in devices)
        {
            attempted++;
            ApnPushResult result;
            try
            {
                result = await _apn.SendPassUpdateAsync(device.PushToken, reason, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Apple Wallet APNs threw unexpectedly. TenantId={TenantId}, Serial={Serial}, reason={Reason}.",
                    tenantId,
                    device.SerialNumber,
                    reason);
                result = ApnPushResult.Transient(null, ex.GetType().Name);
            }

            if (result.Success)
            {
                accepted++;
                _logger.LogInformation(
                    "Apple Wallet APNs accepted. TenantId={TenantId}, Serial={Serial}, reason={Reason}, status={StatusCode}.",
                    tenantId,
                    device.SerialNumber,
                    reason,
                    result.StatusCode);
                continue;
            }

            failed++;
            unsupported |= result.FailureType == ApnPushFailureType.Unsupported;
            worstFailure = SelectWorstFailure(worstFailure, result.FailureType);
            failureReason ??= FormatFailureReason(result);
            _logger.LogWarning(
                "Apple Wallet APNs was not accepted. TenantId={TenantId}, Serial={Serial}, reason={Reason}, status={StatusCode}, apnsReason={ApnsReason}, failureType={FailureType}.",
                tenantId,
                device.SerialNumber,
                reason,
                result.StatusCode,
                result.Reason ?? "none",
                result.FailureType);
        }

        _logger.LogInformation(
            "Apple Wallet pass refresh finished. TenantId={TenantId}, reason={Reason}, CardsTouched={CardsTouched}, DevicesFound={DevicesFound}, PushesAttempted={Attempted}, PushesAccepted={Accepted}, PushesFailed={Failed}, Unsupported={Unsupported}, FailureType={FailureType}.",
            tenantId,
            reason,
            cards.Count,
            devices.Count,
            attempted,
            accepted,
            failed,
            unsupported,
            worstFailure);

        return new AppleWalletPassRefreshResult(
            tenantId,
            normalizedSerials,
            cards.Count,
            devices.Count,
            attempted,
            accepted,
            failed,
            unsupported,
            worstFailure,
            failureReason);
    }

    private static ApnPushFailureType SelectWorstFailure(ApnPushFailureType current, ApnPushFailureType candidate)
    {
        if (candidate == ApnPushFailureType.Unsupported)
            return ApnPushFailureType.Unsupported;
        if (candidate == ApnPushFailureType.Transient && current != ApnPushFailureType.Unsupported)
            return ApnPushFailureType.Transient;
        if (candidate == ApnPushFailureType.Permanent && current == ApnPushFailureType.None)
            return ApnPushFailureType.Permanent;
        return current;
    }

    private static string FormatFailureReason(ApnPushResult result)
    {
        var prefix = result.FailureType switch
        {
            ApnPushFailureType.Transient => NotificationDeliveryFailureReasons.TransientApnsFailurePrefix,
            ApnPushFailureType.Permanent => NotificationDeliveryFailureReasons.PermanentApnsFailurePrefix,
            ApnPushFailureType.Unsupported => NotificationDeliveryFailureReasons.UnsupportedApnsPrefix,
            _ => "APNs failure"
        };

        var status = result.StatusCode.HasValue ? result.StatusCode.Value.ToString() : "no-status";
        var reason = string.IsNullOrWhiteSpace(result.Reason) ? "unknown" : result.Reason;
        return $"{prefix}: status={status}; reason={reason}";
    }
}
