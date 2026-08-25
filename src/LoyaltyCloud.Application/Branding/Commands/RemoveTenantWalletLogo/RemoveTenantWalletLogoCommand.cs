using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Branding.Commands.RemoveTenantWalletLogo;

public sealed record RemoveTenantWalletLogoCommand : IRequest<Result>;
