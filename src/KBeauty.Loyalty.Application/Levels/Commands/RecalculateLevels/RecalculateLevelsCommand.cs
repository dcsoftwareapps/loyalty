using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Levels.Commands.RecalculateLevels;

public sealed record RecalculateLevelsCommand(string OperatorId)
    : IRequest<Result<RecalculateLevelsResponse>>;
