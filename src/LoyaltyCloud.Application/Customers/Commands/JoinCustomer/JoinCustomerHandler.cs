using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Application.Customers.Commands.RegisterCustomer;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using MediatR;

namespace LoyaltyCloud.Application.Customers.Commands.JoinCustomer;

public sealed class JoinCustomerHandler : IRequestHandler<JoinCustomerCommand, Result<JoinCustomerResponse>>
{
    private readonly ICustomerRepository _customers;
    private readonly ILoyaltyCardRepository _cards;
    private readonly ISender _sender;

    public JoinCustomerHandler(
        ICustomerRepository customers,
        ILoyaltyCardRepository cards,
        ISender sender)
    {
        _customers = customers;
        _cards = cards;
        _sender = sender;
    }

    public async Task<Result<JoinCustomerResponse>> Handle(JoinCustomerCommand command, CancellationToken ct)
    {
        var phone = CustomerPhoneNormalizer.Normalize(command.Phone);
        if (string.IsNullOrWhiteSpace(phone))
            return Result.Fail<JoinCustomerResponse>("El telefono es obligatorio.");

        var existingCustomer = await _customers.GetByNormalizedPhoneAsync(phone, ct);
        if (existingCustomer is not null)
        {
            if (!existingCustomer.IsActive)
                return Result.Fail<JoinCustomerResponse>("No pudimos recuperar una tarjeta con estos datos. Contacta al negocio.");

            if (!CustomerNameNormalizer.Matches(existingCustomer.FullName, command.FirstName, command.LastName))
                return Result.Fail<JoinCustomerResponse>("Este número de teléfono ya está registrado.");

            var existingCard = await _cards.GetByCustomerIdAsync(existingCustomer.Id, ct);
            if (existingCard is null)
                return Result.Fail<JoinCustomerResponse>("El cliente existe pero no tiene tarjeta Loyalty.");

            return Result.Ok(new JoinCustomerResponse(
                existingCustomer.Id,
                existingCard.SerialNumber,
                existingCustomer.FullName,
                phone,
                AlreadyExists: true,
                "Ya tienes una tarjeta. Encontramos tu cuenta existente. Puedes volver a agregarla a tu Wallet."));
        }

        var fullName = $"{command.FirstName.Trim()} {command.LastName.Trim()}".Trim();
        var registerResult = await _sender.Send(new RegisterCustomerCommand(
            FullName: fullName,
            Email: BuildInternalEmail(phone),
            DateOfBirth: Customer.BirthdayNotCaptured,
            Phone: phone,
            ReferredBySerialNumber: null), ct);

        if (registerResult.IsFailure)
        {
            var recoveryResult = await TryRecoverExistingCustomerAsync(phone, command, ct);
            return recoveryResult ?? Result.Fail<JoinCustomerResponse>(registerResult.Errors);
        }

        var createdCustomer = await _customers.GetByNormalizedPhoneAsync(phone, ct);
        if (createdCustomer is null)
            return Result.Fail<JoinCustomerResponse>("El cliente fue registrado pero no pudo recuperarse por telefono.");

        return Result.Ok(new JoinCustomerResponse(
            CustomerId: createdCustomer.Id,
            SerialNumber: registerResult.Value.SerialNumber,
            FullName: fullName,
            Phone: phone,
            AlreadyExists: false,
            "Listo. Tu tarjeta de lealtad esta lista."));
    }

    private async Task<Result<JoinCustomerResponse>?> TryRecoverExistingCustomerAsync(
        string normalizedPhone,
        JoinCustomerCommand command,
        CancellationToken ct)
    {
        var existingCustomer = await _customers.GetByNormalizedPhoneAsync(normalizedPhone, ct);
        if (existingCustomer is null)
            return null;

        if (!existingCustomer.IsActive)
            return Result.Fail<JoinCustomerResponse>("No pudimos recuperar una tarjeta con estos datos. Contacta al negocio.");

        if (!CustomerNameNormalizer.Matches(existingCustomer.FullName, command.FirstName, command.LastName))
            return Result.Fail<JoinCustomerResponse>("Este número de teléfono ya está registrado.");

        var existingCard = await _cards.GetByCustomerIdAsync(existingCustomer.Id, ct);
        if (existingCard is null)
            return Result.Fail<JoinCustomerResponse>("El cliente existe pero no tiene tarjeta Loyalty.");

        return Result.Ok(new JoinCustomerResponse(
            existingCustomer.Id,
            existingCard.SerialNumber,
            existingCustomer.FullName,
            normalizedPhone,
            AlreadyExists: true,
            "Ya tienes una tarjeta. Encontramos tu cuenta existente. Puedes volver a agregarla a tu Wallet."));
    }

    private static string BuildInternalEmail(string normalizedPhone) =>
        $"phone-{normalizedPhone}@loyaltycloud.local";
}
