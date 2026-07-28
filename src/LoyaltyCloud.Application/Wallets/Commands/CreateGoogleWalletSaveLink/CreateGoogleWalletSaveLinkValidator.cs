using FluentValidation;

namespace LoyaltyCloud.Application.Wallets.Commands.CreateGoogleWalletSaveLink;

internal sealed class CreateGoogleWalletSaveLinkValidator
    : AbstractValidator<CreateGoogleWalletSaveLinkCommand>
{
    public CreateGoogleWalletSaveLinkValidator()
    {
        RuleFor(x => x.SerialNumber)
            .NotEmpty()
            .MaximumLength(20);
    }
}

