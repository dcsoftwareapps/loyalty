using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Levels.Commands.RecalculateLevels;

public sealed record RecalculateLevelsCommand(string OperatorId)
    : IRequest<Result<RecalculateLevelsResponse>>;
