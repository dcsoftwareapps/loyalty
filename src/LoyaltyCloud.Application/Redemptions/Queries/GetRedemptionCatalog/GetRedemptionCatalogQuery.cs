using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Redemptions.Queries.GetRedemptionCatalog;

/// <summary>
/// Catálogo filtrado al nivel de una clienta específica. La clienta solo ve
/// lo que efectivamente puede canjear hoy con su nivel actual.
/// </summary>
public sealed record GetRedemptionCatalogQuery(string SerialNumber)
    : IRequest<Result<IReadOnlyList<RewardCatalogItemDto>>>;
