using FluentValidation;

namespace LoyaltyCloud.Application.Rewards.Queries.ListRewards;

internal sealed class ListRewardsValidator : AbstractValidator<ListRewardsQuery>
{
    public ListRewardsValidator()
    {
        RuleFor(x => x.MinLevel)
            .MaximumLength(20)
            .WithMessage("MinLevel no puede exceder 20 caracteres.");
    }
}
