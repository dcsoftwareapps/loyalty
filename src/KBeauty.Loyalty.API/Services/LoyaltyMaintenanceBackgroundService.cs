using System.Diagnostics;
using System.Globalization;
using KBeauty.Loyalty.API.Configuration;
using KBeauty.Loyalty.Application.Levels.Commands.RecalculateLevels;
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
                await RunMaintenanceAsync(stoppingToken);
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

            await RunMaintenanceAsync(stoppingToken);
        }
    }

    private async Task RunMaintenanceAsync(CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting loyalty maintenance.");

        using var scope = _scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await RunExpirationAsync(sender, ct);
        await RunLevelRecalculationAsync(sender, ct);

        stopwatch.Stop();
        _logger.LogInformation(
            "Finished loyalty maintenance in {ElapsedMilliseconds} ms.",
            stopwatch.ElapsedMilliseconds);
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
