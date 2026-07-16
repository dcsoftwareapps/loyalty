using System.Diagnostics;
using System.Globalization;
using KBeauty.Loyalty.API.Configuration;
using KBeauty.Loyalty.Application.Levels.Commands.RecalculateLevels;
using KBeauty.Loyalty.Application.Notifications.Commands.CreateBirthdayBenefitStartedNotifications;
using KBeauty.Loyalty.Application.Notifications.Commands.CreateMonthlyProductStartedNotifications;
using KBeauty.Loyalty.Application.Notifications.Commands.CreatePointExpirationNotifications;
using KBeauty.Loyalty.Application.Points.Commands.ExpirePoints;
using MediatR;
using Microsoft.Extensions.Options;

namespace KBeauty.Loyalty.API.Services;

public sealed class LoyaltyMaintenanceBackgroundService : BackgroundService
{
    private const string OperatorId = "loyalty-maintenance";
    private static readonly TimeSpan InvalidConfigurationDelay = TimeSpan.FromHours(1);
    private static readonly IReadOnlyDictionary<string, string> IanaToWindowsTimeZones =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["America/Tijuana"] = "Pacific Standard Time (Mexico)"
        };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<LoyaltyMaintenanceOptions> _options;
    private readonly ILogger<LoyaltyMaintenanceBackgroundService> _logger;

    public LoyaltyMaintenanceBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<LoyaltyMaintenanceOptions> options,
        ILogger<LoyaltyMaintenanceBackgroundService> logger)
    {
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

            if (!TryResolveSchedule(options, out var runAtLocalTime, out var timeZone))
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

            var nextRunUtc = CalculateNextRunUtc(DateTimeOffset.UtcNow, runAtLocalTime, timeZone);
            var nextRunLocal = TimeZoneInfo.ConvertTime(nextRunUtc, timeZone);
            var delay = nextRunUtc - DateTimeOffset.UtcNow;
            if (delay < TimeSpan.Zero)
                delay = TimeSpan.Zero;

            _logger.LogInformation(
                "Next loyalty maintenance scheduled at {NextRunLocal} ({TimeZoneId}) / {NextRunUtc} UTC.",
                nextRunLocal,
                timeZone.Id,
                nextRunUtc);

            await DelaySafelyAsync(delay, stoppingToken);
            if (stoppingToken.IsCancellationRequested)
                break;

            await RunMaintenanceAsync(options, stoppingToken);
        }
    }

    private async Task RunMaintenanceAsync(LoyaltyMaintenanceOptions options, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting loyalty maintenance.");

        using var scope = _scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await RunExpirationAsync(sender, ct);
        await RunLevelRecalculationAsync(sender, ct);
        await RunPointExpirationNotificationsAsync(sender, options, ct);
        await RunMonthlyProductNotificationsAsync(sender, options, ct);
        await RunBirthdayBenefitNotificationsAsync(sender, options, ct);

        stopwatch.Stop();
        _logger.LogInformation(
            "Finished loyalty maintenance in {ElapsedMilliseconds} ms.",
            stopwatch.ElapsedMilliseconds);
    }

    private async Task RunPointExpirationNotificationsAsync(
        ISender sender,
        LoyaltyMaintenanceOptions options,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(
                new CreatePointExpirationNotificationsCommand(OperatorId, DaysAhead: 15, TimeZoneId: options.TimeZoneId),
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
        LoyaltyMaintenanceOptions options,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(
                new CreateMonthlyProductStartedNotificationsCommand(OperatorId, options.TimeZoneId),
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
        LoyaltyMaintenanceOptions options,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(
                new CreateBirthdayBenefitStartedNotificationsCommand(OperatorId, options.TimeZoneId),
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

    private bool TryResolveSchedule(
        LoyaltyMaintenanceOptions options,
        out TimeOnly runAtLocalTime,
        out TimeZoneInfo timeZone)
    {
        runAtLocalTime = default;
        timeZone = TimeZoneInfo.Utc;

        if (!TimeOnly.TryParseExact(
                options.RunAtLocalTime,
                "HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out runAtLocalTime))
        {
            _logger.LogError(
                "Invalid LoyaltyMaintenance:RunAtLocalTime value '{RunAtLocalTime}'. Expected format HH:mm.",
                options.RunAtLocalTime);
            return false;
        }

        if (TryFindTimeZone(options.TimeZoneId, out timeZone))
            return true;

        _logger.LogError(
            "Invalid LoyaltyMaintenance:TimeZoneId value '{TimeZoneId}'. Maintenance loop will retry later.",
            options.TimeZoneId);
        return false;
    }

    private static bool TryFindTimeZone(string timeZoneId, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        if (IanaToWindowsTimeZones.TryGetValue(timeZoneId, out var windowsId))
        {
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                return true;
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        timeZone = TimeZoneInfo.Utc;
        return false;
    }

    private static DateTimeOffset CalculateNextRunUtc(
        DateTimeOffset nowUtc,
        TimeOnly runAtLocalTime,
        TimeZoneInfo timeZone)
    {
        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, timeZone);
        var candidateLocal = nowLocal.Date + runAtLocalTime.ToTimeSpan();
        if (candidateLocal <= nowLocal.DateTime)
            candidateLocal = candidateLocal.AddDays(1);

        while (timeZone.IsInvalidTime(candidateLocal))
            candidateLocal = candidateLocal.AddMinutes(30);

        var candidateUtc = TimeZoneInfo.ConvertTimeToUtc(candidateLocal, timeZone);
        return new DateTimeOffset(candidateUtc, TimeSpan.Zero);
    }

    private static async Task DelaySafelyAsync(TimeSpan delay, CancellationToken ct)
    {
        if (delay <= TimeSpan.Zero)
            return;

        await Task.Delay(delay, ct);
    }
}
