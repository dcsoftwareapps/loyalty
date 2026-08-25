using LoyaltyCloud.Application.Billing;
using Microsoft.Extensions.Logging;
namespace LoyaltyCloud.Infrastructure.Services;
internal sealed class BillingNotificationService(ILogger<BillingNotificationService> logger) : IBillingNotificationService
{
 public Task SendAsync(BillingNotification n, CancellationToken ct = default)
 {
  if (string.IsNullOrWhiteSpace(n.Recipient)) { logger.LogWarning("Billing notification skipped: no BillingContactEmail. TenantId={TenantId}, Type={Type}.", n.TenantId, n.Type); return Task.CompletedTask; }
  logger.LogInformation("Billing email queued. TenantId={TenantId}, Recipient={Recipient}, Type={Type}, ExternalId={ExternalId}.", n.TenantId, n.Recipient, n.Type, n.ExternalId);
  return Task.CompletedTask;
 }
}
