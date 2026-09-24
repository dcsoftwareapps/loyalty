using System.Text.RegularExpressions;
using FluentValidation;

namespace LoyaltyCloud.Application.SelfServiceSignup;

internal sealed partial class SelfServiceSignupValidator : AbstractValidator<SelfServiceSignupCommand>
{
    public SelfServiceSignupValidator()
    {
        RuleFor(command => command.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(value => value.Trim().Length <= 254)
            .WithMessage("Email no puede exceder 254 caracteres.")
            .EmailAddress()
            .WithMessage("Email debe ser un email valido.");

        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(8);

        RuleFor(command => command.BusinessDisplayName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(value => value.Trim().Length <= 200)
            .WithMessage("BusinessDisplayName no puede exceder 200 caracteres.");

        RuleFor(command => command.Slug)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(value => value.Trim().Length is >= 3 and <= 50)
            .WithMessage("Slug debe tener entre 3 y 50 caracteres.")
            .Must(value => SlugRegex().IsMatch(value.Trim()))
            .WithMessage("Slug solo puede contener minusculas, numeros y guiones intermedios.");

        RuleFor(command => command.TimeZoneId)
            .Must(BeValidTimeZone)
            .When(command => !string.IsNullOrWhiteSpace(command.TimeZoneId))
            .WithMessage("TimeZoneId no es valido en este runtime.");

        RuleFor(command => command.SignupAttemptId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(value => value.Trim().Length <= 100)
            .WithMessage("SignupAttemptId no puede exceder 100 caracteres.");
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

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}
