using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Extensions;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Repositories;
using MediatR;

namespace LoyaltyCloud.Application.Customers.Queries.GetCustomerBySerial;

/// <inheritdoc cref="GetCustomerBySerialQuery"/>
public sealed class GetCustomerBySerialHandler
    : IRequestHandler<GetCustomerBySerialQuery, Result<CustomerDetailDto>>
{
    private readonly ILoyaltyCardRepository _cards;
    private readonly ICustomerRepository _customers;
    private readonly IPointTransactionRepository _transactions;
    private readonly ITenantLoyaltyLevelReadService _tenantLevels;
    private readonly ILevelProgressService _levelProgress;
    private readonly ITenantContext _tenantContext;
    private readonly IDateTimeProvider _dt;

    public GetCustomerBySerialHandler(
        ILoyaltyCardRepository cards,
        ICustomerRepository customers,
        IPointTransactionRepository transactions,
        ITenantLoyaltyLevelReadService tenantLevels,
        ILevelProgressService levelProgress,
        ITenantContext tenantContext,
        IDateTimeProvider dt)
    {
        _cards = cards;
        _customers = customers;
        _transactions = transactions;
        _tenantLevels = tenantLevels;
        _levelProgress = levelProgress;
        _tenantContext = tenantContext;
        _dt = dt;
    }

    /// <inheritdoc />
    public async Task<Result<CustomerDetailDto>> Handle(GetCustomerBySerialQuery query, CancellationToken ct)
    {
        var card = await _cards.GetBySerialNumberAsync(query.SerialNumber, ct);
        if (card is null)
            return Result.Fail<CustomerDetailDto>($"No se encontró tarjeta con serial '{query.SerialNumber}'.");

        if (card.TenantId != _tenantContext.RequireTenantId())
            return Result.Fail<CustomerDetailDto>("La tarjeta no pertenece al tenant actual.");

        var customer = await _customers.GetByIdAsync(card.CustomerId, ct);
        if (customer is null)
            return Result.Fail<CustomerDetailDto>("La tarjeta existe pero su cliente no — datos inconsistentes.");
        if (!customer.IsActive || !card.IsActive)
            return Result.Fail<CustomerDetailDto>($"No se encontró tarjeta con serial '{query.SerialNumber}'.");

        var tenantLevels = await _tenantLevels.GetActiveLevelsAsync(ct);
        var rollingPoints = await _transactions.GetEligibleLevelPointsAsync(card.Id, _dt.UtcNow.AddMonths(-12), ct);
        var progress = _levelProgress.Calculate(rollingPoints, tenantLevels);

        return Result.Ok(new CustomerDetailDto(
            CustomerId: customer.Id,
            FullName: customer.FullName,
            Email: customer.Email,
            Phone: customer.Phone,
            DateOfBirth: customer.DateOfBirth,
            IsBirthMonth: customer.DateOfBirth.IsBirthMonth(_dt.UtcNow),
            SerialNumber: card.SerialNumber,
            CurrentPoints: card.CurrentPoints,
            LifetimePoints: card.LifetimePoints,
            Level: progress.CurrentLevel.Name,
            PointsToNextLevel: progress.PointsToNextLevel,
            PointsEarnedThisYear: rollingPoints,
            LevelAchievedAt: card.LevelAchievedAt,
            LastActivityAt: card.LastActivityAt,
            CreatedAt: customer.CreatedAt,
            IsActive: customer.IsActive && card.IsActive));
    }
}
