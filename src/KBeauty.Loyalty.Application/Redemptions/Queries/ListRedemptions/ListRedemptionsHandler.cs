using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Redemptions.Queries.ListRedemptions;

public sealed class ListRedemptionsHandler
    : IRequestHandler<ListRedemptionsQuery, Result<IReadOnlyList<RedemptionHistoryItemDto>>>
{
    private readonly IRedemptionHistoryReadService _read;

    public ListRedemptionsHandler(IRedemptionHistoryReadService read) => _read = read;

    public async Task<Result<IReadOnlyList<RedemptionHistoryItemDto>>> Handle(
        ListRedemptionsQuery query,
        CancellationToken ct)
    {
        var items = await _read.ListAsync(query.Status, query.Search, ct);
        return Result.Ok(items);
    }
}
