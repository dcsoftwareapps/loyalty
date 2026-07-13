using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Points.Commands.ExpirePoints;

public sealed record ExpirePointsCommand(string OperatorId) : IRequest<Result<ExpirePointsResponse>>;
