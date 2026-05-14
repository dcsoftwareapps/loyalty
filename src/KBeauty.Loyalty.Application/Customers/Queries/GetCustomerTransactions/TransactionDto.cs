using KBeauty.Loyalty.Domain.Enums;

namespace KBeauty.Loyalty.Application.Customers.Queries.GetCustomerTransactions;

/// <summary>Fila del historial — incluye el delta signed para mostrar + o − en la UI.</summary>
public sealed record TransactionDto(
    Guid Id,
    int Points,
    TransactionType Type,
    BonusType? BonusType,
    string Description,
    decimal? PurchaseAmount,
    DateTime CreatedAt,
    string? CreatedBy);
