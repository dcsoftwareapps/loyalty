using FluentValidation;

namespace KBeauty.Loyalty.Application.Points.Commands.AddPoints;

public sealed class AddPointsValidator : AbstractValidator<AddPointsCommand>
{
    public AddPointsValidator()
    {
        RuleFor(x => x.SerialNumber)
            .NotEmpty().WithMessage("El serial es requerido.")
            .MaximumLength(20);

        RuleFor(x => x.PurchaseAmount)
            .GreaterThan(0).WithMessage("El monto de compra debe ser mayor a 0.")
            .LessThan(1_000_000).WithMessage("El monto excede el máximo permitido.");

        RuleFor(x => x.OperatorId)
            .NotEmpty().WithMessage("Id de operador requerido para auditoría.");
    }
}
