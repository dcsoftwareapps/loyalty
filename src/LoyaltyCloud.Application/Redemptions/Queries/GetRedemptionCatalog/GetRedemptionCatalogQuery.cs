using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Redemptions.Queries.GetRedemptionCatalog;

/// <summary>
/// Catálogo filtrado al nivel de un cliente específico. El cliente solo ve
/// lo que efectivamente puede canjear hoy con su nivel actual.
/// </summary>
public sealed record GetRedemptionCatalogQuery(string SerialNumber)
    : IRequest<Result<IReadOnlyList<RewardCatalogItemDto>>>;
