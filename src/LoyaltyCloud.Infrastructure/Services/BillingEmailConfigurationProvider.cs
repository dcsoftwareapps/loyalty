using LoyaltyCloud.Application.Billing;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class BillingEmailConfigurationProvider(
    AppDbContext db,
    IOptions<EmailOptions> options,
    IHostEnvironment? environment = null) : IBillingEmailConfigurationProvider
{
    public async Task<BillingEmailSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var settings = await db.BillingSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Code == Domain.Entities.BillingSettings.SingletonCode, ct);
        var credentials = options.Value.CredentialsConfigured;
        if (settings is null)
            return new(false, options.Value.EffectiveProvider, null, "LoyaltyCloud", null, credentials, false);

        var complete = credentials
            && !string.IsNullOrWhiteSpace(settings.EmailProvider)
            && !string.IsNullOrWhiteSpace(settings.EmailFromAddress)
            && !string.IsNullOrWhiteSpace(settings.EmailFromName)
            && Uri.TryCreate(settings.EmailApplicationBaseUrl, UriKind.Absolute, out var uri)
            && (environment?.IsDevelopment() == true || (uri.Scheme == Uri.UriSchemeHttps && !uri.IsLoopback
                && string.IsNullOrEmpty(uri.UserInfo)));

        return new(settings.EmailNotificationsEnabled, options.Value.EffectiveProvider,
            settings.EmailFromAddress, settings.EmailFromName,
            settings.EmailApplicationBaseUrl, credentials, complete);
    }
}
