using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class AppleWalletNotificationChannelProcessor : INotificationChannelProcessor
{
    private readonly ILoyaltyCardRepository _cards;
    private readonly IDeviceRegistrationRepository _devices;
    private readonly IApnService _apn;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<AppleWalletNotificationChannelProcessor> _logger;

    public NotificationChannel Channel => NotificationChannel.AppleWallet;

    public AppleWalletNotificationChannelProcessor(
        ILoyaltyCardRepository cards,
        IDeviceRegistrationRepository devices,
        IApnService apn,
        IDateTimeProvider dt,
        IUnitOfWork uow,
        ILogger<AppleWalletNotificationChannelProcessor> logger)
    {
        _cards = cards;
        _devices = devices;
        _apn = apn;
        _dt = dt;
        _uow = uow;
        _logger = logger;
    }

    public async Task ProcessAsync(LoyaltyNotification notification, NotificationDelivery delivery, CancellationToken ct = default)
    {
        var now = _dt.UtcNow;
        _logger.LogInformation(
            "Notification {NotificationId}: Apple Wallet processing started. Delivery={DeliveryId}, loyaltyCardId={LoyaltyCardId}, apnService={ApnService}.",
            notification.Id,
            delivery.Id,
            notification.LoyaltyCardId,
            _apn.GetType().Name);

        delivery.MarkProcessing(now);

        _logger.LogInformation("Notification {NotificationId}: Loading LoyaltyCard...", notification.Id);
        var card = await _cards.GetByIdAsync(notification.LoyaltyCardId, ct);
        if (card is null)
        {
            _logger.LogInformation(
                "Notification {NotificationId}: Skipping APNs because LoyaltyCard {LoyaltyCardId} was not found.",
                notification.Id,
                notification.LoyaltyCardId);
            delivery.MarkCompleted(NotificationDeliveryStatus.Failed, now, failureReason: "Tarjeta no encontrada.");
            return;
        }

        _logger.LogInformation(
            "Notification {NotificationId}: LoyaltyCard loaded. serial={Serial}, LastActivityAt={LastActivityAt:O}.",
            notification.Id,
            card.SerialNumber,
            card.LastActivityAt);

        var lastActivityBefore = card.LastActivityAt;
        card.Touch(_dt);
        _cards.Update(card);
        _logger.LogInformation(
            "Notification {NotificationId}: Saving LastActivityAt for serial {Serial}. before={LastActivityAtBefore:O}, after={LastActivityAtAfter:O}.",
            notification.Id,
            card.SerialNumber,
            lastActivityBefore,
            card.LastActivityAt);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Notification {NotificationId}: SaveChanges completed. Apple Wallet pass timestamp updated for serial {Serial}. LastActivityAt before={LastActivityAtBefore:O}, after={LastActivityAtAfter:O}.",
            notification.Id,
            card.SerialNumber,
            lastActivityBefore,
            card.LastActivityAt);

        _logger.LogInformation(
            "Notification {NotificationId}: Looking for registered devices for serial {Serial}...",
            notification.Id,
            card.SerialNumber);
        var devices = await _devices.GetBySerialNumberAsync(card.SerialNumber, ct);
        _logger.LogInformation(
            "Notification {NotificationId}: Found {DeviceCount} registered devices for serial {Serial}.",
            notification.Id,
            devices.Count,
            card.SerialNumber);

        if (devices.Count == 0)
        {
            delivery.MarkCompleted(NotificationDeliveryStatus.NoRecipients, now, devicesFound: 0, failureReason: "Sin dispositivos registrados.");
            _logger.LogInformation(
                "Notification {NotificationId}: Skipping APNs because no Wallet devices were registered for serial {Serial}.",
                notification.Id,
                card.SerialNumber);
            return;
        }

        if (_apn is NoOpApnService)
        {
            delivery.MarkCompleted(
                NotificationDeliveryStatus.Unsupported,
                _dt.UtcNow,
                devicesFound: devices.Count,
                pushesAttempted: 0,
                pushesAccepted: 0,
                pushesFailed: 0,
                providerReference: "apns-noop",
                failureReason: "APNs real deshabilitado por configuracion.");

            _logger.LogInformation(
                "Notification {NotificationId}: Skipping APNs because NoOpApnService is registered. serial={Serial}, devices={Devices}.",
                notification.Id,
                card.SerialNumber,
                devices.Count);
            return;
        }

        var attempted = 0;
        var accepted = 0;
        var failed = 0;
        foreach (var device in devices)
        {
            attempted++;
            _logger.LogInformation(
                "Notification {NotificationId}: Device {Device} pushToken={PushToken}.",
                notification.Id,
                SafeDeviceIdentifier(device.DeviceLibraryIdentifier),
                SafePushToken(device.PushToken));

            try
            {
                _logger.LogInformation(
                    "Notification {NotificationId}: Sending APNs for serial {Serial} to device {Device}.",
                    notification.Id,
                    card.SerialNumber,
                    SafeDeviceIdentifier(device.DeviceLibraryIdentifier));
                await _apn.SendPassUpdateAsync(device.PushToken, PassUpdateReason.LevelChanged, ct);
                accepted++;
                _logger.LogInformation(
                    "Notification {NotificationId}: APNs accepted for serial {Serial} and device {Device}.",
                    notification.Id,
                    card.SerialNumber,
                    SafeDeviceIdentifier(device.DeviceLibraryIdentifier));
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogWarning(
                    ex,
                    "Notification {NotificationId}: APNs failed for serial {Serial} and device {Device}.",
                    notification.Id,
                    card.SerialNumber,
                    SafeDeviceIdentifier(device.DeviceLibraryIdentifier));
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
            "Notification {NotificationId}: Finished processing Apple Wallet channel for serial {Serial}. devices={Devices}, attempted={Attempted}, accepted={Accepted}, failed={Failed}, deliveryStatus={DeliveryStatus}.",
            notification.Id,
            card.SerialNumber,
            devices.Count,
            attempted,
            accepted,
            failed,
            status);
    }

    private static string SafeDeviceIdentifier(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? "empty"
            : value.Length <= 8
                ? value
                : $"{value[..4]}...{value[^4..]}";

    private static string SafePushToken(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? "empty"
            : value.Length <= 12
                ? $"{value[..Math.Min(value.Length, 4)]}..."
                : $"{value[..6]}...{value[^6..]}";
}
