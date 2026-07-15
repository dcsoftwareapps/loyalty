using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Enums;

namespace KBeauty.Loyalty.Application.Common.Interfaces;

public interface INotificationChannelProcessor
{
    NotificationChannel Channel { get; }
    Task ProcessAsync(LoyaltyNotification notification, NotificationDelivery delivery, CancellationToken ct = default);
}
