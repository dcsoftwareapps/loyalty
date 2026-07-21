using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Repositories;
using MediatR;

namespace LoyaltyCloud.Application.Customers.Queries.GetCustomerTransactions;

/// <inheritdoc cref="GetCustomerTransactionsQuery"/>
public sealed class GetCustomerTransactionsHandler
    : IRequestHandler<GetCustomerTransactionsQuery, Result<PagedResult<TransactionDto>>>
{
    private readonly ILoyaltyCardRepository _cards;
    private readonly IPointTransactionRepository _transactions;

    public GetCustomerTransactionsHandler(
        ILoyaltyCardRepository cards,
        IPointTransactionRepository transactions)
    {
        _cards = cards;
        _transactions = transactions;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<TransactionDto>>> Handle(
        GetCustomerTransactionsQuery query,
        CancellationToken ct)
    {
        var card = await _cards.GetBySerialNumberAsync(query.SerialNumber, ct);
        if (card is null)
            return Result.Fail<PagedResult<TransactionDto>>(
                $"No se encontró tarjeta con serial '{query.SerialNumber}'.");

        var page = await _transactions.GetByCardIdAsync(card.Id, query.Pagination, ct);

        var dtos = page.Items
            .Select(t => new TransactionDto(
                t.Id,
                t.Points,
                t.Type,
                t.BonusType,
                t.Description,
                t.PurchaseAmount,
                t.CreatedAt,
                t.CreatedBy))
            .ToList()
            .AsReadOnly();

        return Result.Ok(new PagedResult<TransactionDto>(
            Items: dtos,
            TotalCount: page.TotalCount,
            PageNumber: page.PageNumber,
            PageSize: page.PageSize));
    }
}
