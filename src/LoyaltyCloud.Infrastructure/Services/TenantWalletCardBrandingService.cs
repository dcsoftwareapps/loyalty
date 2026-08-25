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
    private readonly IAppleWalletPassRefreshService _passRefresh;
    private readonly ILogger<TenantWalletCardBrandingService> _logger;

    public TenantWalletCardBrandingService(
        AppDbContext db,
        ITenantContext tenantContext,
        ITenantBrandingReadService brandingRead,
        IAppleWalletPassRefreshService passRefresh,
        ILogger<TenantWalletCardBrandingService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _brandingRead = brandingRead;
        _passRefresh = passRefresh;
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
        _logger.LogInformation(
            "Tenant wallet branding refresh requested. TenantId={TenantId}.",
            tenantId);

        var result = await _passRefresh.RefreshTenantInstalledPassesAsync(
            tenantId,
            PassUpdateReason.BrandingUpdated,
            ct);

        _logger.LogInformation(
            "Tenant wallet branding refresh completed. TenantId={TenantId}, CardsTouched={CardsTouched}, DevicesFound={DevicesFound}, PushesAttempted={Attempted}, PushesAccepted={Accepted}, PushesFailed={Failed}, Unsupported={Unsupported}, FailureType={FailureType}.",
            tenantId,
            result.CardsTouched,
            result.DevicesFound,
            result.PushesAttempted,
            result.PushesAccepted,
            result.PushesFailed,
            result.Unsupported,
            result.WorstFailureType);
    }
}
