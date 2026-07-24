using System.Text.Json;
using LoyaltyCloud.Application.Common.Events;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Events;
using LoyaltyCloud.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Application.Notifications.Events;

public sealed class LevelUpgradedNotificationHandler
    : INotificationHandler<DomainEventNotification<LevelUpgradedEvent>>
{
    private readonly ILoyaltyCardRepository _cards;
    private readonly ILoyaltyNotificationService _notifications;
    private readonly ILevelCalculationService _levels;
    private readonly ITenantLoyaltyLevelReadService _tenantLevels;
    private readonly ILogger<LevelUpgradedNotificationHandler> _logger;

    public LevelUpgradedNotificationHandler(
        ILoyaltyCardRepository cards,
        ILoyaltyNotificationService notifications,
        ILevelCalculationService levels,
        ITenantLoyaltyLevelReadService tenantLevels,
        ILogger<LevelUpgradedNotificationHandler> logger)
    {
        _cards = cards;
        _notifications = notifications;
        _levels = levels;
        _tenantLevels = tenantLevels;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<LevelUpgradedEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        var tenantLevels = await _tenantLevels.GetActiveLevelsAsync(ct);
        if (_levels.CompareLevels(e.OldLevel, e.NewLevel, tenantLevels) <= 0)
            return;

        var card = await _cards.GetByIdAsync(e.CardId, ct);
        if (card is null)
            return;

        var title = "Subiste de nivel!";
        var message = $"Ahora eres cliente {e.NewLevel}";
        var correlation = $"level-changed:{card.Id}:{e.NewLevel}:{card.LevelAchievedAt:O}";
        var metadata = JsonSerializer.Serialize(new
        {
            previousLevel = e.OldLevel,
            newLevel = e.NewLevel,
            isUpgrade = true
        });

        try
        {
            _logger.LogInformation(
                "Creating LevelChanged notification for card {CardId}: {OldLevel} -> {NewLevel}.",
                card.Id,
                e.OldLevel,
                e.NewLevel);

            await _notifications.CreateAsync(new CreateLoyaltyNotificationRequest(
                card.SerialNumber,
                NotificationType.LevelChanged,
                title,
                message,
                ScheduledAtUtc: null,
                DisplayUntilUtc: DateTime.UtcNow.AddDays(7),
                Channels: [NotificationChannel.AppleWallet],
                CorrelationId: correlation,
                Source: "level-upgraded-event",
                MetadataJson: metadata,
                ProcessImmediately: true), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo crear notificacion LevelChanged para tarjeta {CardId}.", card.Id);
        }
    }

}
