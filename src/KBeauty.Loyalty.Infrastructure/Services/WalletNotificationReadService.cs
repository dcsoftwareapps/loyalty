using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KBeauty.Loyalty.Infrastructure.Services;

internal sealed class WalletNotificationReadService : IWalletNotificationReadService
{
    private readonly AppDbContext _db;

    public WalletNotificationReadService(AppDbContext db) => _db = db;

    public async Task<WalletNotificationMessage?> GetActiveMessageAsync(Guid loyaltyCardId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var row = await _db.LoyaltyNotifications
            .AsNoTracking()
            .Where(n => n.LoyaltyCardId == loyaltyCardId
                     && (n.Status == NotificationStatus.Delivered ||
                         n.Status == NotificationStatus.PartiallyDelivered)
                     && n.DisplayUntilUtc.HasValue
                     && n.DisplayUntilUtc > now)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new { n.Id, n.Type, n.Title, n.Message, n.MetadataJson })
            .FirstOrDefaultAsync(ct);

        return row is null
            ? null
            : new WalletNotificationMessage(row.Id, row.Type, row.Title, row.Message, row.MetadataJson);
    }
}
