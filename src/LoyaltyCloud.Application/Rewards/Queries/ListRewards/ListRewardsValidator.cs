using FluentValidation;

namespace LoyaltyCloud.Application.Rewards.Queries.ListRewards;

internal sealed class ListRewardsValidator : AbstractValidator<ListRewardsQuery>
{
    public ListRewardsValidator()
    {
        RuleFor(x => x.MinLevel)
            .Must(level => string.IsNullOrWhiteSpace(level) || RewardValidation.IsValidMemberLevel(level))
            .WithMessage("MinLevel debe ser Mist, Glow o Radiance.");
    }
}
