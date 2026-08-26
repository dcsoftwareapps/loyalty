using LoyaltyCloud.Domain.Enums;
namespace LoyaltyCloud.Application.Billing;

public sealed record BillingNotification(
    Guid TenantId,
    string? Recipient,
    BillingNotificationType Type,
    string ExternalId,
    decimal? Amount,
    string? Currency,
    DateTime? EffectiveUtc,
    DateTime? GraceEndsUtc,
    string BillingUrl,
    string BusinessName = "Tu negocio",
    int? PeriodMonths = null,
    DateTime? PaidThroughUtc = null,
    DateTime? NextRenewalUtc = null,
    string? CardBrand = null,
    string? CardLast4 = null);

public sealed record TransactionalEmail(string Recipient, string Subject, string TextBody, string HtmlBody, string FromAddress, string FromName);
public interface IBillingEmailConfigurationProvider { Task<BillingEmailSettingsDto> GetAsync(CancellationToken ct = default); }
public interface ITransactionalEmailSender { Task SendAsync(TransactionalEmail email, CancellationToken ct = default); }
public interface IBillingNotificationService { Task SendAsync(BillingNotification notification, CancellationToken ct = default); }
