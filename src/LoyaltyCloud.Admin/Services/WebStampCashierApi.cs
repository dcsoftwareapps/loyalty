using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Admin.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace LoyaltyCloud.Admin.Services;

public sealed class WebStampCashierApi(IStampOperationsService service, ILogger<WebStampCashierApi> logger,
    AuthenticationStateProvider authentication, AdminCurrentUserService currentUser) : IStampCashierApi
{
    public async Task<StampCall<CashierStampState>> ReadAsync(string serial)
    {
        currentUser.SetCircuitPrincipal((await authentication.GetAuthenticationStateAsync()).User);
        try
        {
            var result = await service.ReadAsync(serial);
            return result.IsSuccess ? new(Map(result.Value)) : new(null, result.Error);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cashier Stamps customer lookup failed.");
            return new(null, "No fue posible consultar al cliente. Intenta de nuevo.", true);
        }
    }

    public async Task<StampCall<CashierStampResult>> ExecuteAsync(PendingStampOperation operation)
    {
        try
        {
            currentUser.SetCircuitPrincipal((await authentication.GetAuthenticationStateAsync()).User);
            var result = await service.ExecuteAsync(operation.Operation, operation.SerialNumber, operation.IdempotencyKey, operation.RewardId);
            return result.IsSuccess
                ? new(new(Map(result.Value.State), result.Value.UnlockedRewards.Select(Map).ToList()))
                : new(null, result.Error);
        }
        catch (Exception ex)
        {
            // A failed SQL response cannot prove whether the transaction committed.
            logger.LogError(ex, "Cashier Stamps operation could not be confirmed. Operation={Operation}", operation.Operation);
            return new(null, "No pudimos confirmar la operación.", true);
        }
    }

    private static CashierStampReward Map(StampRewardDto r) => new(r.Id, r.Name, r.Description, r.RequiredStamps, r.EarnedAt);
    private static CashierStampState Map(StampStateDto s) => new(s.SerialNumber, s.CustomerName, s.CycleId,
        s.CycleSequence, s.CurrentStamps, s.MaxStamps, s.CycleCompleted, s.AvailableRewards.Select(Map).ToList(), s.CanReverseLastEarn);
}
