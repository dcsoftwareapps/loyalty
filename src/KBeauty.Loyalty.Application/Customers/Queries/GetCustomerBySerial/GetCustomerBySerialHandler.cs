using KBeauty.Loyalty.Common.Extensions;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Repositories;
using KBeauty.Loyalty.Domain.ValueObjects;
using MediatR;

namespace KBeauty.Loyalty.Application.Customers.Queries.GetCustomerBySerial;

/// <inheritdoc cref="GetCustomerBySerialQuery"/>
public sealed class GetCustomerBySerialHandler
    : IRequestHandler<GetCustomerBySerialQuery, Result<CustomerDetailDto>>
{
    private readonly ILoyaltyCardRepository _cards;
    private readonly ICustomerRepository _customers;
    private readonly IProgramConfigRepository _config;
    private readonly IDateTimeProvider _dt;

    public GetCustomerBySerialHandler(
        ILoyaltyCardRepository cards,
        ICustomerRepository customers,
        IProgramConfigRepository config,
        IDateTimeProvider dt)
    {
        _cards = cards;
        _customers = customers;
        _config = config;
        _dt = dt;
    }

    /// <inheritdoc />
    public async Task<Result<CustomerDetailDto>> Handle(GetCustomerBySerialQuery query, CancellationToken ct)
    {
        var card = await _cards.GetBySerialNumberAsync(query.SerialNumber, ct);
        if (card is null)
            return Result.Fail<CustomerDetailDto>($"No se encontró tarjeta con serial '{query.SerialNumber}'.");

        var customer = await _customers.GetByIdAsync(card.CustomerId, ct);
        if (customer is null)
            return Result.Fail<CustomerDetailDto>("La tarjeta existe pero su clienta no — datos inconsistentes.");

        var snapshot = ProgramConfigSnapshot.FromEntries(await _config.GetAllAsync(ct));
        var memberLevel = MemberLevel.FromPoints(card.CurrentPoints, snapshot);

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
            Level: card.Level,
            PointsToNextLevel: memberLevel.PointsToNextLevel(card.CurrentPoints),
            PointsEarnedThisYear: card.PointsEarnedThisYear,
            LevelAchievedAt: card.LevelAchievedAt,
            LastActivityAt: card.LastActivityAt,
            CreatedAt: customer.CreatedAt,
            IsActive: customer.IsActive && card.IsActive));
    }
}
