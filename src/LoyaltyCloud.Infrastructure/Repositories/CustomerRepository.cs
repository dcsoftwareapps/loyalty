using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Repositories;

internal sealed class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CustomerRepository(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Customers.FirstOrDefaultAsync(c => c.TenantId == _tenantContext.RequireTenantId() && c.Id == id, ct);

    public Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var tenantId = _tenantContext.RequireTenantId();
        return _db.Customers.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Email == normalized, ct);
    }

    public Task<Customer?> GetByNormalizedPhoneAsync(string normalizedPhone, CancellationToken ct = default)
    {
        var normalized = normalizedPhone.Trim();
        var tenantId = _tenantContext.RequireTenantId();
        return _db.Customers.FirstOrDefaultAsync(c =>
            c.TenantId == tenantId && c.NormalizedPhone == normalized, ct);
    }

    public Task<Customer?> GetBySerialNumberAsync(string serialNumber, CancellationToken ct = default)
    {
        var normalized = serialNumber.Trim().ToUpperInvariant();
        var tenantId = _tenantContext.RequireTenantId();
        return _db.Customers
            .Where(c => _db.LoyaltyCards
                .Any(card => card.TenantId == tenantId
                          && card.CustomerId == c.Id
                          && card.SerialNumber == normalized))
            .Where(c => c.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var tenantId = _tenantContext.RequireTenantId();
        return _db.Customers
            .AsNoTracking()
            .AnyAsync(c => c.TenantId == tenantId && c.Email == normalized, ct);
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
