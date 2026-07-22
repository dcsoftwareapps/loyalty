using System.Text.RegularExpressions;
using FluentValidation;

namespace LoyaltyCloud.Application.Provisioning;

internal sealed partial class ProvisionTenantValidator : AbstractValidator<ProvisionTenantCommand>
{
    public ProvisionTenantValidator()
    {
        RuleFor(c => c.Slug)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(value => value.Trim().Length is >= 3 and <= 50)
            .WithMessage("Slug debe tener entre 3 y 50 caracteres.")
            .Must(value => SlugRegex().IsMatch(value.Trim()))
            .WithMessage("Slug solo puede contener minusculas, numeros y guiones intermedios.");

        RuleFor(c => c.DisplayName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(value => value.Trim().Length <= 200)
            .WithMessage("DisplayName no puede exceder 200 caracteres.");

        RuleFor(c => c.TimeZoneId)
            .Must(BeValidTimeZone)
            .When(c => !string.IsNullOrWhiteSpace(c.TimeZoneId))
            .WithMessage("TimeZoneId no es valido en este runtime.");

        RuleFor(c => c.AdminUsername)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(value => value.Trim().Length <= 150)
            .WithMessage("AdminUsername no puede exceder 150 caracteres.");

        RuleFor(c => c.AdminPassword)
            .NotEmpty()
            .MinimumLength(8);

        RuleFor(c => c.WhatsAppUrl)
            .Must(BeSafeOptionalUrl)
            .When(c => !string.IsNullOrWhiteSpace(c.WhatsAppUrl))
            .WithMessage("WhatsAppUrl debe ser una URL absoluta http, https o tel.");

        RuleFor(c => c.InstagramUrl)
            .Must(BeSafeOptionalHttpUrl)
            .When(c => !string.IsNullOrWhiteSpace(c.InstagramUrl))
            .WithMessage("InstagramUrl debe ser una URL absoluta http o https.");

        RuleFor(c => c.TermsUrl)
            .Must(BeSafeOptionalHttpUrl)
            .When(c => !string.IsNullOrWhiteSpace(c.TermsUrl))
            .WithMessage("TermsUrl debe ser una URL absoluta http o https.");
    }

    private static bool BeValidTimeZone(string? value)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(value!.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool BeSafeOptionalHttpUrl(string? value) =>
        BeSafeOptionalUrl(value, Uri.UriSchemeHttp, Uri.UriSchemeHttps);

    private static bool BeSafeOptionalUrl(string? value) =>
        BeSafeOptionalUrl(value, Uri.UriSchemeHttp, Uri.UriSchemeHttps, "tel");

    private static bool BeSafeOptionalUrl(string? value, params string[] schemes)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            && schemes.Any(scheme => string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}
