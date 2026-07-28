using LoyaltyCloud.Application.Common.Wallet;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Wallets.Commands.CreateGoogleWalletSaveLink;

public sealed record CreateGoogleWalletSaveLinkCommand(string SerialNumber)
    : IRequest<Result<GoogleWalletSaveLinkResponse>>;

