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
    private readonly IAppleWalletPassRefreshService _passRefresh;
    private readonly IDateTimeProvider _dt;
    private readonly ILogger<AppleWalletNotificationChannelProcessor> _logger;

    public NotificationChannel Channel => NotificationChannel.AppleWallet;

    public AppleWalletNotificationChannelProcessor(
        ILoyaltyCardRepository cards,
        IAppleWalletPassRefreshService passRefresh,
        IDateTimeProvider dt,
        ILogger<AppleWalletNotificationChannelProcessor> logger)
    {
        _cards = cards;
        _passRefresh = passRefresh;
        _dt = dt;
        _logger = logger;
    }

    public async Task ProcessAsync(LoyaltyNotification notification, NotificationDelivery delivery, CancellationToken ct = default)
    {
        var now = _dt.UtcNow;
        _logger.LogInformation(
            "Notification {NotificationId}: Apple Wallet processing started. Delivery={DeliveryId}, loyaltyCardId={LoyaltyCardId}.",
            notification.Id,
            delivery.Id,
            notification.LoyaltyCardId);

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

        var refresh = await _passRefresh.RefreshCardAsync(
            notification.TenantId,
            notification.LoyaltyCardId,
            ToPassUpdateReason(notification.Type),
            ct);

        if (!refresh.HasRecipients)
        {
            delivery.MarkCompleted(
                NotificationDeliveryStatus.NoRecipients,
                _dt.UtcNow,
                devicesFound: 0,
                failureReason: refresh.FailureReason ?? "Sin dispositivos registrados.");
            _logger.LogInformation(
                "Notification {NotificationId}: Skipping APNs because no Wallet devices were registered for serial {Serial}.",
                notification.Id,
                card.SerialNumber);
            return;
        }

        if (refresh.Unsupported)
        {
            delivery.MarkCompleted(
                NotificationDeliveryStatus.Unsupported,
                _dt.UtcNow,
                devicesFound: refresh.DevicesFound,
                pushesAttempted: refresh.PushesAttempted,
                pushesAccepted: refresh.PushesAccepted,
                pushesFailed: refresh.PushesFailed,
                providerReference: "apns-noop",
                failureReason: refresh.FailureReason ?? "APNs real deshabilitado por configuracion.");

            _logger.LogInformation(
                "Notification {NotificationId}: Skipping APNs because NoOpApnService is registered. serial={Serial}, devices={Devices}.",
                notification.Id,
                card.SerialNumber,
                refresh.DevicesFound);
            return;
        }

        var status = refresh.PushesFailed == 0 ? NotificationDeliveryStatus.Succeeded : NotificationDeliveryStatus.Failed;
        delivery.MarkCompleted(
            status,
            _dt.UtcNow,
            devicesFound: refresh.DevicesFound,
            pushesAttempted: refresh.PushesAttempted,
            pushesAccepted: refresh.PushesAccepted,
            pushesFailed: refresh.PushesFailed,
            providerReference: "apns-passkit",
            failureReason: refresh.PushesFailed == 0 ? null : refresh.FailureReason ?? "Uno o mas pushes fallaron.");

        _logger.LogInformation(
            "Notification {NotificationId}: Finished processing Apple Wallet channel for serial {Serial}. devices={Devices}, attempted={Attempted}, accepted={Accepted}, failed={Failed}, deliveryStatus={DeliveryStatus}.",
            notification.Id,
            card.SerialNumber,
            refresh.DevicesFound,
            refresh.PushesAttempted,
            refresh.PushesAccepted,
            refresh.PushesFailed,
            status);
    }

    private static PassUpdateReason ToPassUpdateReason(NotificationType type) => type switch
    {
        NotificationType.PointsAdded => PassUpdateReason.PointsAdded,
        NotificationType.LevelChanged => PassUpdateReason.LevelChanged,
        NotificationType.PointsExpiring => PassUpdateReason.PointsExpired,
        _ => PassUpdateReason.LevelChanged
    };
}
