namespace LoyaltyCloud.Application.Customers.Queries.GetCustomers;

/// <summary>Fila resumida para la tabla de clientas.</summary>
public sealed record CustomerListItemDto(
    Guid CustomerId,
    string FullName,
    string Email,
    string SerialNumber,
    string Level,
    int CurrentPoints,
    DateTime CreatedAt);
