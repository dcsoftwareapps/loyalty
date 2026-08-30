using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Branding.Commands.UpdateWalletCardBranding;

public sealed record UpdateWalletCardBrandingCommand(
    string? WalletBackgroundColor,
    int? WalletLogoScalePercent) : IRequest<Result<TenantBrandingInfo>>;
