using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Admin.Queries.GetAdminDashboard;

/// <summary>Pantalla principal del panel admin.</summary>
public sealed record GetAdminDashboardQuery : IRequest<Result<DashboardDto>>;
