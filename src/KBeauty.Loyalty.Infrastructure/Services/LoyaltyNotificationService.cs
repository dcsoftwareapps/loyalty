using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Application.Notifications;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Domain.Repositories;
using KBeauty.Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KBeauty.Loyalty.Infrastructure.Services;

internal sealed class LoyaltyNotificationService : ILoyaltyNotificationService
{
    private readonly ILoyaltyNotificationRepository _notifications;
    private readonly ILoyaltyCardRepository _cards;
    private readonly ICustomerRepository _customers;
    private readonly IEnumerable<INotificationChannelProcessor> _processors;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _db;
    private readonly ILogger<LoyaltyNotificationService> _logger;

    public LoyaltyNotificationService(
        ILoyaltyNotificationRepository notifications,
        ILoyaltyCardRepository cards,
        ICustomerRepository customers,
        IEnumerable<INotificationChannelProcessor> processors,
        IDateTimeProvider dt,
        IUnitOfWork uow,
        AppDbContext db,
        ILogger<LoyaltyNotificationService> logger)
    {
        _notifications = notifications;
        _cards = cards;
        _customers = customers;
        _processors = processors;
        _dt = dt;
        _uow = uow;
        _db = db;
        _logger = logger;
    }

    public async Task<NotificationDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var notification = await _notifications.GetByIdAsync(id, ct);
        return notification is null ? null : await MapAsync(notification, ct);
    }

    public async Task<IReadOnlyList<NotificationDto>> ListAsync(
        Guid? customerId,
        NotificationType? type,
        NotificationStatus? status,
        NotificationChannel? channel,
        DateTime? fromUtc,
        DateTime? toUtc,
        int take,
        CancellationToken ct = default)
    {
        var rows = await _notifications.ListAsync(customerId, type, status, channel, fromUtc, toUtc, take, ct);
        var list = new List<NotificationDto>();
        foreach (var row in rows)
            list.Add(await MapAsync(row, ct));
        return list.AsReadOnly();
    }

    public async Task<NotificationMetricsDto> GetMetricsAsync(CancellationToken ct = default)
    {
        var pending = await _db.LoyaltyNotifications.AsNoTracking().CountAsync(n => n.Status == NotificationStatus.Pending, ct);
        var processed = await _db.LoyaltyNotifications.AsNoTracking().CountAsync(n => n.Status == NotificationStatus.Delivered || n.Status == NotificationStatus.PartiallyDelivered, ct);
        var failed = await _db.LoyaltyNotifications.AsNoTracking().CountAsync(n => n.Status == NotificationStatus.Failed, ct);
        var customersReached = await _db.LoyaltyNotifications.AsNoTracking()
            .Where(n => n.Status == NotificationStatus.Delivered || n.Status == NotificationStatus.PartiallyDelivered)
            .Select(n => n.CustomerId)
            .Distinct()
            .CountAsync(ct);
        var pushesAttempted = await _db.NotificationDeliveries.AsNoTracking().SumAsync(d => (int?)d.PushesAttempted, ct) ?? 0;
        var pushesFailed = await _db.NotificationDeliveries.AsNoTracking().SumAsync(d => (int?)d.PushesFailed, ct) ?? 0;
        return new NotificationMetricsDto(pending, processed, failed, customersReached, pushesAttempted, pushesFailed);
    }

    public async Task<NotificationDto> CreateAsync(CreateLoyaltyNotificationRequest request, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            var existing = await _notifications.GetByCorrelationIdAsync(request.CorrelationId, ct);
            if (existing is not null)
            {
                _logger.LogInformation("Notification duplicate avoided for correlation {CorrelationId}.", request.CorrelationId);
                return await MapAsync(existing, ct);
            }
        }

        var card = await _cards.GetBySerialNumberAsync(request.SerialNumber, ct)
            ?? throw new InvalidOperationException($"No se encontro tarjeta '{request.SerialNumber}'.");
        var customer = await _customers.GetByIdAsync(card.CustomerId, ct)
            ?? throw new InvalidOperationException("No se encontro cliente asociado.");

        var channels = request.Channels.Count == 0
            ? [NotificationChannel.AppleWallet]
            : request.Channels.Distinct().ToArray();
        var now = _dt.UtcNow;
        var notification = new LoyaltyNotification(
            Guid.NewGuid(),
            customer.Id,
            card.Id,
            request.Type,
            request.Title,
            request.Message,
            now,
            request.ScheduledAtUtc,
            request.DisplayUntilUtc,
            request.CorrelationId,
            request.Source,
            request.MetadataJson);

        foreach (var channel in channels)
            notification.AddDelivery(new NotificationDelivery(Guid.NewGuid(), notification.Id, channel, now));

        await _notifications.AddAsync(notification, ct);
        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Notification {NotificationId} created ({Type}).", notification.Id, notification.Type);

        if (request.ProcessImmediately && (!request.ScheduledAtUtc.HasValue || request.ScheduledAtUtc <= now))
            return await ProcessAsync(notification.Id, ct);

        return await MapAsync(notification, ct);
    }

    public async Task<NotificationDto> ProcessAsync(Guid id, CancellationToken ct = default)
    {
        var notification = await _notifications.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"No se encontro notificacion {id}.");

        if (notification.Status == NotificationStatus.Cancelled)
            return await MapAsync(notification, ct);

        notification.MarkProcessing(_dt.UtcNow);
        foreach (var delivery in notification.Deliveries.Where(d => d.Status == NotificationDeliveryStatus.Pending))
        {
            var processor = _processors.FirstOrDefault(p => p.Channel == delivery.Channel);
            if (processor is null)
            {
                delivery.MarkProcessing(_dt.UtcNow);
                delivery.MarkCompleted(NotificationDeliveryStatus.Unsupported, _dt.UtcNow, failureReason: "Canal no soportado en Fase 5.1.");
                continue;
            }

            await processor.ProcessAsync(notification, delivery, ct);
        }

        var statuses = notification.Deliveries.Select(d => d.Status).ToList();
        var finalStatus =
            statuses.All(s => s is NotificationDeliveryStatus.Succeeded or NotificationDeliveryStatus.NoRecipients or NotificationDeliveryStatus.Unsupported)
                ? NotificationStatus.Delivered
                : statuses.Any(s => s is NotificationDeliveryStatus.Succeeded or NotificationDeliveryStatus.NoRecipients)
                    ? NotificationStatus.PartiallyDelivered
                    : NotificationStatus.Failed;

        var failure = finalStatus == NotificationStatus.Failed
            ? string.Join(" | ", notification.Deliveries.Select(d => d.FailureReason).Where(x => !string.IsNullOrWhiteSpace(x)))
            : null;
        notification.MarkCompleted(finalStatus, _dt.UtcNow, failure);
        _notifications.Update(notification);
        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Notification {NotificationId} processed with status {Status}.", notification.Id, notification.Status);
        return await MapAsync(notification, ct);
    }

    public async Task<NotificationDto> RetryAsync(Guid id, CancellationToken ct = default)
    {
        var notification = await _notifications.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"No se encontro notificacion {id}.");

        foreach (var delivery in notification.Deliveries)
            delivery.ResetForRetry();

        notification.MarkPendingForRetry();
        _notifications.Update(notification);
        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Notification {NotificationId} queued for retry.", id);
        return await ProcessAsync(id, ct);
    }

    public async Task<NotificationDto> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var notification = await _notifications.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"No se encontro notificacion {id}.");

        notification.Cancel(_dt.UtcNow);
        foreach (var delivery in notification.Deliveries.Where(d => d.Status == NotificationDeliveryStatus.Pending))
            delivery.Cancel(_dt.UtcNow);
        _notifications.Update(notification);
        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Notification {NotificationId} cancelled.", id);
        return await MapAsync(notification, ct);
    }

    public async Task<int> ProcessPendingAsync(int batchSize, int maxAttempts, CancellationToken ct = default)
    {
        var pending = await _notifications.GetPendingDueAsync(_dt.UtcNow, batchSize, maxAttempts, ct);
        foreach (var notification in pending)
            await ProcessAsync(notification.Id, ct);
        return pending.Count;
    }

    private async Task<NotificationDto> MapAsync(LoyaltyNotification notification, CancellationToken ct)
    {
        var baseInfo = await (
            from card in _db.LoyaltyCards.AsNoTracking()
            join customer in _db.Customers.AsNoTracking() on card.CustomerId equals customer.Id
            where card.Id == notification.LoyaltyCardId
            select new { customer.FullName, card.SerialNumber })
            .FirstOrDefaultAsync(ct);

        return new NotificationDto(
            notification.Id,
            notification.CustomerId,
            notification.LoyaltyCardId,
            baseInfo?.FullName,
            baseInfo?.SerialNumber,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.Status,
            notification.CreatedAt,
            notification.ScheduledAtUtc,
            notification.DisplayUntilUtc,
            notification.ProcessedAt,
            notification.CorrelationId,
            notification.Source,
            notification.FailureReason,
            notification.Deliveries.Select(d => new NotificationDeliveryDto(
                d.Id,
                d.Channel,
                d.Status,
                d.AttemptCount,
                d.DevicesFound,
                d.PushesAttempted,
                d.PushesAccepted,
                d.PushesFailed,
                d.FailureReason)).ToList());
    }
}
