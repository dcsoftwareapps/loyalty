using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Admin.Queries.GetAdminDashboard;

/// <inheritdoc cref="GetAdminDashboardQuery"/>
public sealed class GetAdminDashboardHandler
    : IRequestHandler<GetAdminDashboardQuery, Result<DashboardDto>>
{
    private readonly IDashboardReadService _read;

    public GetAdminDashboardHandler(IDashboardReadService read)
    {
        _read = read;
    }

    /// <inheritdoc />
    public async Task<Result<DashboardDto>> Handle(GetAdminDashboardQuery _, CancellationToken ct)
    {
        var dto = await _read.GetDashboardAsync(ct);
        return Result.Ok(dto);
    }
}
