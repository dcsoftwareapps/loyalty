using LoyaltyCloud.Domain.Common;
using LoyaltyCloud.Domain.Enums;
namespace LoyaltyCloud.Domain.Entities;
public sealed class PaymentWebhookEvent : Entity
{
    public PaymentProvider Provider { get; private set; } public string ProviderEventId { get; private set; }=string.Empty;
    public string EventType { get; private set; }=string.Empty; public DateTime ReceivedAt { get; private set; } public DateTime? ProcessedAt { get; private set; }
    public WebhookProcessingStatus ProcessingStatus { get; private set; }
    private PaymentWebhookEvent() { }
    public PaymentWebhookEvent(Guid id, PaymentProvider provider, string eventId, string type, DateTime nowUtc):base(id){Provider=provider;ProviderEventId=eventId;EventType=type;ReceivedAt=nowUtc;ProcessingStatus=WebhookProcessingStatus.Received;}
    public void Processed(DateTime nowUtc){ProcessingStatus=WebhookProcessingStatus.Processed;ProcessedAt=nowUtc;}
    public void Fail(){ProcessingStatus=WebhookProcessingStatus.Failed;}
}
