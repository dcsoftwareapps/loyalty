using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Provisioning;

public sealed class ProvisionTenantHandler
    : IRequestHandler<ProvisionTenantCommand, Result<ProvisionTenantResult>>
{
    private const string DefaultTimeZoneId = "America/Tijuana";
    private readonly ITenantProvisioningService _provisioning;

    public ProvisionTenantHandler(ITenantProvisioningService provisioning)
    {
        _provisioning = provisioning;
    }

    public async Task<Result<ProvisionTenantResult>> Handle(
        ProvisionTenantCommand command,
        CancellationToken ct)
    {
        var request = new ProvisionTenantRequest(
            Slug: command.Slug.Trim(),
            DisplayName: command.DisplayName.Trim(),
            TimeZoneId: string.IsNullOrWhiteSpace(command.TimeZoneId) ? DefaultTimeZoneId : command.TimeZoneId.Trim(),
            AdminUsername: command.AdminUsername.Trim(),
            AdminPassword: command.AdminPassword,
            PrimaryColor: command.PrimaryColor,
            SecondaryColor: command.SecondaryColor,
            SupportPhone: command.SupportPhone,
            WhatsAppUrl: command.WhatsAppUrl,
            InstagramUrl: command.InstagramUrl,
            TermsUrl: command.TermsUrl);

        return await _provisioning.ProvisionAsync(request, ct);
    }
}
