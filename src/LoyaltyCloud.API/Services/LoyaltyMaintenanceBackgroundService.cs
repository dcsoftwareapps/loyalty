using System.Diagnostics;
using LoyaltyCloud.API.Configuration;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Levels.Commands.RecalculateLevels;
using LoyaltyCloud.Application.Notifications.Commands.CreateBirthdayBenefitStartedNotifications;
using LoyaltyCloud.Application.Notifications.Commands.CreateMonthlyProductStartedNotifications;
using LoyaltyCloud.Application.Notifications.Commands.CreatePointCampaignStartedNotifications;
using LoyaltyCloud.Application.Notifications.Commands.CreatePointExpirationNotifications;
using LoyaltyCloud.Application.Points.Commands.ExpirePoints;
using MediatR;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.API.Services;

public sealed class LoyaltyMaintenanceBackgroundService : BackgroundService
{
    private const string OperatorId = "loyalty-maintenance";
    private static readonly TimeSpan InvalidConfigurationDelay = TimeSpan.FromHours(1);

    private readonly ITenantExecutionRunner _tenantRunner;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<LoyaltyMaintenanceOptions> _options;
    private readonly ILogger<LoyaltyMaintenanceBackgroundService> _logger;

    public LoyaltyMaintenanceBackgroundService(
        ITenantExecutionRunner tenantRunner,
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<LoyaltyMaintenanceOptions> options,
        ILogger<LoyaltyMaintenanceBackgroundService> logger)
    {
        _tenantRunner = tenantRunner;
        _scopeFactory = scopeFactory;
        _options = options;
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
                _logger.LogInformation("Loyalty maintenance is disabled.");
                return;
            }

            if (!TryResolveInterval(options, out var interval))
            {
                await DelaySafelyAsync(InvalidConfigurationDelay, stoppingToken);
                continue;
            }

            if (options.RunOnStartup && !runOnStartupExecuted)
            {
                runOnStartupExecuted = true;
                _logger.LogInformation("Running loyalty maintenance once on startup.");
                await RunMaintenanceAsync(options, stoppingToken);
            }

            _logger.LogInformation(
                "Next loyalty maintenance scheduled in {IntervalHours} hour(s).",
                interval.TotalHours);

            await DelaySafelyAsync(interval, stoppingToken);
            if (stoppingToken.IsCancellationRequested)
                break;

