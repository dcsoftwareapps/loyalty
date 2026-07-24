using FluentValidation;
using LoyaltyCloud.Application.Common.Interfaces;

namespace LoyaltyCloud.Application.Campaigns.Commands.UpdatePointCampaign;

internal sealed class UpdatePointCampaignValidator : AbstractValidator<UpdatePointCampaignCommand>
{
    public UpdatePointCampaignValidator(ITenantLoyaltyLevelReadService levels)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Name es obligatorio.");
        RuleFor(x => x.Description).NotEmpty().Must(description => !string.IsNullOrWhiteSpace(description)).WithMessage("Description es obligatorio.");
        RuleFor(x => x.Multiplier).Must(PointCampaignValidation.IsValidMultiplier).WithMessage("Multiplier debe estar entre 2 y 5.");
        RuleFor(x => x.MinimumPurchaseAmount).GreaterThanOrEqualTo(0).When(x => x.MinimumPurchaseAmount.HasValue).WithMessage("MinimumPurchaseAmount no puede ser negativo.");
        RuleFor(x => x.LevelEligibility)
            .MustAsync((eligibility, ct) => PointCampaignValidation.IsValidLevelEligibilityAsync(eligibility, levels, ct))
            .WithMessage("LevelEligibility debe ser All o un nivel activo del tenant actual.");
        RuleFor(x => x).Must(x => PointCampaignValidation.HasValidDateRange(x.StartsAtUtc, x.EndsAtUtc)).WithMessage("EndsAtUtc no puede ser menor que StartsAtUtc.");
    }
}
