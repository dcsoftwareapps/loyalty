using FluentValidation;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;

namespace LoyaltyCloud.Application.Notifications.Custom.Commands.CreateCustomNotificationCampaign;

internal sealed class CreateCustomNotificationCampaignValidator : AbstractValidator<CreateCustomNotificationCampaignCommand>
{
    public CreateCustomNotificationCampaignValidator(ITenantLoyaltyLevelReadService levels)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(80);
        RuleFor(x => x.ShortMessage)
            .NotEmpty()
            .MaximumLength(40)
            .Must(value => value is null || (!value.Contains('\n') && !value.Contains('\r') && !value.Contains('\t')))
            .WithMessage("ShortMessage no debe contener saltos de linea ni tabuladores.");
        RuleFor(x => x.LongMessage).NotEmpty().MaximumLength(500);
        RuleFor(x => x.AudienceType)
            .MustAsync((audienceType, ct) => IsValidAudienceTypeAsync(audienceType, levels, ct))
            .WithMessage("AudienceType debe ser una audiencia soportada o un nivel activo del tenant actual.");
        RuleFor(x => x.MinimumPoints)
            .GreaterThanOrEqualTo(0)
            .When(x => CustomNotificationCampaign.IsMinimumPointsAudience(x.AudienceType));
        RuleFor(x => x.MinimumPoints)
            .Null()
            .When(x => !CustomNotificationCampaign.IsMinimumPointsAudience(x.AudienceType))
            .WithMessage("MinimumPoints solo aplica para audiencia MinimumPoints.");
        RuleFor(x => x.PointsExpiringDaysAhead)
            .GreaterThan(0)
            .When(x => CustomNotificationCampaign.IsPointsExpiringAudience(x.AudienceType));
        RuleFor(x => x.PointsExpiringDaysAhead)
            .Null()
            .When(x => !CustomNotificationCampaign.IsPointsExpiringAudience(x.AudienceType))
            .WithMessage("PointsExpiringDaysAhead solo aplica para audiencia PointsExpiring.");
        RuleFor(x => x.DisplayUntilUtc)
            .GreaterThan(DateTime.UtcNow)
            .When(x => x.SendImmediately);
        RuleFor(x => x.DisplayUntilUtc)
            .GreaterThan(x => x.ScheduledAtUtc!.Value)
            .When(x => !x.SendImmediately && x.ScheduledAtUtc.HasValue);
        RuleFor(x => x.ScheduledAtUtc)
            .NotNull()
            .When(x => !x.SendImmediately)
            .WithMessage("ScheduledAtUtc es requerido para campanas programadas.");
        RuleFor(x => x.ScheduledAtUtc)
            .GreaterThan(DateTime.UtcNow)
            .When(x => !x.SendImmediately && x.ScheduledAtUtc.HasValue)
            .WithMessage("ScheduledAtUtc debe ser futuro para campanas programadas.");
    }

    private static async Task<bool> IsValidAudienceTypeAsync(
        string? audienceType,
        ITenantLoyaltyLevelReadService levels,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(audienceType))
            return false;

        var value = audienceType.Trim();
        if (CustomNotificationCampaign.IsAllWalletUsersAudience(value) ||
            CustomNotificationCampaign.IsMinimumPointsAudience(value) ||
            CustomNotificationCampaign.IsPointsExpiringAudience(value))
            return true;

        if (value.Length > 50)
            return false;

        var activeLevels = await levels.GetActiveLevelsAsync(ct);
        return activeLevels.Any(level =>
            string.Equals(level.Name, value, StringComparison.OrdinalIgnoreCase));
    }
}
