using KBeauty.Loyalty.Domain.Entities;

namespace KBeauty.Loyalty.Domain.Repositories;

/// <summary>Acceso persistente al agregado <see cref="Customer"/>.</summary>
public interface ICustomerRepository
{
    /// <summary>Busca una clienta por Id. Devuelve <c>null</c> si no existe.</summary>
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Busca por email — normalizado a lowercase en la implementación.</summary>
    Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Busca la clienta a través del serial de su <see cref="LoyaltyCard"/>.</summary>
    Task<Customer?> GetBySerialNumberAsync(string serialNumber, CancellationToken ct = default);

    /// <summary>Indica si existe ya una clienta con ese email (sin traer la entidad).</summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);

    /// <summary>Marca la entidad para inserción. El commit se hace con <c>IUnitOfWork</c>.</summary>
    Task AddAsync(Customer customer, CancellationToken ct = default);

    /// <summary>Marca la entidad como modificada (con EF Core es no-op si está tracked).</summary>
    void Update(Customer customer);
}
