using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface INotificationChannelProcessor
{
    NotificationChannel Channel { get; }
    Task ProcessAsync(LoyaltyNotification notification, NotificationDelivery delivery, CancellationToken ct = default);
}
