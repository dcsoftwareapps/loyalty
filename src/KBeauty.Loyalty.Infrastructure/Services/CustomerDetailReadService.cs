using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Application.Customers.Queries.GetCustomerDetail;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Domain.ValueObjects;
using KBeauty.Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KBeauty.Loyalty.Infrastructure.Services;

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
                customer.CreatedAt,
                CustomerIsActive = customer.IsActive,
                CardId = card == null ? (Guid?)null : card.Id,
                SerialNumber = card == null ? null : card.SerialNumber,
                CardIsActive = card != null && card.IsActive,
                CurrentPoints = card == null ? 0 : card.CurrentPoints,
                LifetimePoints = card == null ? 0 : card.LifetimePoints,
                Level = card == null ? "Sin Wallet" : card.Level,
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

        var rollingLevel = baseInfo.Level;
        if (hasCard)
        {
            var windowStart = _dt.UtcNow.AddMonths(-12);
            var rollingPoints = await _db.PointTransactions
                .AsNoTracking()
                .Where(t => t.LoyaltyCardId == cardId
                         && t.CreatedAt >= windowStart
                         && t.Points > 0
                         && LevelProgressTransactionTypes.All.Contains(t.Type))
                .SumAsync(t => (int?)t.Points, ct) ?? 0;

            var snapshot = ProgramConfigSnapshot.FromEntries(await _db.ProgramConfigs.AsNoTracking().ToListAsync(ct));
            rollingLevel = _levels.CalculateLevel(rollingPoints, snapshot).Name;
        }

        var pointHistory = hasCard
            ? await _db.PointTransactions
                .AsNoTracking()
                .Where(t => t.LoyaltyCardId == cardId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new CustomerPointHistoryItemDto(
                    t.CreatedAt,
                    t.Type,
                    t.Description,
                    t.Points,
                    null))
                .Take(50)
                .ToListAsync(ct)
            : new List<CustomerPointHistoryItemDto>();

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

        return new CustomerDetailDto(
            Summary: new CustomerSummaryDto(
                CustomerId: baseInfo.Id,
                FullName: baseInfo.FullName,
                Email: baseInfo.Email,
                Phone: baseInfo.Phone,
                CreatedAt: baseInfo.CreatedAt,
                IsActive: baseInfo.CustomerIsActive && (!hasCard || baseInfo.CardIsActive),
                Level: rollingLevel,
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
                LifetimePoints: baseInfo.LifetimePoints,
                PointsRedeemed: pointsRedeemed,
                TotalRedemptions: totalRedemptions,
                PendingRedemptions: pendingRedemptions,
                CancelledRedemptions: cancelledRedemptions,
                ConfirmedRedemptions: confirmedRedemptions),
            PointHistory: pointHistory.AsReadOnly(),
            RedemptionHistory: redemptionHistory.AsReadOnly());
    }
}
