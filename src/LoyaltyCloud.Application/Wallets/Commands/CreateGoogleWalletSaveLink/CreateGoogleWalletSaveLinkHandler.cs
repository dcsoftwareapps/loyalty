using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Common.Wallet;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Wallets.Commands.CreateGoogleWalletSaveLink;

internal sealed class CreateGoogleWalletSaveLinkHandler
    : IRequestHandler<CreateGoogleWalletSaveLinkCommand, Result<GoogleWalletSaveLinkResponse>>
{
    private readonly IGoogleWalletService _googleWallet;

    public CreateGoogleWalletSaveLinkHandler(IGoogleWalletService googleWallet)
    {
        _googleWallet = googleWallet;
    }

    public Task<Result<GoogleWalletSaveLinkResponse>> Handle(
        CreateGoogleWalletSaveLinkCommand request,
        CancellationToken cancellationToken) =>
        _googleWallet.GetOrCreateSaveLinkAsync(request.SerialNumber, cancellationToken);
}

