using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Customers.Queries.GetCustomerDetail;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.ValueObjects;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class CustomerDetailReadService : ICustomerDetailReadService
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dt;
    private readonly ILevelCalculationService _levels;

    public CustomerDetailReadService(AppDbContext db, IDateTimeProvider dt, ILevelCalculationService levels)
    {
        _db = db;
        _dt = dt;
        _levels = levels;
    }

    public async Task<CustomerDetailDto?> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
    {
        var baseInfo = await (
            from customer in _db.Customers.AsNoTracking()
            where customer.Id == customerId
            join card in _db.LoyaltyCards.AsNoTracking() on customer.Id equals card.CustomerId into cards
            from card in cards.DefaultIfEmpty()
            select new
            {
                customer.Id,
                customer.FullName,
                customer.Email,
                customer.Phone,
                customer.DateOfBirth,
                customer.CreatedAt,
                CustomerIsActive = customer.IsActive,
                CardId = card == null ? (Guid?)null : card.Id,
                SerialNumber = card == null ? null : card.SerialNumber,
                CardIsActive = card != null && card.IsActive,
                CurrentPoints = card == null ? 0 : card.CurrentPoints,
                LifetimePoints = card == null ? 0 : card.LifetimePoints,
                Level = card == null ? "Sin Wallet" : card.Level,
                LevelAchievedAt = card == null ? (DateTime?)null : card.LevelAchievedAt,
                LastActivityAt = card == null ? (DateTime?)null : card.LastActivityAt
            })
            .FirstOrDefaultAsync(ct);

        if (baseInfo is null)
            return null;

        var hasCard = baseInfo.CardId.HasValue;
        var cardId = baseInfo.CardId ?? Guid.Empty;
        var serial = baseInfo.SerialNumber;

        var pointsRedeemed = hasCard
            ? await _db.PointTransactions
                .AsNoTracking()
                .Where(t => t.LoyaltyCardId == cardId && t.Type == TransactionType.Redemption && t.Points < 0)
                .SumAsync(t => (int?)-t.Points, ct) ?? 0
            : 0;

        var pendingRedemptions = hasCard
            ? await _db.Redemptions
                .AsNoTracking()
                .CountAsync(r => r.LoyaltyCardId == cardId && r.Status == RedemptionStatus.Pending, ct)
            : 0;

        var cancelledRedemptions = hasCard
            ? await _db.Redemptions
                .AsNoTracking()
                .CountAsync(r => r.LoyaltyCardId == cardId && r.Status == RedemptionStatus.Cancelled, ct)
            : 0;

        var confirmedRedemptions = hasCard
            ? await _db.Redemptions
                .AsNoTracking()
                .CountAsync(r => r.LoyaltyCardId == cardId && r.Status == RedemptionStatus.Confirmed, ct)
            : 0;
        var totalRedemptions = pendingRedemptions + cancelledRedemptions + confirmedRedemptions;

        var deviceCount = !string.IsNullOrWhiteSpace(serial)
            ? await _db.DeviceRegistrations
                .AsNoTracking()
                .CountAsync(d => d.SerialNumber == serial, ct)
            : 0;

        var now = _dt.UtcNow;
        var rollingLevel = baseInfo.Level;
        var rollingPoints = 0;
        var rollingProgress = new RollingProgressDto(0, 0, 0, 0, baseInfo.Level, "No disponible");
        if (hasCard)
        {
            var windowStart = now.AddMonths(-12);
            rollingPoints = await _db.PointTransactions
                .AsNoTracking()
                .Where(t => t.LoyaltyCardId == cardId
                         && t.CreatedAt >= windowStart
                         && t.Points > 0
                         && LevelProgressTransactionTypes.All.Contains(t.Type))
                .SumAsync(t => (int?)t.Points, ct) ?? 0;

            var snapshot = ProgramConfigSnapshot.FromEntries(await _db.ProgramConfigs.AsNoTracking().ToListAsync(ct));
            var memberLevel = _levels.CalculateLevel(rollingPoints, snapshot);
            rollingLevel = memberLevel.Name;
            var nextLevel = memberLevel.Name switch
            {
                var n when string.Equals(n, LoyaltyConstants.Levels.Mist, StringComparison.OrdinalIgnoreCase) => LoyaltyConstants.Levels.Glow,
                var n when string.Equals(n, LoyaltyConstants.Levels.Glow, StringComparison.OrdinalIgnoreCase) => LoyaltyConstants.Levels.Radiance,
                _ => "Maximo"
            };
            rollingProgress = new RollingProgressDto(
                RollingPoints: rollingPoints,
                GlowThreshold: snapshot.LevelGlowMin,
                RadianceThreshold: snapshot.LevelRadianceMin,
                PointsToNextLevel: memberLevel.PointsToNextLevel(rollingPoints),
                CurrentLevel: memberLevel.Name,
                NextLevel: nextLevel);
        }

        var lotRows = hasCard
            ? await (
                from lot in _db.PointLots.AsNoTracking()
                where lot.LoyaltyCardId == cardId
                orderby lot.ExpiresAt, lot.EarnedAt, lot.Id
                select new
                {
                    lot.Id,
                    lot.EarnedAt,
                    lot.ExpiresAt,
                    lot.OriginalAmount,
                    lot.RemainingAmount,
                    HasExpirationConsumption = (
                        from consumption in _db.PointLotConsumptions.AsNoTracking()
                        join tx in _db.PointTransactions.AsNoTracking()
                            on consumption.ConsumingPointTransactionId equals tx.Id
                        where consumption.PointLotId == lot.Id
                           && consumption.ReversedAt == null
                           && tx.Type == TransactionType.Expired
                        select consumption.Id).Any()
                })
                .ToListAsync(ct)
            : [];

        var upcomingDate = lotRows
            .Where(l => l.RemainingAmount > 0 && l.ExpiresAt > now)
            .Select(l => (DateTime?)l.ExpiresAt)
            .FirstOrDefault();

        var upcomingExpiration = upcomingDate.HasValue
            ? new UpcomingExpirationDto(
                upcomingDate.Value,
                lotRows
                    .Where(l => l.RemainingAmount > 0 && l.ExpiresAt == upcomingDate.Value)
                    .Sum(l => l.RemainingAmount))
            : null;

        var lotSummaries = lotRows
            .Select(l => new LotSummaryDto(
                LotId: l.Id,
                EarnedAt: l.EarnedAt,
                ExpiresAt: l.ExpiresAt,
                OriginalAmount: l.OriginalAmount,
                RemainingAmount: l.RemainingAmount,
                Status: GetLotStatus(l.OriginalAmount, l.RemainingAmount, l.ExpiresAt, l.HasExpirationConsumption, now)))
            .ToList();

        var shouldLoadConsumptions = lotRows.Any(l => l.RemainingAmount < l.OriginalAmount);
        var consumptionRows = hasCard && shouldLoadConsumptions
            ? await (
                from consumption in _db.PointLotConsumptions.AsNoTracking()
                join lot in _db.PointLots.AsNoTracking() on consumption.PointLotId equals lot.Id
                join tx in _db.PointTransactions.AsNoTracking()
                    on consumption.ConsumingPointTransactionId equals tx.Id
                join redemption in _db.Redemptions.AsNoTracking()
                    on consumption.RedemptionId equals redemption.Id into redemptions
                from redemption in redemptions.DefaultIfEmpty()
                join reward in _db.RewardCatalogItems.AsNoTracking()
                    on redemption.RewardCatalogItemId equals reward.Id into rewards
                from reward in rewards.DefaultIfEmpty()
                where lot.LoyaltyCardId == cardId
                orderby lot.ExpiresAt, lot.EarnedAt, lot.Id, consumption.CreatedAt
                select new
                {
                    consumption.Id,
                    consumption.PointLotId,
                    lot.EarnedAt,
                    lot.ExpiresAt,
                    lot.OriginalAmount,
                    consumption.Amount,
                    consumption.CreatedAt,
                    IsReversed = consumption.ReversedAt != null,
                    Reason = tx.Type.ToString(),
                    RewardName = reward == null ? null : reward.Name
                })
                .ToListAsync(ct)
            : [];

        var runningByLot = lotRows.ToDictionary(l => l.Id, l => l.OriginalAmount);
        var consumptions = new List<ConsumptionDto>();
        foreach (var row in consumptionRows)
        {
            if (!runningByLot.TryGetValue(row.PointLotId, out var running))
                running = row.OriginalAmount;

            var remainingAfter = row.IsReversed ? running : Math.Max(0, running - row.Amount);
            if (!row.IsReversed)
                runningByLot[row.PointLotId] = remainingAfter;

            consumptions.Add(new ConsumptionDto(
                ConsumptionId: row.Id,
                LotId: row.PointLotId,
                LotEarnedAt: row.EarnedAt,
                LotExpiresAt: row.ExpiresAt,
                AmountConsumed: row.Amount,
                RemainingAfterConsumption: remainingAfter,
                Reason: row.Reason,
                RewardName: row.RewardName,
                ConsumedAt: row.CreatedAt,
                IsReversed: row.IsReversed));
        }

        var pointTransactions = hasCard
            ? await (
                from transaction in _db.PointTransactions.AsNoTracking()
                join campaign in _db.PointCampaigns.AsNoTracking()
                    on transaction.CampaignId equals campaign.Id into campaigns
                from campaign in campaigns.DefaultIfEmpty()
                where transaction.LoyaltyCardId == cardId
                orderby transaction.CreatedAt descending
                select new
                {
                    transaction.CreatedAt,
                    transaction.Type,
                    transaction.Description,
                    transaction.Points,
                    transaction.CampaignId,
                    CampaignName = campaign == null ? null : campaign.Name,
                    transaction.AppliedMultiplier
                })
                .Take(50)
                .ToListAsync(ct)
            : [];

        var pointHistory = new List<CustomerPointHistoryItemDto>();
        if (hasCard && pointTransactions.Count > 0)
        {
            var balance = baseInfo.CurrentPoints;
            pointHistory = pointTransactions
                .Select(t =>
                {
                    var item = new CustomerPointHistoryItemDto(
                        t.CreatedAt,
                        t.Type,
                        t.Description,
                        t.Points,
                        balance,
                        t.CampaignId,
                        t.CampaignName,
                        t.AppliedMultiplier);
                    balance -= t.Points;
                    return item;
                })
                .ToList();
        }

        var redemptionHistory = hasCard
            ? await (
                from redemption in _db.Redemptions.AsNoTracking()
                join reward in _db.RewardCatalogItems.AsNoTracking() on redemption.RewardCatalogItemId equals reward.Id
                where redemption.LoyaltyCardId == cardId
                orderby redemption.RedeemedAt descending
                select new CustomerRedemptionHistoryItemDto(
                    redemption.RedeemedAt,
                    reward.Name,
                    redemption.Status,
                    redemption.PointsSpent))
                .Take(50)
                .ToListAsync(ct)
            : new List<CustomerRedemptionHistoryItemDto>();

        var notificationHistory = await _db.LoyaltyNotifications
            .AsNoTracking()
            .Where(n => n.CustomerId == baseInfo.Id)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new CustomerNotificationHistoryItemDto(
                n.CreatedAt,
                n.Type,
                n.Title,
                n.Message,
                n.CustomNotificationCampaignId,
                n.ShortMessage,
                n.LongMessage,
                n.Status,
                n.Deliveries.Sum(d => (int?)d.PushesAttempted) ?? 0,
                n.Deliveries.Sum(d => (int?)d.PushesAccepted) ?? 0,
                n.Deliveries.Sum(d => (int?)d.PushesFailed) ?? 0))
            .Take(10)
            .ToListAsync(ct);

        return new CustomerDetailDto(
            Summary: new CustomerSummaryDto(
                CustomerId: baseInfo.Id,
                FullName: baseInfo.FullName,
                Email: baseInfo.Email,
                Phone: baseInfo.Phone,
                DateOfBirth: baseInfo.DateOfBirth,
                BirthdayCaptured: baseInfo.DateOfBirth != Customer.BirthdayNotCaptured,
                CreatedAt: baseInfo.CreatedAt,
                IsActive: baseInfo.CustomerIsActive && (!hasCard || baseInfo.CardIsActive),
                Level: rollingLevel,
                LevelAchievedAt: baseInfo.LevelAchievedAt,
                WalletIssued: hasCard),
            Wallet: new CustomerWalletDto(
                WalletIssued: hasCard,
                SerialNumber: serial,
                CurrentPoints: hasCard ? baseInfo.CurrentPoints : null,
                IssuedAt: hasCard ? baseInfo.CreatedAt : null,
                LastActivityAt: baseInfo.LastActivityAt,
                DeviceRegistrationCount: deviceCount,
                LastPushSentAt: null),
            Statistics: new CustomerStatisticsDto(
                CurrentPoints: baseInfo.CurrentPoints,
                RollingPoints: rollingPoints,
                LifetimePoints: baseInfo.LifetimePoints,
                PointsRedeemed: pointsRedeemed,
                TotalRedemptions: totalRedemptions,
                PendingRedemptions: pendingRedemptions,
                CancelledRedemptions: cancelledRedemptions,
                ConfirmedRedemptions: confirmedRedemptions),
            LoyaltyAudit: new CustomerLoyaltyAuditDto(
                UpcomingExpiration: upcomingExpiration,
                RollingProgress: rollingProgress,
                Lots: lotSummaries.AsReadOnly(),
                Consumptions: consumptions.AsReadOnly()),
            NotificationHistory: notificationHistory.AsReadOnly(),
            PointHistory: pointHistory.AsReadOnly(),
            RedemptionHistory: redemptionHistory.AsReadOnly());
    }

    private static string GetLotStatus(
        int originalAmount,
        int remainingAmount,
        DateTime expiresAt,
        bool hasExpirationConsumption,
        DateTime now)
    {
        if (hasExpirationConsumption || (remainingAmount > 0 && expiresAt <= now))
            return "Expirado";
        if (remainingAmount == 0)
            return "Consumido";
        if (remainingAmount < originalAmount)
            return "Parcialmente consumido";

        return "Activo";
    }
}
