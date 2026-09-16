using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.GiftCards;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class GiftCardService(AppDbContext db, IDbContextFactory<AppDbContext> dbContextFactory, ITenantContext tenant, IDateTimeProvider clock, ICurrentUserService user, IGiftCardWalletService wallets, IGiftCardAppleWalletService appleWallets) : IGiftCardService
{
    private static readonly Guid SystemUserId = Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFF1");
    public async Task<bool> IsEnabledAsync(CancellationToken ct = default)
    {
        await using var readDb = await dbContextFactory.CreateDbContextAsync(ct);
        return await readDb.GiftCardConfigurations.AsNoTracking().AnyAsync(x => x.IsEnabled, ct);
    }

    public async Task SetEnabledAsync(bool enabled, CancellationToken ct = default)
    {
        var config = await ConfigurationAsync(create: true, ct);
        config.SetEnabled(enabled, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task<GiftCardSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var config = await ConfigurationAsync(create: true, ct);
        return await SettingsDtoAsync(config, ct);
    }

    public async Task<GiftCardSettingsDto> UpdateSettingsAsync(UpdateGiftCardSettingsRequest request, CancellationToken ct = default)
    {
        ValidateSettings(request);
        var config = await ConfigurationAsync(create: true, ct);
        config.Update(request.IsEnabled, request.AllowCustomAmount, request.AllowPartialRedemption, request.AllowPromotionalIssuance, request.ExpirationMode, request.DefaultExpirationMonths, request.Currency.Trim(), request.DisplayName.Trim(), request.PrimaryColor.Trim(), request.TextColor.Trim(), Clean(request.LogoUrl), Clean(request.SecondaryText), Clean(request.Terms), Clean(request.FooterMessage), clock.UtcNow);
        await db.SaveChangesAsync(ct);
        var sync = await wallets.SynchronizeBrandingAsync(ct);
        var result = await SettingsDtoAsync(config, ct);
        return sync.Failed > 0 ? result with { SyncWarning = "La configuración se guardó, pero algunas tarjetas de Google Wallet no pudieron actualizarse." } : result;
    }

    public async Task<GiftCardDenominationDto> AddDenominationAsync(decimal amount, CancellationToken ct = default)
    {
        var config = await EnabledConfigurationAsync(ct);
        ValidateMoney(amount, "Ingresa un monto mayor a $0.");
        var duplicate = await db.GiftCardDenominations.AnyAsync(x => x.Amount == decimal.Round(amount, 2) && x.Currency == config.Currency, ct);
        if (duplicate) throw new InvalidOperationException("La denominación ya existe.");
        var item = new GiftCardDenomination(Guid.NewGuid(), TenantId(), amount, config.Currency, clock.UtcNow);
        db.GiftCardDenominations.Add(item); await db.SaveChangesAsync(ct);
        return new(item.Id, item.Amount, item.Currency, item.IsActive);
    }

    public async Task SetDenominationActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        await EnabledConfigurationAsync(ct);
        var item = await db.GiftCardDenominations.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Denominación no encontrada.");
        item.SetActive(active); await db.SaveChangesAsync(ct);
    }

    public async Task<IssuedGiftCardDto> IssueAsync(IssueGiftCardRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateMoney(request.Amount, "Ingresa un monto mayor a cero con máximo dos decimales.");
        var recipientName = Required(request.RecipientName, 150, "Ingresa el nombre del destinatario.");
        var email = Optional(request.RecipientEmail, 254);
        if (email is not null && !new EmailAddressAttribute().IsValid(email))
            throw new ArgumentException("Ingresa un correo electrónico válido.");
        var phone = Optional(request.RecipientPhone, 30);
        if (phone is not null && !ValidPhone(phone))
            throw new ArgumentException("Ingresa un número de teléfono válido.");
        var senderName = Optional(request.SenderName, 150);
        var personalMessage = Optional(request.PersonalMessage, 500);
        var amount = decimal.Round(request.Amount, 2);
        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        if (idempotencyKey is not null)
        {
            var replay = await ReplayIssueAsync(idempotencyKey, amount, request.RecipientMemberId, recipientName,
                email, phone, senderName, personalMessage, request.Source, request.ExpiresAtUtc, ct);
            if (replay is not null) return replay;
        }
        var config = await EnabledConfigurationAsync(ct);
        if (!config.AllowCustomAmount && !await db.GiftCardDenominations.AnyAsync(x => x.IsActive && x.Currency == config.Currency && x.Amount == amount, ct))
            throw new InvalidOperationException("Selecciona una denominación habilitada.");
        if (request.Source == GiftCardSource.Promotional && !config.AllowPromotionalIssuance)
            throw new InvalidOperationException("La emisión promocional está deshabilitada.");
        var now = clock.UtcNow;
        var expires = ResolveExpiration(config, request.ExpiresAtUtc, now);
        var token = GenerateClaimToken();
        var card = new GiftCard(Guid.NewGuid(), TenantId(), await UniqueCodeAsync(ct), GiftCard.HashClaimToken(token), amount, config.Currency, request.RecipientMemberId, recipientName, email, phone, senderName, personalMessage, request.Source, UserId(), now, expires);
        db.GiftCards.Add(card);
        db.GiftCardTransactions.Add(new GiftCardTransaction(Guid.NewGuid(), TenantId(), card.Id, GiftCardTransactionType.Issued, amount, 0, amount, UserId(), now, notes: personalMessage, idempotencyKey: idempotencyKey));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (idempotencyKey is not null)
        {
            db.ChangeTracker.Clear();
            var replay = await ReplayIssueAsync(idempotencyKey, amount, request.RecipientMemberId, recipientName,
                email, phone, senderName, personalMessage, request.Source, request.ExpiresAtUtc, ct);
            if (replay is not null) return replay;
            throw;
        }
        return new(ToDto(card), token);
    }
    public async Task<GiftCardPage> SearchAsync(string? search, GiftCardStatus? status, DateTime? fromUtc, DateTime? toUtc, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        await EnabledConfigurationAsync(ct); page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.GiftCards.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var value = search.Trim(); query = query.Where(x => x.PublicCode.Contains(value) || x.RecipientName.Contains(value) || (x.RecipientEmail != null && x.RecipientEmail.Contains(value))); }
        if (status is not null) query = query.Where(x => x.Status == status);
        if (fromUtc is not null) query = query.Where(x => x.IssuedAtUtc >= fromUtc);
        if (toUtc is not null) query = query.Where(x => x.IssuedAtUtc < toUtc);
        var total = await query.CountAsync(ct);
        var cards = await query.OrderByDescending(x => x.IssuedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(cards.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<GiftCardDetailDto?> GetAsync(Guid id, CancellationToken ct = default) { await EnabledConfigurationAsync(ct); var card = await db.GiftCards.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); return card is null ? null : await DetailAsync(card, ct); }
    public async Task<GiftCardDetailDto?> GetByCodeAsync(string code, CancellationToken ct = default) { await EnabledConfigurationAsync(ct); var normalized = code.Trim().ToUpperInvariant(); var card = await db.GiftCards.SingleOrDefaultAsync(x => x.PublicCode == normalized, ct); if (card is null) return null; await PersistExpirationAsync(card, ct); return await DetailAsync(card, ct); }
    public async Task<GiftCardDetailDto?> GetByClaimTokenAsync(string claimToken, CancellationToken ct = default)
    {
        await EnabledConfigurationAsync(ct);
        if (string.IsNullOrWhiteSpace(claimToken) || claimToken.Length > 256)
            return null;

        var hash = GiftCard.HashClaimToken(claimToken.Trim());
        var card = await db.GiftCards.SingleOrDefaultAsync(x => x.ClaimTokenHash == hash && !x.ClaimRevoked, ct);
        if (card is null)
            return null;

        await PersistExpirationAsync(card, ct);
        return await DetailAsync(card, ct);
    }

    public async Task<IssuedGiftCardDto> RotateClaimTokenAsync(Guid id, CancellationToken ct = default)
    {
        await EnabledConfigurationAsync(ct);
        var card = await db.GiftCards.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Tarjeta de regalo no encontrada.");
        card.EvaluateExpiration(clock.UtcNow);
        if (card.Status != GiftCardStatus.Active)
            throw new InvalidOperationException("Esta tarjeta de regalo ya no está disponible para entrega.");
        if (string.IsNullOrWhiteSpace(card.RecipientEmail))
            throw new InvalidOperationException("La tarjeta de regalo no tiene email de destinatario.");

        var token = GenerateClaimToken();
        card.ReplaceClaimTokenHash(GiftCard.HashClaimToken(token), clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return new(ToDto(card), token);
    }

    public async Task<GiftCardOperationResult> RedeemAsync(string code, decimal amount, string idempotencyKey, string? reference, string? notes, CancellationToken ct = default)
    {
        ValidateMoney(amount, "Ingresa un monto mayor a $0.");
        var config = await EnabledConfigurationAsync(ct); return await MutateAsync(idempotencyKey, GiftCardTransactionType.Redeemed, -amount, async () => await db.GiftCards.SingleOrDefaultAsync(x => x.PublicCode == code.Trim().ToUpperInvariant(), ct), (card, now) => { var balances = card.Redeem(amount, config.AllowPartialRedemption, now); return new GiftCardTransaction(Guid.NewGuid(), TenantId(), card.Id, GiftCardTransactionType.Redeemed, -decimal.Round(amount, 2), balances.Before, balances.After, UserId(), now, reference, notes, idempotencyKey); }, ct);
    }

    public async Task<GiftCardOperationResult> AdjustAsync(Guid id, decimal amount, string idempotencyKey, string? reference, string? notes, CancellationToken ct = default)
    {
        ValidateMoney(Math.Abs(amount), "Ingresa un monto mayor a $0.");
        if (string.IsNullOrWhiteSpace(notes)) return new(false, "El motivo es requerido.", null);
        await EnabledConfigurationAsync(ct); return await MutateAsync(idempotencyKey, amount > 0 ? GiftCardTransactionType.AdjustmentCredit : GiftCardTransactionType.AdjustmentDebit, amount, async () => await db.GiftCards.SingleOrDefaultAsync(x => x.Id == id, ct), (card, now) => { var balances = card.Adjust(amount, now); var type = amount > 0 ? GiftCardTransactionType.AdjustmentCredit : GiftCardTransactionType.AdjustmentDebit; return new GiftCardTransaction(Guid.NewGuid(), TenantId(), card.Id, type, decimal.Round(amount, 2), balances.Before, balances.After, UserId(), now, reference, notes, idempotencyKey); }, ct);
    }

    public async Task<GiftCardOperationResult> CancelAsync(Guid id, string idempotencyKey, string? notes, CancellationToken ct = default)
    {
        await EnabledConfigurationAsync(ct);
        return await MutateAsync(idempotencyKey, GiftCardTransactionType.Cancelled, 0m, async () => await db.GiftCards.SingleOrDefaultAsync(x => x.Id == id, ct), (card, now) => { var balance = card.Cancel(now); return new GiftCardTransaction(Guid.NewGuid(), TenantId(), card.Id, GiftCardTransactionType.Cancelled, 0, balance, balance, UserId(), now, notes: Clean(notes), idempotencyKey: idempotencyKey); }, ct);
    }
    public async Task<GiftCardDashboardDto> GetDashboardAsync(DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default)
    {
        await EnabledConfigurationAsync(ct); var cards = db.GiftCards.AsNoTracking(); var tx = db.GiftCardTransactions.AsNoTracking();
        if (fromUtc is not null) tx = tx.Where(x => x.CreatedAtUtc >= fromUtc); if (toUtc is not null) tx = tx.Where(x => x.CreatedAtUtc < toUtc);
        return new(await cards.CountAsync(x => x.Status == GiftCardStatus.Active, ct), await cards.Where(x => x.Status == GiftCardStatus.Active).SumAsync(x => (decimal?)x.CurrentBalance, ct) ?? 0, await tx.Where(x => x.Type == GiftCardTransactionType.Issued).SumAsync(x => (decimal?)x.Amount, ct) ?? 0, -(await tx.Where(x => x.Type == GiftCardTransactionType.Redeemed).SumAsync(x => (decimal?)x.Amount, ct) ?? 0), await cards.CountAsync(x => x.Status == GiftCardStatus.FullyRedeemed, ct), await cards.CountAsync(x => x.Status == GiftCardStatus.Expired, ct), await cards.CountAsync(x => x.Status == GiftCardStatus.Cancelled, ct));
    }

    public async Task<IReadOnlyList<GiftCardReportPoint>> GetReportAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        await EnabledConfigurationAsync(ct); if (toUtc <= fromUtc) throw new ArgumentException("Rango inválido.");
        var rows = await db.GiftCardTransactions.AsNoTracking().Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtc && (x.Type == GiftCardTransactionType.Issued || x.Type == GiftCardTransactionType.Redeemed)).GroupBy(x => x.CreatedAtUtc.Date).Select(g => new { Date = g.Key, Issued = g.Where(x => x.Type == GiftCardTransactionType.Issued).Sum(x => x.Amount), Redeemed = -g.Where(x => x.Type == GiftCardTransactionType.Redeemed).Sum(x => x.Amount), IssuedCount = g.Count(x => x.Type == GiftCardTransactionType.Issued), RedemptionCount = g.Count(x => x.Type == GiftCardTransactionType.Redeemed) }).OrderBy(x => x.Date).ToListAsync(ct);
        return rows.Select(x => new GiftCardReportPoint(x.Date, x.Issued, x.Redeemed, x.IssuedCount, x.RedemptionCount)).ToList();
    }

    public async Task<int> ExpireDueAsync(CancellationToken ct = default)
    {
        var config = await db.GiftCardConfigurations.SingleOrDefaultAsync(ct);
        if (config is null || !config.IsEnabled) return 0;
        var now = clock.UtcNow;
        var cards = await db.GiftCards.Where(x => x.Status == GiftCardStatus.Active && x.ExpiresAtUtc != null && x.ExpiresAtUtc <= now).ToListAsync(ct);
        foreach (var card in cards)
        {
            var balance = card.EvaluateExpiration(now);
            if (balance > 0 && !await db.GiftCardTransactions.AnyAsync(x => x.GiftCardId == card.Id && x.Type == GiftCardTransactionType.Expired, ct))
                db.GiftCardTransactions.Add(new GiftCardTransaction(Guid.NewGuid(), TenantId(), card.Id, GiftCardTransactionType.Expired, 0, balance, balance, SystemUserId, now));
        }
        if (cards.Count > 0) { await db.SaveChangesAsync(ct); foreach (var card in cards) { try { await wallets.SynchronizeAsync(card.Id, ct); } catch { } try { await appleWallets.SynchronizeAsync(card.Id, ct); } catch { } } }
        return cards.Count;
    }

    private async Task<GiftCardOperationResult> MutateAsync(
        string key, GiftCardTransactionType operation, decimal amount,
        Func<Task<GiftCard?>> load, Func<GiftCard, DateTime, GiftCardTransaction> mutation,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
            return new(false, "Idempotency key requerida.", null, Failure: GiftCardFailure.InvalidInput);

        // Resolve within the authenticated tenant before examining a replay.
        var card = await load();
        if (card is null)
            return new(false, "Tarjeta de regalo no encontrada.", null, Failure: GiftCardFailure.NotFound);

        var replay = await ReplayAsync(key, card.Id, operation, amount, ct);
        if (replay is not null) return replay;

        // Interactive Server may retain this context between operations.
        await db.Entry(card).ReloadAsync(ct);
        try
        {
            var transaction = mutation(card, clock.UtcNow);
            db.GiftCardTransactions.Add(transaction);
            await db.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            var failure = ex.Message switch
            {
                "La Gift Card no está activa." => card.Status == GiftCardStatus.Expired
                    ? GiftCardFailure.Expired : GiftCardFailure.Inactive,
                "Saldo insuficiente o monto inválido." => GiftCardFailure.InsufficientBalance,
                "Esta Gift Card solo permite canje completo." => GiftCardFailure.PartialRedemptionNotAllowed,
                _ => GiftCardFailure.InvalidInput
            };
            db.ChangeTracker.Clear();
            // The other request may have committed between the initial check and reload.
            replay = await ReplayAsync(key, card.Id, operation, amount, ct);
            return replay ?? new(false, ex.Message, null, Failure: failure);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            replay = await ReplayAsync(key, card.Id, operation, amount, ct);
            return replay ?? new(false,
                "La tarjeta de regalo cambió durante el canje. Consulta el saldo e inténtalo nuevamente.",
                null, Failure: GiftCardFailure.ConcurrencyConflict);
        }
        catch (DbUpdateException)
        {
            // A unique-key race rolls back this SaveChanges, including its balance update.
            db.ChangeTracker.Clear();
            replay = await ReplayAsync(key, card.Id, operation, amount, ct);
            if (replay is null) throw;
            return replay;
        }

        try { await wallets.SynchronizeAsync(card.Id, ct); } catch { /* Value movement is committed. */ }
        try { await appleWallets.SynchronizeAsync(card.Id, ct); } catch { /* Value movement is committed. */ }
        return new(true, null, await DetailAsync(card, ct),
            RedeemedAmount: operation == GiftCardTransactionType.Redeemed ? -amount : null);
    }

    private async Task<GiftCardOperationResult?> ReplayAsync(
        string key, Guid cardId, GiftCardTransactionType operation, decimal amount, CancellationToken ct)
    {
        var transaction = await db.GiftCardTransactions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
        if (transaction is null) return null;
        if (transaction.GiftCardId != cardId || transaction.Type != operation || transaction.Amount != amount)
            return new(false, "La clave de idempotencia ya corresponde a otra operación.", null,
                Failure: GiftCardFailure.IdempotencyConflict);

        var current = await db.GiftCards.AsNoTracking().SingleAsync(x => x.Id == cardId, ct);
        return new(true, null, await DetailAsync(current, ct), true,
            RedeemedAmount: operation == GiftCardTransactionType.Redeemed ? -transaction.Amount : null);
    }

    private async Task<IssuedGiftCardDto?> ReplayIssueAsync(
        string key,
        decimal amount,
        Guid? recipientMemberId,
        string recipientName,
        string? email,
        string? phone,
        string? senderName,
        string? personalMessage,
        GiftCardSource source,
        DateTime? explicitExpirationUtc,
        CancellationToken ct)
    {
        var transaction = await db.GiftCardTransactions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
        if (transaction is null) return null;
        if (transaction.Type != GiftCardTransactionType.Issued || transaction.Amount != amount)
            throw IssueIdempotencyConflict();

        var card = await db.GiftCards.SingleOrDefaultAsync(x => x.Id == transaction.GiftCardId, ct)
            ?? throw IssueIdempotencyConflict();

        var explicitExpirationMatches = explicitExpirationUtc is null || card.ExpiresAtUtc == explicitExpirationUtc;
        if (card.InitialValue != amount || card.RecipientMemberId != recipientMemberId
            || card.RecipientName != recipientName || card.RecipientEmail != email
            || card.RecipientPhone != phone || card.SenderName != senderName
            || card.PersonalMessage != personalMessage || card.Source != source
            || !explicitExpirationMatches)
            throw IssueIdempotencyConflict();

        var token = GenerateClaimToken();
        card.ReplaceClaimTokenHash(GiftCard.HashClaimToken(token), clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return new(ToDto(card), token);
    }

    private static string? NormalizeIdempotencyKey(string? value)
    {
        var key = value?.Trim();
        if (string.IsNullOrWhiteSpace(key)) return null;
        if (key.Length > 100) throw new ArgumentException("Idempotency key requerida.");
        return key;
    }

    private static InvalidOperationException IssueIdempotencyConflict() =>
        new("La clave de idempotencia ya corresponde a otra emisión.");

    private async Task<GiftCardConfiguration> ConfigurationAsync(bool create, CancellationToken ct) { var result = await db.GiftCardConfigurations.SingleOrDefaultAsync(ct); if (result is null && create) { result = new GiftCardConfiguration(Guid.NewGuid(), TenantId(), clock.UtcNow); db.GiftCardConfigurations.Add(result); await db.SaveChangesAsync(ct); } return result ?? throw new InvalidOperationException("El módulo de tarjetas de regalo no está configurado."); }
    private async Task<GiftCardConfiguration> EnabledConfigurationAsync(CancellationToken ct)
    {
        var result = await db.GiftCardConfigurations.SingleOrDefaultAsync(ct);
        if (result is null || !result.IsEnabled)
            throw new GiftCardUnavailableException("El módulo de tarjetas de regalo no está disponible.");
        return result;
    }
    private async Task<GiftCardSettingsDto> SettingsDtoAsync(GiftCardConfiguration c, CancellationToken ct) { var d = await db.GiftCardDenominations.AsNoTracking().OrderBy(x => x.Amount).Select(x => new GiftCardDenominationDto(x.Id, x.Amount, x.Currency, x.IsActive)).ToListAsync(ct); return new(c.Id,c.IsEnabled,c.AllowCustomAmount,c.AllowPartialRedemption,c.AllowPromotionalIssuance,c.ExpirationMode,c.DefaultExpirationMonths,c.Currency,c.DisplayName,c.PrimaryColor,c.TextColor,c.LogoUrl,c.SecondaryText,c.Terms,c.FooterMessage,d); }
    private async Task<GiftCardDetailDto> DetailAsync(GiftCard card, CancellationToken ct) { var tx = await db.GiftCardTransactions.AsNoTracking().Where(x => x.GiftCardId == card.Id).OrderByDescending(x => x.CreatedAtUtc).Select(x => new GiftCardTransactionDto(x.Id,x.Type,x.Amount,x.BalanceBefore,x.BalanceAfter,x.PerformedByUserId,x.CreatedAtUtc,x.Reference,x.Notes,x.IdempotencyKey)).ToListAsync(ct); return new(ToDto(card), tx); }
    private async Task PersistExpirationAsync(GiftCard card, CancellationToken ct) { var expired = card.EvaluateExpiration(clock.UtcNow); if (expired <= 0) return; var exists = await db.GiftCardTransactions.AnyAsync(x => x.GiftCardId == card.Id && x.Type == GiftCardTransactionType.Expired, ct); if (!exists) db.GiftCardTransactions.Add(new GiftCardTransaction(Guid.NewGuid(),TenantId(),card.Id,GiftCardTransactionType.Expired,0,expired,expired,UserId(),clock.UtcNow)); await db.SaveChangesAsync(ct); }
    private static DateTime? ResolveExpiration(GiftCardConfiguration c, DateTime? selected, DateTime now) => c.ExpirationMode switch { GiftCardExpirationMode.Never => null, GiftCardExpirationMode.MonthsAfterIssue => now.AddMonths(c.DefaultExpirationMonths!.Value), GiftCardExpirationMode.SelectAtIssue when selected is not null && selected > now => selected, GiftCardExpirationMode.SelectAtIssue => throw new InvalidOperationException("Selecciona una fecha de expiración futura."), _ => null };
    private async Task<string> UniqueCodeAsync(CancellationToken ct) { for (var i=0;i<10;i++) { var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)); var code = $"GC-{raw[..4]}-{raw[4..8]}-{raw[8..12]}"; if (!await db.GiftCards.AnyAsync(x => x.PublicCode == code,ct)) return code; } throw new InvalidOperationException("No se pudo generar un código único."); }
    private static string GenerateClaimToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static readonly Regex PhonePattern = new(@"^[0-9+().\-\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex HexColorPattern = new(@"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static void ValidateMoney(decimal value, string message)
    {
        if (value <= 0 || DecimalScale(value) > 2) throw new ArgumentException(message);
    }

    private static int DecimalScale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0x7F;
    private static bool ValidPhone(string value)
    {
        if (!PhonePattern.IsMatch(value)) return false;
        var digits = value.Count(char.IsDigit);
        return digits is >= 7 and <= 15;
    }

    private static string Required(string? value, int maxLength, string message)
    {
        var clean = value?.Trim();
        if (string.IsNullOrWhiteSpace(clean)) throw new ArgumentException(message);
        if (clean.Length > maxLength) throw new ArgumentException($"El valor no puede exceder {maxLength} caracteres.");
        return clean;
    }

    private static string? Optional(string? value, int maxLength)
    {
        var clean = value?.Trim();
        if (string.IsNullOrWhiteSpace(clean)) return null;
        if (clean.Length > maxLength) throw new ArgumentException($"El valor no puede exceder {maxLength} caracteres.");
        return clean;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateSettings(UpdateGiftCardSettingsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName)) throw new ArgumentException("Ingresa el nombre de la tarjeta de regalo.");
        if (request.DisplayName.Trim().Length > 100) throw new ArgumentException("El nombre no puede exceder 100 caracteres.");
        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3) throw new ArgumentException("Ingresa una moneda válida de tres letras.");
        if (!HexColorPattern.IsMatch(request.PrimaryColor?.Trim() ?? string.Empty) || !HexColorPattern.IsMatch(request.TextColor?.Trim() ?? string.Empty)) throw new ArgumentException("Los colores deben usar formato #RRGGBB.");
        if (request.ExpirationMode == GiftCardExpirationMode.MonthsAfterIssue && request.DefaultExpirationMonths is null or < 1 or > 120) throw new ArgumentException("Ingresa una vigencia entre 1 y 120 meses.");
        if (request.Terms?.Trim().Length > 2000) throw new ArgumentException("Los términos no pueden exceder 2000 caracteres.");
    }
    private Guid TenantId() => tenant.TenantId is { } id && id != Guid.Empty ? id : throw new InvalidOperationException("Tenant requerido.");
    private Guid UserId() => Guid.TryParse(user.UserId, out var id) && id != Guid.Empty ? id : throw new InvalidOperationException("Usuario autenticado requerido.");
    private static GiftCardDto ToDto(GiftCard c) => new(c.Id,c.PublicCode,c.InitialValue,c.CurrentBalance,c.Currency,c.Status,c.RecipientMemberId,c.RecipientName,c.RecipientEmail,c.RecipientPhone,c.SenderName,c.PersonalMessage,c.Source,c.IssuedAtUtc,c.ExpiresAtUtc,c.UpdatedAtUtc);
}
