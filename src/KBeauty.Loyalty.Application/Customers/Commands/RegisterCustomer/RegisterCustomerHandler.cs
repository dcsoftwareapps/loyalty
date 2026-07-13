using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Extensions;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Domain.Repositories;
using KBeauty.Loyalty.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KBeauty.Loyalty.Application.Customers.Commands.RegisterCustomer;

/// <summary>
/// Implementa el alta de clienta:
/// <list type="number">
///   <item>Verifica unicidad de email.</item>
///   <item>Resuelve referidor (si se proveyó serial).</item>
///   <item>Crea Customer + LoyaltyCard con serial determinista.</item>
///   <item>Aplica bono de bienvenida (con su <c>PointTransaction</c>).</item>
///   <item>Aplica bono al referidor si aplica.</item>
///   <item>Commit en una sola unidad de trabajo.</item>
///   <item>Genera .pkpass y devuelve URL de descarga.</item>
/// </list>
/// </summary>
public sealed class RegisterCustomerHandler
    : IRequestHandler<RegisterCustomerCommand, Result<RegisterCustomerResponse>>
{
    private readonly ICustomerRepository _customers;
    private readonly ILoyaltyCardRepository _cards;
    private readonly IPointTransactionRepository _transactions;
    private readonly IPointLotRepository _pointLots;
    private readonly IProgramConfigRepository _config;
    private readonly IPassGeneratorService _passes;
    private readonly IStorageService _storage;
    private readonly IDateTimeProvider _dt;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<RegisterCustomerHandler> _logger;

    public RegisterCustomerHandler(
        ICustomerRepository customers,
        ILoyaltyCardRepository cards,
        IPointTransactionRepository transactions,
        IPointLotRepository pointLots,
        IProgramConfigRepository config,
        IPassGeneratorService passes,
        IStorageService storage,
        IDateTimeProvider dt,
        ICurrentUserService currentUser,
        IUnitOfWork uow,
        ILogger<RegisterCustomerHandler> logger)
    {
        _customers = customers;
        _cards = cards;
        _transactions = transactions;
        _pointLots = pointLots;
        _config = config;
        _passes = passes;
        _storage = storage;
        _dt = dt;
        _currentUser = currentUser;
        _uow = uow;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<RegisterCustomerResponse>> Handle(
        RegisterCustomerCommand command,
        CancellationToken ct)
    {
        // 1. Email único
        var emailNormalized = command.Email.Trim().ToLowerInvariant();
        if (await _customers.EmailExistsAsync(emailNormalized, ct))
            return Result.Fail<RegisterCustomerResponse>($"Ya existe una clienta con email {emailNormalized}.");

        // 2. Resolver referidor (si aplica)
        LoyaltyCard? referrerCard = null;
        Guid? referredBy = null;
        if (!string.IsNullOrWhiteSpace(command.ReferredBySerialNumber))
        {
            referrerCard = await _cards.GetBySerialNumberAsync(command.ReferredBySerialNumber, ct);
            if (referrerCard is null)
                return Result.Fail<RegisterCustomerResponse>(
                    $"No se encontró la tarjeta de referido '{command.ReferredBySerialNumber}'.");
            referredBy = referrerCard.CustomerId;
        }

        var now = _dt.UtcNow;
        var operatorName = _currentUser.UserName ?? "system";

        // 3. Crear Customer + Card
        var customerId = Guid.NewGuid();
        var customer = new Customer(
            id: customerId,
            fullName: command.FullName,
            email: emailNormalized,
            dateOfBirth: command.DateOfBirth,
            createdAtUtc: now,
            phone: command.Phone,
            referredBy: referredBy);

        var cardId = Guid.NewGuid();
        var serial = customerId.ToString("N").ToSerialNumber();
        var card = new LoyaltyCard(cardId, customerId, serial, now);

        await _customers.AddAsync(customer, ct);
        await _cards.AddAsync(card, ct);

        // 4. Snapshot de config y bono de bienvenida
        var snapshot = ProgramConfigSnapshot.FromEntries(await _config.GetAllAsync(ct));

        if (snapshot.WelcomeBonusPoints > 0)
        {
            card.EarnPoints(snapshot.WelcomeBonusPoints, TransactionType.BonusWelcome, snapshot, _dt);
            var transactionId = Guid.NewGuid();
            await _transactions.AddAsync(new PointTransaction(
                id: transactionId,
                loyaltyCardId: cardId,
                points: snapshot.WelcomeBonusPoints,
                type: TransactionType.BonusWelcome,
                description: "Bono de bienvenida al programa",
                createdAtUtc: now,
                bonusType: BonusType.Welcome,
                createdBy: operatorName), ct);
            await _pointLots.AddLotAsync(new PointLot(
                id: Guid.NewGuid(),
                loyaltyCardId: cardId,
                sourcePointTransactionId: transactionId,
                amount: snapshot.WelcomeBonusPoints,
                earnedAtUtc: now,
                expiresAtUtc: now.AddMonths(snapshot.PointsExpireAfterMonths),
                createdAtUtc: now), ct);
        }

        // 5. Bono al referidor
        if (referrerCard is not null && snapshot.ReferralBonusPoints > 0)
        {
            referrerCard.EarnPoints(snapshot.ReferralBonusPoints, TransactionType.BonusReferral, snapshot, _dt);
            _cards.Update(referrerCard);
            var transactionId = Guid.NewGuid();
            await _transactions.AddAsync(new PointTransaction(
                id: transactionId,
                loyaltyCardId: referrerCard.Id,
                points: snapshot.ReferralBonusPoints,
                type: TransactionType.BonusReferral,
                description: $"Referido nuevo: {command.FullName}",
                createdAtUtc: now,
                bonusType: BonusType.Referral,
                createdBy: operatorName), ct);
            await _pointLots.AddLotAsync(new PointLot(
                id: Guid.NewGuid(),
                loyaltyCardId: referrerCard.Id,
                sourcePointTransactionId: transactionId,
                amount: snapshot.ReferralBonusPoints,
                earnedAtUtc: now,
                expiresAtUtc: now.AddMonths(snapshot.PointsExpireAfterMonths),
                createdAtUtc: now), ct);
        }

        // 6. Commit transaccional
        await _uow.SaveChangesAsync(ct);

        // 7. Generar y subir pase. Si falla, el cliente ya está creado — log y
        //    devolvemos URL vacía; podrá descargarlo después desde /api/customers/{serial}.
        string passUrl;
        try
        {
            var passBytes = await _passes.GeneratePassAsync(card, customer, ct);
            passUrl = await _storage.UploadPassAsync(serial, passBytes, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo generando .pkpass para serial {Serial}", serial);
            passUrl = string.Empty;
        }

        return Result.Ok(new RegisterCustomerResponse(
            SerialNumber: serial,
            PassDownloadUrl: passUrl,
            CurrentPoints: card.CurrentPoints,
            Level: card.Level));
    }
}
