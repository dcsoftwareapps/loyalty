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

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<LoyaltyNotificationOptions> _options;
    private readonly IOptionsMonitor<CustomNotificationCampaignOptions> _campaignOptions;
    private readonly ILogger<LoyaltyNotificationBackgroundService> _logger;

    public LoyaltyNotificationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<LoyaltyNotificationOptions> options,
        IOptionsMonitor<CustomNotificationCampaignOptions> campaignOptions,
        ILogger<LoyaltyNotificationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
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
            using var scope = _scopeFactory.CreateScope();
            var tenantResolver = scope.ServiceProvider.GetRequiredService<IDefaultTenantResolutionService>();
            await tenantResolver.ResolveDefaultTenantIfMissingAsync(ct);
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var campaignResult = await sender.Send(
                new ProcessDueCustomNotificationCampaignsCommand(_campaignOptions.CurrentValue.BatchSize),
                ct);

            if (campaignResult.IsFailure)
            {
                _logger.LogWarning("Custom notification campaign processing failed: {Error}", campaignResult.Error);
            }
            else if (campaignResult.Value > 0)
            {
                _logger.LogInformation("Processed {Count} due custom notification campaign(s).", campaignResult.Value);
            }

            var result = await sender.Send(new ProcessPendingNotificationsCommand(options.BatchSize, options.MaxAttempts), ct);

            if (result.IsFailure)
            {
                _logger.LogWarning("Loyalty notification processing failed: {Error}", result.Error);
                return;
            }

            if (result.Value > 0)
                _logger.LogInformation("Processed {Count} pending loyalty notifications.", result.Value);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing pending loyalty notifications.");
        }
    }
}