            await RunMaintenanceAsync(options, stoppingToken);
        }
    }

    private async Task RunMaintenanceAsync(LoyaltyMaintenanceOptions options, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting loyalty maintenance.");

        try
        {
            await RunSubscriptionMaintenanceAsync(ct);

            var summary = await _tenantRunner.RunForOperationalTenantsAsync(
                "loyalty-maintenance",
                async (serviceProvider, tenant, tenantCt) =>
                {
                    var sender = serviceProvider.GetRequiredService<ISender>();

                    await RunExpirationAsync(sender, tenantCt);
                    await RunLevelRecalculationAsync(sender, tenantCt);
                    await RunPointExpirationNotificationsAsync(sender, tenant.TimeZoneId, tenantCt);
                    await RunMonthlyProductNotificationsAsync(sender, tenant.TimeZoneId, tenantCt);
                    await RunBirthdayBenefitNotificationsAsync(sender, tenant.TimeZoneId, tenantCt);
                    await RunPointCampaignNotificationsAsync(sender, tenant.TimeZoneId, tenantCt);
                },
                ct);

            stopwatch.Stop();
            _logger.LogInformation(
                "Finished loyalty maintenance in {ElapsedMilliseconds} ms. EligibleTenantCount={EligibleTenantCount}, SucceededTenantCount={SucceededTenantCount}, FailedTenantCount={FailedTenantCount}, SkippedTenantCount={SkippedTenantCount}.",
                stopwatch.ElapsedMilliseconds,
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
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error running loyalty maintenance tenant cycle.");
        }
    }

    private async Task RunSubscriptionMaintenanceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var maintenance = scope.ServiceProvider.GetRequiredService<ISubscriptionMaintenanceService>();
            var result = await maintenance.ProcessAsync(ct);
            _logger.LogInformation(
                "Subscription maintenance result: tenantsProcessed={TenantsProcessed}, trialsSuspended={TrialsSuspended}, activeMovedToPastDue={ActiveMovedToPastDue}, pastDueSuspended={PastDueSuspended}, failedTenants={FailedTenants}.",
                result.TenantsProcessed,
                result.TrialsSuspended,
                result.ActiveMovedToPastDue,
                result.PastDueSuspended,
                result.FailedTenants);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error running subscription maintenance. Tenant maintenance will continue.");
        }
    }

    private async Task RunPointExpirationNotificationsAsync(
        ISender sender,
        string timeZoneId,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(
                new CreatePointExpirationNotificationsCommand(OperatorId, DaysAhead: 15, TimeZoneId: timeZoneId),
                ct);
            if (result.IsFailure)
            {
                _logger.LogError("Point expiration notification scan failed: {Error}", result.Error);
                return;
            }

            var value = result.Value;
            _logger.LogInformation(
                "Point expiration notification result: targetExpirationDate={TargetExpirationDate}, candidatesFound={CandidatesFound}, notificationsCreated={NotificationsCreated}, alreadyNotified={AlreadyNotified}.",
                value.TargetExpirationDate,
                value.CandidatesFound,
                value.NotificationsCreated,
                value.AlreadyNotified);

            LogWarnings("Point expiration notifications", value.Warnings);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error running point expiration notification scan.");
        }
    }

    private async Task RunMonthlyProductNotificationsAsync(
        ISender sender,
        string timeZoneId,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(
                new CreateMonthlyProductStartedNotificationsCommand(OperatorId, timeZoneId),
                ct);
            if (result.IsFailure)
            {
                _logger.LogError("Monthly product notification scan failed: {Error}", result.Error);
                return;
            }

            var value = result.Value;
            _logger.LogInformation(
                "Monthly product notification result: rewardId={RewardId}, product={ProductName}, cardsEligible={CardsEligible}, notificationsCreated={NotificationsCreated}, alreadyNotified={AlreadyNotified}.",
                value.MonthlyProductId,
                value.MonthlyProductName,
                value.CardsEligible,
                value.NotificationsCreated,
                value.AlreadyNotified);

            LogWarnings("Monthly product notifications", value.Warnings);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error running monthly product notification scan.");
        }
    }

    private async Task RunBirthdayBenefitNotificationsAsync(
        ISender sender,
        string timeZoneId,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(
                new CreateBirthdayBenefitStartedNotificationsCommand(OperatorId, timeZoneId),
                ct);
            if (result.IsFailure)
            {
                _logger.LogError("Birthday benefit notification scan failed: {Error}", result.Error);
                return;
            }

            var value = result.Value;
            _logger.LogInformation(
                "Birthday benefit notification result: localDate={LocalDate}, customersEligible={CustomersEligible}, notificationsCreated={NotificationsCreated}, alreadyNotified={AlreadyNotified}.",
                value.LocalDate,
                value.CustomersEligible,
                value.NotificationsCreated,
                value.AlreadyNotified);

            LogWarnings("Birthday benefit notifications", value.Warnings);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error running birthday benefit notification scan.");
        }
    }

    private async Task RunPointCampaignNotificationsAsync(
        ISender sender,
        string timeZoneId,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(
                new CreatePointCampaignStartedNotificationsCommand(OperatorId, timeZoneId),
                ct);
            if (result.IsFailure)
            {
                _logger.LogError("Point campaign notification scan failed: {Error}", result.Error);
                return;
            }

            var value = result.Value;
            _logger.LogInformation(
                "Point campaign notification result: activeCampaignsFound={ActiveCampaignsFound}, cardsEvaluated={CardsEvaluated}, cardsEligible={CardsEligible}, notificationsCreated={NotificationsCreated}, alreadyNotified={AlreadyNotified}.",
                value.ActiveCampaignsFound,
                value.CardsEvaluated,
                value.CardsEligible,
                value.NotificationsCreated,
                value.AlreadyNotified);

            LogWarnings("Point campaign notifications", value.Warnings);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error running point campaign notification scan.");
        }
    }

    private async Task RunExpirationAsync(ISender sender, CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new ExpirePointsCommand(OperatorId), ct);
            if (result.IsFailure)
            {
                _logger.LogError("Point expiration failed: {Error}", result.Error);
                return;
            }

            var value = result.Value;
            _logger.LogInformation(
                "Point expiration result: enabled={Enabled}, clientsProcessed={ClientsProcessed}, clientsAffected={ClientsAffected}, lotsExpired={LotsExpired}, pointsExpired={PointsExpired}, walletsNotified={WalletsNotified}.",
                value.Enabled,
                value.ClientsProcessed,
                value.ClientsAffected,
                value.LotsExpired,
                value.PointsExpired,
                value.WalletsNotified);

            LogWarnings("Point expiration", value.Warnings);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error running point expiration. Level recalculation will continue.");
        }
    }

    private async Task RunLevelRecalculationAsync(ISender sender, CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new RecalculateLevelsCommand(OperatorId), ct);
            if (result.IsFailure)
            {
                _logger.LogError("Level recalculation failed: {Error}", result.Error);
                return;
            }

            var value = result.Value;
            _logger.LogInformation(
                "Level recalculation result: cardsProcessed={CardsProcessed}, cardsChanged={CardsChanged}, cardsUpgraded={CardsUpgraded}, cardsDowngraded={CardsDowngraded}, walletsNotified={WalletsNotified}.",
                value.CardsProcessed,
                value.CardsChanged,
                value.CardsUpgraded,
                value.CardsDowngraded,
                value.WalletsNotified);

            LogWarnings("Level recalculation", value.Warnings);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error running level recalculation.");
        }
    }

    private void LogWarnings(string operation, IReadOnlyList<string> warnings)
    {
        if (warnings.Count == 0)
            return;

        _logger.LogWarning(
            "{Operation} completed with {WarningCount} warnings: {Warnings}",
            operation,
            warnings.Count,
            string.Join(" | ", warnings));
    }

    private bool TryResolveInterval(
        LoyaltyMaintenanceOptions options,
        out TimeSpan interval)
    {
        interval = default;

        if (options.IntervalHours <= 0)
        {
            _logger.LogError(
                "Invalid LoyaltyMaintenance:IntervalHours value '{IntervalHours}'. Expected a positive integer.",
                options.IntervalHours);
            return false;
        }

        interval = TimeSpan.FromHours(options.IntervalHours);
        return true;
    }

    private static async Task DelaySafelyAsync(TimeSpan delay, CancellationToken ct)
    {
        if (delay <= TimeSpan.Zero)
            return;

        await Task.Delay(delay, ct);
    }
}
