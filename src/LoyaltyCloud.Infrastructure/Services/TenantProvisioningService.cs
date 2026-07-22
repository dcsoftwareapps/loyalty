using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Provisioning;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class TenantProvisioningService : ITenantProvisioningService
{
    private const string DuplicateSlugError = "El identificador del negocio ya está en uso.";
    private readonly AppDbContext _db;
    private readonly IMutableTenantContext _tenantContext;
    private readonly IPasswordHashingService _passwords;
    private readonly IDateTimeProvider _clock;
    private readonly ProvisioningOptions _options;
    private readonly ILogger<TenantProvisioningService> _logger;

    public TenantProvisioningService(
        AppDbContext db,
        IMutableTenantContext tenantContext,
        IPasswordHashingService passwords,
        IDateTimeProvider clock,
        IOptions<ProvisioningOptions> options,
        ILogger<TenantProvisioningService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _passwords = passwords;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<ProvisionTenantResult>> ProvisionAsync(
        ProvisionTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.HasTenant)
            return Result.Fail<ProvisionTenantResult>("Provisioning debe ejecutarse sin TenantContext previo.");

        string slug;
        try
        {
            slug = Tenant.NormalizeSlug(request.Slug);
        }
        catch (ArgumentException ex)
        {
            return Result.Fail<ProvisionTenantResult>(ex.Message);
        }

        _logger.LogInformation("Tenant provisioning started. TenantSlug={TenantSlug}", slug);

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                if (await _db.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken))
                {
                    _logger.LogWarning(
                        "Tenant provisioning failed. TenantSlug={TenantSlug}, Reason={Reason}",
                        slug,
                        "duplicate_slug");
                    return Result.Fail<ProvisionTenantResult>(DuplicateSlugError);
                }

                var now = _clock.UtcNow;
                var tenantId = Guid.NewGuid();
                var adminUserId = Guid.NewGuid();
                var trialDays = Math.Max(1, _options.TrialDays);

                var tenant = new Tenant(
                    tenantId,
                    slug,
                    request.DisplayName,
                    request.TimeZoneId,
                    now);
                var branding = new TenantBranding(
                    tenantId,
                    primaryColor: TenantBrandingSanitizer.ColorOrDefault(
                        request.PrimaryColor,
                        TenantBrandingSanitizer.DefaultPrimaryColor,
                        tenantId,
                        "PrimaryColor",
                        _logger),
                    secondaryColor: TenantBrandingSanitizer.ColorOrDefault(
                        request.SecondaryColor,
                        TenantBrandingSanitizer.DefaultSecondaryColor,
                        tenantId,
                        "SecondaryColor",
                        _logger),
                    supportPhone: request.SupportPhone,
                    whatsAppUrl: TenantBrandingSanitizer.UrlOrNull(
                        request.WhatsAppUrl,
                        tenantId,
                        "WhatsAppUrl",
                        _logger,
                        Uri.UriSchemeHttp,
                        Uri.UriSchemeHttps,
                        "tel"),
                    instagramUrl: TenantBrandingSanitizer.UrlOrNull(
                        request.InstagramUrl,
                        tenantId,
                        "InstagramUrl",
                        _logger,
                        Uri.UriSchemeHttp,
                        Uri.UriSchemeHttps),
                    termsUrl: TenantBrandingSanitizer.UrlOrNull(
                        request.TermsUrl,
                        tenantId,
                        "TermsUrl",
                        _logger,
                        Uri.UriSchemeHttp,
                        Uri.UriSchemeHttps));
                var subscription = new TenantSubscription(
                    tenantId,
                    TenantSubscriptionStatus.Trial,
                    "trial",
                    currentPeriodStart: now,
                    currentPeriodEnd: now.AddDays(trialDays));

                _db.Tenants.Add(tenant);
                _db.TenantBrandings.Add(branding);
                _db.TenantSubscriptions.Add(subscription);

                _tenantContext.SetTenant(tenantId, slug);

                var passwordHash = _passwords.HashPassword(request.AdminPassword);
                _db.TenantAdminUsers.Add(new TenantAdminUser(
                    adminUserId,
                    tenantId,
                    request.AdminUsername,
                    passwordHash,
                    now));

                foreach (var row in TenantProvisioningDefaults.ProgramConfigRows)
                {
                    _db.ProgramConfigs.Add(new ProgramConfig(
                        Guid.NewGuid(),
                        tenantId,
                        row.Key,
                        row.Value,
                        now,
                        row.Description,
                        TenantProvisioningDefaults.UpdatedBy));
                }

                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Tenant provisioning completed. TenantId={TenantId}, TenantSlug={TenantSlug}",
                    tenantId,
                    slug);

                return Result.Ok(new ProvisionTenantResult(
                    tenantId,
                    slug,
                    adminUserId,
                    TenantSubscriptionStatus.Trial.ToString()));
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogWarning(
                    ex,
                    "Tenant provisioning failed. TenantSlug={TenantSlug}, Reason={Reason}",
                    slug,
                    "duplicate_slug_constraint");
                return Result.Fail<ProvisionTenantResult>(DuplicateSlugError);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogError(
                    ex,
                    "Tenant provisioning failed. TenantSlug={TenantSlug}, Reason={Reason}",
                    slug,
                    "unexpected_error");
                throw;
            }
        });
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var current = ex.InnerException;
        while (current is not null)
        {
            if (current.GetType().Name == "SqlException"
                && (current.Message.Contains("2601", StringComparison.Ordinal)
                    || current.Message.Contains("2627", StringComparison.Ordinal)
                    || current.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                    || current.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            current = current.InnerException;
        }

        return ex.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }
}
