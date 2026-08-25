using LoyaltyCloud.Application.Common.Branding;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class TenantWalletCardBrandingService : ITenantWalletCardBrandingService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantBrandingReadService _brandingRead;
    private readonly IDateTimeProvider _dt;
    private readonly IApnService _apn;
    private readonly ILogger<TenantWalletCardBrandingService> _logger;

    public TenantWalletCardBrandingService(
        AppDbContext db,
        ITenantContext tenantContext,
        ITenantBrandingReadService brandingRead,
        IDateTimeProvider dt,
        IApnService apn,
        ILogger<TenantWalletCardBrandingService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _brandingRead = brandingRead;
        _dt = dt;
        _apn = apn;
        _logger = logger;
    }

    public async Task<Result<TenantBrandingInfo>> UpdateBackgroundColorAsync(
        string? walletBackgroundColor,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var normalizedColor = string.IsNullOrWhiteSpace(walletBackgroundColor)
            ? null
            : WalletColorContrast.NormalizeHexOrDefault(walletBackgroundColor);

        if (!string.IsNullOrWhiteSpace(walletBackgroundColor) && !WalletColorContrast.IsHexColor(walletBackgroundColor))
            return Result.Fail<TenantBrandingInfo>("El color de la tarjeta debe usar formato #RRGGBB.");

        var branding = await _db.TenantBrandings.SingleOrDefaultAsync(b => b.TenantId == tenantId, cancellationToken);
        if (branding is null)
            return Result.Fail<TenantBrandingInfo>("Branding del tenant no encontrado.");

        var changed = !string.Equals(branding.WalletBackgroundColor, normalizedColor, StringComparison.OrdinalIgnoreCase);
        branding.SetWalletBackgroundColor(normalizedColor);
        await _db.SaveChangesAsync(cancellationToken);

        if (changed)
            await RefreshInstalledApplePassesBestEffortAsync(tenantId, cancellationToken);

        return Result.Ok(await _brandingRead.GetCurrentAsync(cancellationToken));
    }

    public async Task RefreshInstalledApplePassesBestEffortAsync(Guid tenantId, CancellationToken ct)
    {
        var devices = await _db.DeviceRegistrations
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .Select(d => new { d.SerialNumber, d.PushToken })
            .ToListAsync(ct);

        if (devices.Count == 0)
            return;

        var serials = devices.Select(d => d.SerialNumber).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var cards = await _db.LoyaltyCards
            .Where(c => c.TenantId == tenantId && c.IsActive && serials.Contains(c.SerialNumber))
            .ToListAsync(ct);

        foreach (var card in cards)
            card.Touch(_dt);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Tenant wallet branding changed. TenantId={TenantId}, CardsTouched={CardsTouched}, DevicesFound={DevicesFound}.",
            tenantId,
            cards.Count,
            devices.Count);

        foreach (var device in devices)
        {
            try
            {
                await _apn.SendPassUpdateAsync(device.PushToken, PassUpdateReason.BrandingUpdated, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Best-effort APNs after wallet branding change failed. TenantId={TenantId}, Serial={Serial}.",
                    tenantId,
                    device.SerialNumber);
            }
        }
    }
}
