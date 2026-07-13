using FluentValidation;

namespace KBeauty.Loyalty.Application.Levels.Commands.RecalculateLevels;

public sealed class RecalculateLevelsValidator : AbstractValidator<RecalculateLevelsCommand>
{
    public RecalculateLevelsValidator()
    {
        RuleFor(x => x.OperatorId)
            .NotEmpty()
            .MaximumLength(100);
    }
}
