using FluentValidation;

namespace KBeauty.Loyalty.Application.Rewards.Commands.UpdateReward;

internal sealed class UpdateRewardValidator : AbstractValidator<UpdateRewardCommand>
{
    public UpdateRewardValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id es obligatorio.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name es obligatorio.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .Must(description => !string.IsNullOrWhiteSpace(description))
            .WithMessage("Description es obligatorio.");

        RuleFor(x => x.PointsCost)
            .GreaterThan(0)
            .WithMessage("PointsCost debe ser mayor a 0.");

        RuleFor(x => x.MinLevel)
            .Must(RewardValidation.IsValidMemberLevel)
            .WithMessage("MinLevel debe ser Mist, Glow o Radiance.");

        RuleFor(x => x)
            .Must(x => RewardValidation.HasValidDateRange(x.ValidFrom, x.ValidTo))
            .WithMessage("ValidTo no puede ser menor que ValidFrom.");

        RuleFor(x => x)
            .Must(x => RewardValidation.HasMonthlyProductDates(x.IsMonthlyProduct, x.ValidFrom, x.ValidTo))
            .WithMessage("El Producto del mes requiere fecha de inicio y fecha de fin.");
    }
}
