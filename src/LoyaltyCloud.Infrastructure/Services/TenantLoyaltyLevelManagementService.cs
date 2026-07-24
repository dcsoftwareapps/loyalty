using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Levels;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.ValueObjects;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class TenantLoyaltyLevelManagementService : ITenantLoyaltyLevelManagementService
{
    private static readonly HashSet<string> SpecialCampaignLevelValues = new(StringComparer.OrdinalIgnoreCase)
    {
        PointCampaign.CampaignLevelEligibilityAll
    };

    private static readonly HashSet<string> SpecialAudienceValues = new(StringComparer.OrdinalIgnoreCase)
    {
        CustomNotificationCampaign.AudienceAllWalletUsers,
        CustomNotificationCampaign.AudienceMinimumPoints,
        CustomNotificationCampaign.AudiencePointsExpiring,
        nameof(CustomNotificationAudienceType.MistAndAbove),
        nameof(CustomNotificationAudienceType.GlowAndAbove),
        nameof(CustomNotificationAudienceType.RadianceOnly)
    };

    private static readonly HashSet<string> ReservedLevelNames =
        SpecialCampaignLevelValues.Concat(SpecialAudienceValues).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILevelCalculationService _levelCalculation;
    private readonly IDateTimeProvider _dt;
    private readonly IApnService _apn;
    private readonly ILogger<TenantLoyaltyLevelManagementService> _logger;

    public TenantLoyaltyLevelManagementService(
        AppDbContext db,
        ITenantContext tenantContext,
        ILevelCalculationService levelCalculation,
        IDateTimeProvider dt,
        IApnService apn,
        ILogger<TenantLoyaltyLevelManagementService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _levelCalculation = levelCalculation;
        _dt = dt;
        _apn = apn;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<TenantLoyaltyLevelAdminDto>>> ListAsync(CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var levels = await _db.TenantLoyaltyLevels
            .AsNoTracking()
            .Where(level => level.TenantId == tenantId && level.IsActive)
            .OrderBy(level => level.SortOrder)
            .Select(level => ToDto(level))
            .ToListAsync(ct);

        return Result.Ok<IReadOnlyList<TenantLoyaltyLevelAdminDto>>(levels.AsReadOnly());
    }

    public async Task<Result<UpdateTenantLoyaltyLevelsResultDto>> UpdateAsync(
        IReadOnlyList<TenantLoyaltyLevelUpdateItemDto> levels,
        string operatorId,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var now = _dt.UtcNow;
        var validationError = Validate(levels);
        if (validationError is not null)
            return Result.Fail<UpdateTenantLoyaltyLevelsResultDto>(validationError);

        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

        var currentLevels = await _db.TenantLoyaltyLevels
            .Where(level => level.TenantId == tenantId && level.IsActive)
            .OrderBy(level => level.SortOrder)
            .ToListAsync(ct);
        var currentById = currentLevels.ToDictionary(level => level.Id);
        var submittedIds = levels.Where(level => level.Id.HasValue).Select(level => level.Id!.Value).ToHashSet();

        if (submittedIds.Any(id => !currentById.ContainsKey(id)))
            return Result.Fail<UpdateTenantLoyaltyLevelsResultDto>("La lista contiene un nivel que no pertenece al tenant actual.");

        var removedLevels = currentLevels.Where(level => !submittedIds.Contains(level.Id)).ToList();
        var deleteBlockers = await FindDeleteBlockersAsync(removedLevels, tenantId, ct);
        if (deleteBlockers.Count > 0)
            return Result.Fail<UpdateTenantLoyaltyLevelsResultDto>(BuildDeleteBlockedMessage(deleteBlockers));

        var oldLevelRanks = currentLevels.ToDictionary(level => level.Name, level => level.SortOrder, StringComparer.OrdinalIgnoreCase);
        var renameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (removedLevels.Count > 0)
        {
            _db.TenantLoyaltyLevels.RemoveRange(removedLevels);
            await _db.SaveChangesAsync(ct);
        }

        for (var i = 0; i < levels.Count; i++)
        {
            var incoming = levels[i];
            var sortOrder = i + 1;
            if (incoming.Id.HasValue)
            {
                var existing = currentById[incoming.Id.Value];
                if (!string.Equals(existing.Name, incoming.Name.Trim(), StringComparison.Ordinal))
                    renameMap[existing.Name] = incoming.Name.Trim();

                existing.Update(incoming.Name, incoming.PointsRequired, sortOrder, isActive: true, now);
            }
            else
            {
                _db.TenantLoyaltyLevels.Add(new TenantLoyaltyLevel(
                    Guid.NewGuid(),
                    tenantId,
                    incoming.Name,
                    incoming.PointsRequired,
                    sortOrder,
                    now));
            }
        }

        await UpdateOperationalLevelReferencesAsync(renameMap, tenantId, ct);
        await _db.SaveChangesAsync(ct);

        var activeLevels = await _db.TenantLoyaltyLevels
            .AsNoTracking()
            .Where(level => level.TenantId == tenantId && level.IsActive)
            .OrderBy(level => level.SortOrder)
            .Select(level => new TenantLoyaltyLevelDto(level.Id, level.Name, level.Threshold, level.SortOrder))
            .ToListAsync(ct);
        var recalc = await RecalculateCardsAsync(activeLevels, oldLevelRanks, ct);

        await _db.SaveChangesAsync(ct);
        if (tx is not null)
            await tx.CommitAsync(ct);

        var walletsNotified = await NotifyWalletsAsync(recalc.ChangedSerials, recalc.Warnings, ct);

        var updatedLevels = activeLevels
            .Select(level => new TenantLoyaltyLevelAdminDto(level.Id, level.Name, level.Threshold, level.SortOrder, true))
            .ToList()
            .AsReadOnly();

        _logger.LogInformation(
            "Tenant loyalty levels updated. tenant={TenantId}, operator={OperatorId}, cardsReviewed={CardsReviewed}, cardsChanged={CardsChanged}, upgraded={CardsUpgraded}, downgraded={CardsDowngraded}, walletsNotified={WalletsNotified}.",
            tenantId,
            operatorId,
            recalc.CardsReviewed,
            recalc.CardsChanged,
            recalc.CardsUpgraded,
            recalc.CardsDowngraded,
            walletsNotified);

        return Result.Ok(new UpdateTenantLoyaltyLevelsResultDto(
            updatedLevels,
            recalc.CardsReviewed,
            recalc.CardsChanged,
            recalc.CardsUpgraded,
            recalc.CardsDowngraded,
            walletsNotified,
            recalc.Warnings.AsReadOnly()));
    }

    private async Task<RecalculationResult> RecalculateCardsAsync(
        IReadOnlyList<TenantLoyaltyLevelDto> newLevels,
        IReadOnlyDictionary<string, int> oldLevelRanks,
        CancellationToken ct)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var now = _dt.UtcNow;
        var windowStart = now.AddMonths(-12);
        var pointsByCard = await _db.PointTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId
                     && t.CreatedAt >= windowStart
                     && t.Points > 0
                     && LevelProgressTransactionTypes.All.Contains(t.Type))
            .GroupBy(t => t.LoyaltyCardId)
            .Select(g => new { LoyaltyCardId = g.Key, Points = g.Sum(t => t.Points) })
            .ToDictionaryAsync(x => x.LoyaltyCardId, x => x.Points, ct);

        var cards = await _db.LoyaltyCards
            .Where(card => card.TenantId == tenantId && card.IsActive)
            .OrderBy(card => card.SerialNumber)
            .ToListAsync(ct);

        var newLevelRanks = newLevels.ToDictionary(level => level.Name, level => level.SortOrder, StringComparer.OrdinalIgnoreCase);
        var result = new RecalculationResult(CardsReviewed: cards.Count);

        foreach (var card in cards)
        {
            var rollingPoints = pointsByCard.TryGetValue(card.Id, out var points) ? points : 0;
            var calculatedLevel = _levelCalculation.CalculateLevel(rollingPoints, newLevels);
            var oldRank = oldLevelRanks.TryGetValue(card.Level, out var rank)
                ? rank
                : newLevelRanks.GetValueOrDefault(card.Level, 0);

            if (string.Equals(card.Level, calculatedLevel.Name, StringComparison.Ordinal))
                continue;

            if (calculatedLevel.SortOrder > oldRank)
            {
                if (card.ApplyCalculatedLevel(calculatedLevel, _dt))
                {
                    result.CardsChanged++;
                    result.CardsUpgraded++;
                    result.ChangedSerials.Add(card.SerialNumber);
                }
            }
            else
            {
                var sameLogicalLevel = calculatedLevel.SortOrder == oldRank;
                if (card.ApplyConfiguredLevelSilently(calculatedLevel, _dt, updateLevelAchievedAt: !sameLogicalLevel))
                {
                    result.CardsChanged++;
                    if (calculatedLevel.SortOrder < oldRank)
                        result.CardsDowngraded++;
                    result.ChangedSerials.Add(card.SerialNumber);
                }
            }
        }

        return result;
    }

    private async Task<int> NotifyWalletsAsync(
        IEnumerable<string> serials,
        List<string> warnings,
        CancellationToken ct)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var sent = 0;
        foreach (var serial in serials.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var devices = await _db.DeviceRegistrations
                    .AsNoTracking()
                    .Where(device => device.TenantId == tenantId && device.SerialNumber == serial)
                    .ToListAsync(ct);

                foreach (var device in devices)
                {
                    await _apn.SendPassUpdateAsync(device.PushToken, PassUpdateReason.LevelChanged, ct);
                    sent++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fallo enviando push de Wallet por cambio de configuracion de nivel para serial {Serial}", serial);
                warnings.Add($"No se pudo notificar Wallet para serial {serial}.");
            }
        }

        return sent;
    }

    private async Task UpdateOperationalLevelReferencesAsync(
        IReadOnlyDictionary<string, string> renameMap,
        Guid tenantId,
        CancellationToken ct)
    {
        if (renameMap.Count == 0)
            return;
        var renamedLevelNames = renameMap.Keys.ToArray();

        var rewards = await _db.RewardCatalogItems
            .Where(reward => reward.TenantId == tenantId && renamedLevelNames.Contains(reward.MinLevel))
            .ToListAsync(ct);
        foreach (var reward in rewards)
        {
            var wasActive = reward.IsActive;
            reward.Update(reward.Name, reward.Description, reward.PointsCost, renameMap[reward.MinLevel], reward.IsMonthlyProduct, reward.ValidFrom, reward.ValidTo);
            if (wasActive)
                reward.Activate();
            else
                reward.Deactivate();
        }

        var campaigns = await _db.PointCampaigns
            .Where(campaign => campaign.TenantId == tenantId
                            && !SpecialCampaignLevelValues.Contains(campaign.LevelEligibility)
                            && renamedLevelNames.Contains(campaign.LevelEligibility))
            .ToListAsync(ct);
        foreach (var campaign in campaigns)
            campaign.Update(campaign.Name, campaign.Description, campaign.Multiplier, campaign.MinimumPurchaseAmount, renameMap[campaign.LevelEligibility], campaign.StartsAtUtc, campaign.EndsAtUtc, _dt.UtcNow);

        var customCampaigns = await _db.CustomNotificationCampaigns
            .Where(campaign => campaign.TenantId == tenantId
                            && !SpecialAudienceValues.Contains(campaign.AudienceType)
                            && renamedLevelNames.Contains(campaign.AudienceType))
            .ToListAsync(ct);
        foreach (var campaign in customCampaigns)
            campaign.UpdateAudienceType(renameMap[campaign.AudienceType], _dt.UtcNow);
    }

    private async Task<IReadOnlyDictionary<string, int>> FindDeleteBlockersAsync(
        IReadOnlyList<TenantLoyaltyLevel> removedLevels,
        Guid tenantId,
        CancellationToken ct)
    {
        var names = removedLevels.Select(level => level.Name).ToArray();
        var blockers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (names.Length == 0)
            return blockers;

        var rewards = await _db.RewardCatalogItems.CountAsync(
            reward => reward.TenantId == tenantId && names.Contains(reward.MinLevel),
            ct);
        if (rewards > 0)
            blockers["recompensa(s)"] = rewards;

        var campaigns = await _db.PointCampaigns.CountAsync(
            campaign => campaign.TenantId == tenantId
                     && !SpecialCampaignLevelValues.Contains(campaign.LevelEligibility)
                     && names.Contains(campaign.LevelEligibility),
            ct);
        if (campaigns > 0)
            blockers["campaña(s)"] = campaigns;

        var customCampaigns = await _db.CustomNotificationCampaigns.CountAsync(
            campaign => campaign.TenantId == tenantId
                     && !SpecialAudienceValues.Contains(campaign.AudienceType)
                     && names.Contains(campaign.AudienceType),
            ct);
        if (customCampaigns > 0)
            blockers["audiencia(s)"] = customCampaigns;

        return blockers;
    }

    private static string? Validate(IReadOnlyList<TenantLoyaltyLevelUpdateItemDto> levels)
    {
        if (levels.Count is < 3 or > 5)
            return "Debes configurar entre 3 y 5 niveles.";

        var duplicate = levels
            .GroupBy(level => level.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            return $"El nombre de nivel '{duplicate.Key}' está duplicado.";

        for (var i = 0; i < levels.Count; i++)
        {
            var level = levels[i];
            if (string.IsNullOrWhiteSpace(level.Name))
                return "Todos los niveles deben tener nombre.";
            var name = level.Name.Trim();
            if (name.Length > TenantLoyaltyLevel.NameMaxLength)
                return $"El nombre '{name}' excede {TenantLoyaltyLevel.NameMaxLength} caracteres.";
            if (ReservedLevelNames.Contains(name))
                return $"El nombre '{name}' está reservado y no puede usarse como nivel.";
            if (level.PointsRequired < 0)
                return "Los puntos necesarios no pueden ser negativos.";
            if (i == 0 && level.PointsRequired != 0)
                return "El primer nivel siempre debe iniciar en 0 puntos.";
            if (i > 0 && level.PointsRequired <= levels[i - 1].PointsRequired)
                return "Los puntos necesarios deben ser estrictamente ascendentes.";
        }

        return null;
    }

    private static string BuildDeleteBlockedMessage(IReadOnlyDictionary<string, int> blockers) =>
        "No puedes eliminar el nivel porque está utilizado por " +
        string.Join(", ", blockers.Select(blocker => $"{blocker.Value} {blocker.Key}")) +
        ".";

    private static TenantLoyaltyLevelAdminDto ToDto(TenantLoyaltyLevel level) =>
        new(level.Id, level.Name, level.Threshold, level.SortOrder, level.IsActive);

    private sealed record RecalculationResult(int CardsReviewed)
    {
        public int CardsChanged { get; set; }
        public int CardsUpgraded { get; set; }
        public int CardsDowngraded { get; set; }
        public List<string> ChangedSerials { get; } = [];
        public List<string> Warnings { get; } = [];
    }
}
