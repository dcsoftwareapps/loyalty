using LoyaltyCloud.Domain.Enums;
namespace LoyaltyCloud.Application.Billing;
public sealed record BillingNotification(Guid TenantId, string? Recipient, BillingNotificationType Type, string ExternalId, decimal? Amount, string? Currency, DateTime? EffectiveUtc, DateTime? GraceEndsUtc, string BillingUrl);
public interface IBillingNotificationService { Task SendAsync(BillingNotification notification, CancellationToken ct = default); }
