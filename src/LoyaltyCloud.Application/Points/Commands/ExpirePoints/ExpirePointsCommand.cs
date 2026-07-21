using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Points.Commands.ExpirePoints;

public sealed record ExpirePointsCommand(string OperatorId) : IRequest<Result<ExpirePointsResponse>>;
