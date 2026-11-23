using FluentValidation;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Enums;
using MediatR;

namespace LoyaltyCloud.Application.Stamps;

public sealed record StampRewardInput(int RequiredStamps, string Name, string? Description = null, Guid? RewardIdentityId = null, string? ShortName = null);
public sealed record StampProgramDto(Guid Id, int Version, int MaxStamps, int WelcomeStamps,
    StampReversalPolicy ReversalPolicy, IReadOnlyList<StampRewardInput> Rewards);
public sealed record SetStampProgramCommand(int MaxStamps, int WelcomeStamps,
    StampReversalPolicy ReversalPolicy, IReadOnlyList<StampRewardInput> Rewards) : IRequest<Result<StampProgramDto>>;
public sealed record GetActiveStampProgramQuery : IRequest<StampProgramDto?>;

public sealed class SetStampProgramValidator : AbstractValidator<SetStampProgramCommand>
{
    public SetStampProgramValidator()
    {
        RuleFor(x => x.MaxStamps).InclusiveBetween(2, 12).WithMessage("La tarjeta debe tener entre 2 y 12 sellos.");
        RuleFor(x => x.WelcomeStamps).GreaterThanOrEqualTo(0).WithMessage("Los sellos de bienvenida no pueden ser negativos.")
            .LessThan(x => x.MaxStamps).WithMessage("Los sellos de bienvenida deben ser menos que el total de la tarjeta.");
        RuleFor(x => x.ReversalPolicy).IsInEnum().WithMessage("Selecciona una opción válida de reversión.");
        RuleFor(x => x.Rewards).Must(rewards => rewards is null || rewards.Where(r => r.RewardIdentityId.HasValue)
            .Select(r => r.RewardIdentityId).Distinct().Count() == rewards.Count(r => r.RewardIdentityId.HasValue))
            .WithMessage("Cada recompensa debe tener una identidad distinta.");
        RuleFor(x => x.Rewards).NotNull().Must((x, rewards) => rewards is not null
            && rewards.Any(r => r.RequiredStamps == x.MaxStamps)
            && rewards.Select(r => r.RequiredStamps).Distinct().Count() == rewards.Count)
            .WithMessage("Agrega una recompensa para el último sello de la tarjeta y usa un sello distinto para cada recompensa.");
        RuleForEach(x => x.Rewards).ChildRules(reward =>
        {
            reward.RuleFor(x => x.RequiredStamps).GreaterThan(0).WithMessage("El sello de la recompensa debe ser mayor que cero.");
            reward.RuleFor(x => x.Name).NotEmpty().WithMessage("Escribe el nombre de cada recompensa.")
                .MaximumLength(150).WithMessage("El nombre debe tener como máximo 150 caracteres.");
            reward.RuleFor(x => x.ShortName).Must(s => s is null || s.Trim().Length is >= 1 and <= 40)
                .WithMessage("El nombre corto debe tener entre 1 y 40 caracteres.");
            reward.RuleFor(x => x.Description).MaximumLength(1000).WithMessage("La descripción debe tener como máximo 1000 caracteres.");
        });
        RuleFor(x => x.Rewards).Must((x, rewards) => rewards is not null && rewards.All(r => r.RequiredStamps <= x.MaxStamps))
            .WithMessage("Las recompensas no pueden superar el total de sellos de la tarjeta.");
    }
}

public sealed class SetStampProgramHandler(IStampProgramService programs) : IRequestHandler<SetStampProgramCommand, Result<StampProgramDto>>
{
    public Task<Result<StampProgramDto>> Handle(SetStampProgramCommand request, CancellationToken ct) => programs.CreateVersionAsync(request, ct);
}

public sealed class GetActiveStampProgramHandler(IStampProgramService programs) : IRequestHandler<GetActiveStampProgramQuery, StampProgramDto?>
{
    public Task<StampProgramDto?> Handle(GetActiveStampProgramQuery request, CancellationToken ct) => programs.ReadActiveAsync(ct);
}
