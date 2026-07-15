using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace KBeauty.Loyalty.Infrastructure.Services;

internal sealed class AppleWalletNotificationChannelProcessor : INotificationChannelProcessor
{
    private readonly ILoyaltyCardRepository _cards;
    private readonly IDeviceRegistrationRepository _devices;
    private readonly IApnService _apn;
    private readonly IDateTimeProvider _dt;
    private readonly ILogger<AppleWalletNotificationChannelProcessor> _logger;

    public NotificationChannel Channel => NotificationChannel.AppleWallet;

    public AppleWalletNotificationChannelProcessor(
        ILoyaltyCardRepository cards,
        IDeviceRegistrationRepository devices,
        IApnService apn,
        IDateTimeProvider dt,
        ILogger<AppleWalletNotificationChannelProcessor> logger)
    {
        _cards = cards;
        _devices = devices;
        _apn = apn;
        _dt = dt;
        _logger = logger;
    }

    public async Task ProcessAsync(LoyaltyNotification notification, NotificationDelivery delivery, CancellationToken ct = default)
    {
        var now = _dt.UtcNow;
        delivery.MarkProcessing(now);

        var card = await _cards.GetByIdAsync(notification.LoyaltyCardId, ct);
        if (card is null)
        {
            delivery.MarkCompleted(NotificationDeliveryStatus.Failed, now, failureReason: "Tarjeta no encontrada.");
            return;
        }

        card.Touch(_dt);
        _cards.Update(card);

        var devices = await _devices.GetBySerialNumberAsync(card.SerialNumber, ct);
        if (devices.Count == 0)
        {
            delivery.MarkCompleted(NotificationDeliveryStatus.NoRecipients, now, devicesFound: 0, failureReason: "Sin dispositivos registrados.");
            _logger.LogInformation("Notification {NotificationId}: no Wallet devices for serial {Serial}.", notification.Id, card.SerialNumber);
            return;
        }

        var attempted = 0;
        var accepted = 0;
        var failed = 0;
        foreach (var device in devices)
        {
            attempted++;
            try
            {
                await _apn.SendPassUpdateAsync(device.PushToken, PassUpdateReason.LevelChanged, ct);
                accepted++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogWarning(ex, "Notification {NotificationId}: Wallet APN failed for serial {Serial}.", notification.Id, card.SerialNumber);
            }
        }

        var status = failed == 0 ? NotificationDeliveryStatus.Succeeded : NotificationDeliveryStatus.Failed;
        delivery.MarkCompleted(
            status,
            _dt.UtcNow,
            devicesFound: devices.Count,
            pushesAttempted: attempted,
            pushesAccepted: accepted,
            pushesFailed: failed,
            providerReference: "apns-passkit",
            failureReason: failed == 0 ? null : "Uno o mas pushes fallaron.");

        _logger.LogInformation(
            "Notification {NotificationId}: Apple Wallet APNs summary for serial {Serial}: devices={Devices}, attempted={Attempted}, accepted={Accepted}, failed={Failed}.",
            notification.Id,
            card.SerialNumber,
            devices.Count,
            attempted,
            accepted,
            failed);
    }
}
