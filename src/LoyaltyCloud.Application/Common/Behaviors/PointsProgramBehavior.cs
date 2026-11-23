using System.Reflection;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Points.Commands.AddPoints;
using LoyaltyCloud.Application.Points.Commands.ExpirePoints;
using LoyaltyCloud.Application.Redemptions.Commands.RedeemReward;
using LoyaltyCloud.Application.Redemptions.Commands.RedeemMonetaryDiscount;
using LoyaltyCloud.Application.Redemptions.Commands.ConfirmRedemption;
using LoyaltyCloud.Application.Redemptions.Commands.CancelRedemption;
using LoyaltyCloud.Application.Levels.Commands.RecalculateLevels;
using LoyaltyCloud.Application.Levels.Commands.UpdateTenantLoyaltyLevels;
using LoyaltyCloud.Application.Config.Commands.UpdateProgramConfig;
using LoyaltyCloud.Application.Rewards.Commands.CreateReward;
using LoyaltyCloud.Application.Rewards.Commands.UpdateReward;
using LoyaltyCloud.Application.Rewards.Commands.ActivateReward;
using LoyaltyCloud.Application.Rewards.Commands.DeactivateReward;
using LoyaltyCloud.Application.Campaigns.Commands.CreatePointCampaign;
using LoyaltyCloud.Application.Campaigns.Commands.UpdatePointCampaign;
using LoyaltyCloud.Application.Campaigns.Commands.ActivatePointCampaign;
using LoyaltyCloud.Application.Campaigns.Commands.DeactivatePointCampaign;
using LoyaltyCloud.Application.Notifications.Commands.CreatePointExpirationNotifications;
using LoyaltyCloud.Application.Notifications.Commands.CreatePointCampaignStartedNotifications;
using LoyaltyCloud.Application.Notifications.Commands.CreateMonthlyProductStartedNotifications;
using LoyaltyCloud.Application.Notifications.Commands.CreateBirthdayBenefitStartedNotifications;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Enums;
using MediatR;

namespace LoyaltyCloud.Application.Common.Behaviors;

internal sealed class PointsProgramBehavior<TRequest, TResponse>(ITenantProgramReadService programs)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    internal const string Error = "Esta operacion solo esta disponible para programas de Puntos.";

    // Explicit write operations only; shared identity, auth, branding and Gift Cards are unaffected.
    private static readonly bool RequiresPoints = new HashSet<Type>
    {
        typeof(AddPointsCommand), typeof(ExpirePointsCommand), typeof(RedeemRewardCommand),
        typeof(RedeemMonetaryDiscountCommand), typeof(ConfirmRedemptionCommand), typeof(CancelRedemptionCommand),
        typeof(RecalculateLevelsCommand), typeof(UpdateTenantLoyaltyLevelsCommand), typeof(UpdateProgramConfigCommand),
        typeof(CreateRewardCommand), typeof(UpdateRewardCommand), typeof(ActivateRewardCommand), typeof(DeactivateRewardCommand),
        typeof(CreatePointCampaignCommand), typeof(UpdatePointCampaignCommand), typeof(ActivatePointCampaignCommand),
        typeof(DeactivatePointCampaignCommand), typeof(CreatePointExpirationNotificationsCommand),
        typeof(CreatePointCampaignStartedNotificationsCommand), typeof(CreateMonthlyProductStartedNotificationsCommand),
        typeof(CreateBirthdayBenefitStartedNotificationsCommand)
    }.Contains(typeof(TRequest));

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!RequiresPoints || await programs.GetCurrentAsync(ct) == LoyaltyProgramType.Points)
            return await next();

        // Same typed Result convention used by ValidationBehavior.
        var fail = typeof(TResponse).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == nameof(Result.Fail) && !method.IsGenericMethod
                && method.ReturnType == typeof(TResponse)
                && method.GetParameters() is { Length: 1 } parameters
                && parameters[0].ParameterType == typeof(string));
        return (TResponse)fail.Invoke(null, [Error])!;
    }
}
