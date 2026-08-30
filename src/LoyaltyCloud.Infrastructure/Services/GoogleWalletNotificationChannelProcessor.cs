using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Services.GoogleWallet;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class GoogleWalletNotificationChannelProcessor : INotificationChannelProcessor
{
    private readonly IMemberDigitalWalletRepository _wallets;
    private readonly IGoogleWalletClient _client;
    private readonly IDateTimeProvider _dt;
    private readonly ILogger<GoogleWalletNotificationChannelProcessor> _logger;

    public NotificationChannel Channel => NotificationChannel.GoogleWallet;

    public GoogleWalletNotificationChannelProcessor(
        IMemberDigitalWalletRepository wallets,
        IGoogleWalletClient client,
        IDateTimeProvider dt,
        ILogger<GoogleWalletNotificationChannelProcessor> logger)
    {
        _wallets = wallets;
        _client = client;
        _dt = dt;
        _logger = logger;
    }

    public async Task ProcessAsync(
        LoyaltyNotification notification,
        NotificationDelivery delivery,
        CancellationToken ct = default)
    {
        delivery.MarkProcessing(_dt.UtcNow);
        var wallet = await _wallets.GetByLoyaltyCardAndProviderAsync(
            notification.LoyaltyCardId,
            DigitalWalletProvider.Google,
            ct);

        if (wallet is null || wallet.TenantId != notification.TenantId)
        {
            Fail(delivery, "Google Wallet no registrado para el cliente.");
            LogResult(notification, wallet, "WalletUnavailable");
            return;
        }

        if (wallet.Status != DigitalWalletStatus.Active ||
            string.IsNullOrWhiteSpace(wallet.ExternalObjectId) ||
            string.IsNullOrWhiteSpace(wallet.ExternalClassId))
        {
            Fail(delivery, "Google Wallet no esta activo o no tiene identificadores validos.");
            LogResult(notification, wallet, "WalletUnavailable");
            return;
        }

        try
        {
            var header = string.IsNullOrWhiteSpace(notification.ShortMessage)
                ? notification.Title
                : notification.ShortMessage;
            var body = string.IsNullOrWhiteSpace(notification.LongMessage)
                ? notification.Message
                : notification.LongMessage;

            await _client.AddMessageAsync(
                wallet.ExternalObjectId,
                header,
                body,
                $"notification-{notification.Id:N}",
                ct);

            delivery.MarkCompleted(
                NotificationDeliveryStatus.Succeeded,
                _dt.UtcNow,
                devicesFound: 1,
                pushesAttempted: 1,
                pushesAccepted: 1,
                providerReference: "google-wallet-add-message");
            LogResult(notification, wallet, "ProviderAccepted");
        }
        catch (Exception ex)
        {
            var reason = SafeFailure(ex);
            Fail(delivery, reason);
            _logger.LogWarning(
                "Google Wallet notification failed. TenantId={TenantId}, MemberId={MemberId}, WalletProvider=Google, ObjectId={ObjectId}, ClassId={ClassId}, Result=Failed, FailureReason={FailureReason}.",
                notification.TenantId,
                notification.CustomerId,
                Redact(wallet.ExternalObjectId),
                Redact(wallet.ExternalClassId),
                reason);
        }
    }

    private void Fail(NotificationDelivery delivery, string reason) =>
        delivery.MarkCompleted(
            NotificationDeliveryStatus.Failed,
            _dt.UtcNow,
            devicesFound: 0,
            pushesAttempted: 1,
            pushesFailed: 1,
            providerReference: "google-wallet-add-message",
            failureReason: reason);

    private void LogResult(LoyaltyNotification notification, MemberDigitalWallet? wallet, string result) =>
        _logger.LogInformation(
            "Google Wallet notification attempt. TenantId={TenantId}, MemberId={MemberId}, WalletProvider=Google, ObjectId={ObjectId}, ClassId={ClassId}, Result={Result}.",
            notification.TenantId,
            notification.CustomerId,
            Redact(wallet?.ExternalObjectId),
            Redact(wallet?.ExternalClassId),
            result);

    private static string Redact(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "missing"
            : $"***{value[^Math.Min(8, value.Length)..]}";

    private static string SafeFailure(Exception ex)
    {
        var message = string.IsNullOrWhiteSpace(ex.Message)
            ? "Google Wallet rechazo el mensaje."
            : ex.Message;
        return message[..Math.Min(message.Length, 500)];
    }
}
