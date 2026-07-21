using FluentValidation;

namespace LoyaltyCloud.Application.Points.Commands.ExpirePoints;

internal sealed class ExpirePointsValidator : AbstractValidator<ExpirePointsCommand>
{
    public ExpirePointsValidator()
    {
        RuleFor(x => x.OperatorId)
            .NotEmpty()
            .WithMessage("OperatorId requerido.");
    }
}
