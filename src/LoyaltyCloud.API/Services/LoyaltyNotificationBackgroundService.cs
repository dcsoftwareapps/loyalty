using LoyaltyCloud.API.Configuration;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Notifications.Custom.Commands.ProcessDueCustomNotificationCampaigns;
using LoyaltyCloud.Application.Notifications.Commands.ProcessPendingNotifications;
using MediatR;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.API.Services;

public sealed class LoyaltyNotificationBackgroundService : BackgroundService
{
    private static readonly TimeSpan MinimumPollInterval = TimeSpan.FromSeconds(15);

    private readonly ITenantExecutionRunner _tenantRunner;
    private readonly IOptionsMonitor<LoyaltyNotificationOptions> _options;
    private readonly IOptionsMonitor<CustomNotificationCampaignOptions> _campaignOptions;
    private readonly ILogger<LoyaltyNotificationBackgroundService> _logger;

    public LoyaltyNotificationBackgroundService(
        ITenantExecutionRunner tenantRunner,
        IOptionsMonitor<LoyaltyNotificationOptions> options,
        IOptionsMonitor<CustomNotificationCampaignOptions> campaignOptions,
        ILogger<LoyaltyNotificationBackgroundService> logger)
    {
        _tenantRunner = tenantRunner;
        _options = options;
        _campaignOptions = campaignOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var runOnStartupExecuted = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.CurrentValue;
            if (!options.Enabled)
            {
                _logger.LogInformation("Loyalty notification processor is disabled.");
                return;
            }

            if (options.RunOnStartup && !runOnStartupExecuted)
            {
                runOnStartupExecuted = true;
                await ProcessPendingAsync(options, stoppingToken);
            }

            var delay = TimeSpan.FromSeconds(Math.Max(options.PollIntervalSeconds, (int)MinimumPollInterval.TotalSeconds));
            await Task.Delay(delay, stoppingToken);
            if (stoppingToken.IsCancellationRequested)
                break;

            await ProcessPendingAsync(options, stoppingToken);
        }
    }

    private async Task ProcessPendingAsync(LoyaltyNotificationOptions options, CancellationToken ct)
    {
        try
        {
            var summary = await _tenantRunner.RunForOperationalTenantsAsync(
                "loyalty-notification-processing",
                async (serviceProvider, tenant, tenantCt) =>
                {
                    var sender = serviceProvider.GetRequiredService<ISender>();
                    var campaignResult = await sender.Send(
                        new ProcessDueCustomNotificationCampaignsCommand(_campaignOptions.CurrentValue.BatchSize),
                        tenantCt);

                    if (campaignResult.IsFailure)
                    {
                        _logger.LogWarning(
                            "Custom notification campaign processing failed. TenantId={TenantId}, TenantSlug={TenantSlug}, Error={Error}",
                            tenant.TenantId,
                            tenant.Slug,
                            campaignResult.Error);
                    }
                    else if (campaignResult.Value > 0)
                    {
                        _logger.LogInformation(
                            "Processed {Count} due custom notification campaign(s). TenantId={TenantId}, TenantSlug={TenantSlug}",
                            campaignResult.Value,
                            tenant.TenantId,
                            tenant.Slug);
                    }

                    var result = await sender.Send(
                        new ProcessPendingNotificationsCommand(options.BatchSize, options.MaxAttempts),
                        tenantCt);

                    if (result.IsFailure)
                    {
                        _logger.LogWarning(
                            "Loyalty notification processing failed. TenantId={TenantId}, TenantSlug={TenantSlug}, Error={Error}",
                            tenant.TenantId,
                            tenant.Slug,
                            result.Error);
                        return;
                    }

                    if (result.Value > 0)
                    {
                        _logger.LogInformation(
                            "Processed {Count} pending loyalty notifications. TenantId={TenantId}, TenantSlug={TenantSlug}",
                            result.Value,
                            tenant.TenantId,
                            tenant.Slug);
                    }
                },
                ct);

            _logger.LogInformation(
                "Finished loyalty notification processing cycle. EligibleTenantCount={EligibleTenantCount}, SucceededTenantCount={SucceededTenantCount}, FailedTenantCount={FailedTenantCount}, SkippedTenantCount={SkippedTenantCount}.",
                summary.EligibleTenantCount,
                summary.SucceededTenantCount,
                summary.FailedTenantCount,
                summary.SkippedTenantCount);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing pending loyalty notifications tenant cycle.");
        }
    }
}
