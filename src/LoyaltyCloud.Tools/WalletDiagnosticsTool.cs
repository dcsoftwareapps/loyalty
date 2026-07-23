using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Tools;

public sealed class WalletDiagnosticsTool
{
    private readonly AppDbContext _db;
    private readonly ApplePassOptions _apple;
    private readonly IConfiguration _configuration;

    public WalletDiagnosticsTool(
        AppDbContext db,
        IOptions<ApplePassOptions> apple,
        IConfiguration configuration)
    {
        _db = db;
        _apple = apple.Value;
        _configuration = configuration;
    }

    public async Task<int> RunAsync(
        string? serial,
        TextWriter output,
        TextWriter error,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            error.WriteLine("Falta --serial.");
            return 2;
        }

        var normalizedSerial = serial.Trim().ToUpperInvariant();
        var card = await _db.LoyaltyCards
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SerialNumber == normalizedSerial, ct);

        output.WriteLine($"Serial: {normalizedSerial}");
        output.WriteLine($"Configured Pass Type ID: {_apple.PassTypeIdentifier}");
        output.WriteLine($"Configured WebServiceURL: {_apple.WebServiceURL}");
        output.WriteLine($"Configured APNs host: {_apple.ApnHost}");
        output.WriteLine($"Wallet:UseRealApns: {_configuration["Wallet:UseRealApns"] ?? "<null>"}");
        output.WriteLine($"Wallet:UseRealPassSigning: {_configuration["Wallet:UseRealPassSigning"] ?? "<null>"}");
        output.WriteLine();

        if (card is null)
        {
            output.WriteLine("LoyaltyCard: NOT FOUND");
            return 1;
        }

        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == card.TenantId, ct);
        var customer = await _db.Customers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == card.TenantId && c.Id == card.CustomerId, ct);

        output.WriteLine($"TenantId: {card.TenantId}");
        output.WriteLine($"TenantSlug: {tenant?.Slug ?? "<missing>"}");
        output.WriteLine($"TenantActive: {tenant?.IsActive.ToString() ?? "<missing>"}");
        output.WriteLine($"Customer: {customer?.FullName ?? "<missing>"}");
        output.WriteLine($"Current card points: {card.CurrentPoints}");
        output.WriteLine($"Current generated pass points: {card.CurrentPoints}");
        output.WriteLine($"Level: {card.Level}");
        output.WriteLine($"LastActivityAt: {card.LastActivityAt:O}");
        output.WriteLine($"AuthenticationToken present: {!string.IsNullOrWhiteSpace(card.AuthenticationToken)}");
        output.WriteLine($"Expected pass.json serialNumber: {card.SerialNumber}");
        output.WriteLine($"Expected pass.json passTypeIdentifier: {_apple.PassTypeIdentifier}");
        output.WriteLine($"Expected pass.json webServiceURL: {_apple.WebServiceURL}");
        output.WriteLine();

        var registrations = await _db.DeviceRegistrations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(d => d.TenantId == card.TenantId && d.SerialNumber == normalizedSerial)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new
            {
                d.TenantId,
                d.SerialNumber,
                d.PassTypeIdentifier,
                d.DeviceLibraryIdentifier,
                d.PushToken,
                d.CreatedAt
            })
            .ToListAsync(ct);

        output.WriteLine($"Device registrations count: {registrations.Count}");
        foreach (var registration in registrations)
        {
            output.WriteLine(
                "Registration: " +
                $"tenantId={registration.TenantId}; " +
                $"serial={registration.SerialNumber}; " +
                $"passType={registration.PassTypeIdentifier}; " +
                $"device={Mask(registration.DeviceLibraryIdentifier)}; " +
                $"pushToken={Mask(registration.PushToken)}; " +
                $"createdAt={registration.CreatedAt:O}");
        }

        return 0;
    }

    private static string Mask(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "<empty>";

        return value.Length <= 8
            ? $"{value[..Math.Min(4, value.Length)]}..."
            : $"{value[..4]}...{value[^4..]}";
    }
}
