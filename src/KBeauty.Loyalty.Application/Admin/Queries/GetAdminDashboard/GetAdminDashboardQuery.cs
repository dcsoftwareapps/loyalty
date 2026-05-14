using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Admin.Queries.GetAdminDashboard;

/// <summary>Pantalla principal del panel admin.</summary>
public sealed record GetAdminDashboardQuery : IRequest<Result<DashboardDto>>;
