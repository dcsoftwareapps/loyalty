using LoyaltyCloud.Domain.Common;

namespace LoyaltyCloud.Domain.Entities;

public sealed class CashierRefreshToken : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public Guid TenantAdminUserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    private CashierRefreshToken() { }
    public CashierRefreshToken(Guid tenantId, Guid userId, string hash, DateTime now, DateTime expires) : base(Guid.NewGuid())
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty || hash.Length != 64 || expires <= now)
            throw new ArgumentException("Invalid Cashier refresh token metadata.");
        TenantId=tenantId; TenantAdminUserId=userId; TokenHash=hash; CreatedAtUtc=now; ExpiresAtUtc=expires;
    }
    public bool IsUsable(DateTime now) => RevokedAtUtc is null && ExpiresAtUtc > now;
    public void Revoke(DateTime now) => RevokedAtUtc ??= now;
}
