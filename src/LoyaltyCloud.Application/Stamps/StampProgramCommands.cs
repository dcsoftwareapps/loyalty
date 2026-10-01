using FluentValidation;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Enums;
using MediatR;

namespace LoyaltyCloud.Application.Stamps;

public sealed record StampRewardInput(int RequiredStamps, string Name, string? Description = null);
public sealed record StampProgramDto(Guid Id, int Version, int MaxStamps, int WelcomeStamps,
    StampReversalPolicy ReversalPolicy, IReadOnlyList<StampRewardInput> Rewards);
public sealed record SetStampProgramCommand(int MaxStamps, int WelcomeStamps,
    StampReversalPolicy ReversalPolicy, IReadOnlyList<StampRewardInput> Rewards) : IRequest<Result<StampProgramDto>>;
public sealed record GetActiveStampProgramQuery : IRequest<StampProgramDto?>;

public sealed class SetStampProgramValidator : AbstractValidator<SetStampProgramCommand>
{
    public SetStampProgramValidator()
    {
        RuleFor(x => x.MaxStamps).InclusiveBetween(2, 12);
        RuleFor(x => x.WelcomeStamps).GreaterThanOrEqualTo(0).LessThan(x => x.MaxStamps);
        RuleFor(x => x.ReversalPolicy).IsInEnum();
        RuleFor(x => x.Rewards).NotNull().Must((x, rewards) => rewards is not null
            && rewards.Any(r => r.RequiredStamps == x.MaxStamps)
            && rewards.Select(r => r.RequiredStamps).Distinct().Count() == rewards.Count)
            .WithMessage("Se requiere recompensa final y metas distintas.");
        RuleForEach(x => x.Rewards).ChildRules(reward =>
        {
            reward.RuleFor(x => x.RequiredStamps).GreaterThan(0);
            reward.RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
            reward.RuleFor(x => x.Description).MaximumLength(1000);
        });
        RuleFor(x => x.Rewards).Must((x, rewards) => rewards is not null && rewards.All(r => r.RequiredStamps <= x.MaxStamps));
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
