using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Exceptions;
using LoyaltyCloud.Domain.Repositories;
using MediatR;

namespace LoyaltyCloud.Application.Redemptions.Commands.ConfirmRedemption;

/// <inheritdoc cref="ConfirmRedemptionCommand"/>
public sealed class ConfirmRedemptionHandler : IRequestHandler<ConfirmRedemptionCommand, Result>
{
    private readonly IRedemptionRepository _redemptions;
    private readonly IDateTimeProvider _dt;
    private readonly IUnitOfWork _uow;

    public ConfirmRedemptionHandler(
        IRedemptionRepository redemptions,
        IDateTimeProvider dt,
        IUnitOfWork uow)
    {
        _redemptions = redemptions;
        _dt = dt;
        _uow = uow;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(ConfirmRedemptionCommand command, CancellationToken ct)
    {
        var redemption = await _redemptions.GetByIdAsync(command.RedemptionId, ct);
        if (redemption is null)
            return Result.Fail($"No se encontró el canje {command.RedemptionId}.");

        if (redemption.Status != RedemptionStatus.Pending)
            return Result.Fail($"El canje ya está en estado {redemption.Status}.");

        try
        {
            redemption.Confirm(command.OperatorId, _dt.UtcNow, command.Notes);
        }
        catch (RedemptionAlreadyConfirmedException)
        {
            // Carrera entre dos confirmaciones — devolvemos error tratable.
            return Result.Fail("Otro operador ya resolvió este canje.");
        }

        _redemptions.Update(redemption);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
