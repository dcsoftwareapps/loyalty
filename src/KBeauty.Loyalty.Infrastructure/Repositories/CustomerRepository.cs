using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Repositories;
using KBeauty.Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KBeauty.Loyalty.Infrastructure.Repositories;

internal sealed class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _db;

    public CustomerRepository(AppDbContext db) => _db = db;

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _db.Customers.FirstOrDefaultAsync(c => c.Email == normalized, ct);
    }

    public Task<Customer?> GetBySerialNumberAsync(string serialNumber, CancellationToken ct = default)
    {
        var normalized = serialNumber.Trim().ToUpperInvariant();
        return _db.Customers
            .Where(c => _db.LoyaltyCards
                .Any(card => card.CustomerId == c.Id && card.SerialNumber == normalized))
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _db.Customers
            .AsNoTracking()
            .AnyAsync(c => c.Email == normalized, ct);
    }

    public async Task AddAsync(Customer customer, CancellationToken ct = default)
    {
        await _db.Customers.AddAsync(customer, ct);
    }

    public void Update(Customer customer)
    {
        // Si ya está tracked es no-op; si no, lo marca como Modified.
        if (_db.Entry(customer).State == EntityState.Detached)
            _db.Customers.Update(customer);
    }
}
