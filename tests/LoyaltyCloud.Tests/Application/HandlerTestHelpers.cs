using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Domain.ValueObjects;
using Moq;

namespace LoyaltyCloud.Tests.Application;

/// <summary>Helpers compartidos por los tests de handlers de Application.</summary>
internal static class HandlerTestHelpers
{
    public static readonly DateTime Now = new(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc);
    public static readonly Guid KBeautyTenantId = Guid.Parse("b1000000-0000-0000-0000-000000000001");

    public static Mock<IDateTimeProvider> Clock(DateTime? now = null)
    {
        var mock = new Mock<IDateTimeProvider>();
        mock.Setup(d => d.UtcNow).Returns(now ?? Now);
        mock.Setup(d => d.Today).Returns((now ?? Now).Date);
        return mock;
    }

    /// <summary>
    /// Mock de IProgramConfigRepository que devuelve filas vacías — el snapshot
    /// caerá a los valores default (definidos en LoyaltyConstants.Defaults).
    /// </summary>
    public static Mock<IProgramConfigRepository> ConfigRepoWithDefaults()
    {
        var mock = new Mock<IProgramConfigRepository>();
        mock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ProgramConfig>());
        return mock;
    }

    public static Mock<IUnitOfWork> NoOpUnitOfWork()
    {
        var mock = new Mock<IUnitOfWork>();
        mock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return mock;
    }

    public static Mock<ITenantContext> TenantContext()
    {
        var mock = new Mock<ITenantContext>();
        mock.Setup(t => t.TenantId).Returns(KBeautyTenantId);
        mock.Setup(t => t.TenantSlug).Returns("kbeauty");
        mock.Setup(t => t.HasTenant).Returns(true);
        return mock;
    }

    public static Mock<ILevelCalculationService> LevelCalculator()
    {
        var mock = new Mock<ILevelCalculationService>();
        mock.Setup(s => s.CalculateLevel(It.IsAny<int>(), It.IsAny<IReadOnlyList<TenantLoyaltyLevelDto>>()))
            .Returns<int, IReadOnlyList<TenantLoyaltyLevelDto>>((points, levels) => CalculateLevel(points, levels));
        mock.Setup(s => s.IsEligibleForLevelProgress(It.IsAny<TransactionType>()))
            .Returns<TransactionType>(LevelProgressTransactionTypes.Contains);
        mock.Setup(s => s.CompareLevels(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<TenantLoyaltyLevelDto>>()))
            .Returns<string, string, IReadOnlyList<TenantLoyaltyLevelDto>>((current, next, levels) =>
                Rank(next, levels).CompareTo(Rank(current, levels)));
        return mock;
    }

    public static Mock<ITenantLoyaltyLevelReadService> TenantLevels(
        IReadOnlyList<TenantLoyaltyLevelDto>? levels = null)
    {
        var mock = new Mock<ITenantLoyaltyLevelReadService>();
        mock.Setup(s => s.GetActiveLevelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(levels ?? DefaultTenantLevels());
        return mock;
    }

    public static IReadOnlyList<TenantLoyaltyLevelDto> DefaultTenantLevels() =>
    [
        new(Guid.Parse("b1000000-0000-0000-0000-000000000101"), LoyaltyCloud.Common.Constants.LoyaltyConstants.Levels.Mist, 0, 1),
        new(Guid.Parse("b1000000-0000-0000-0000-000000000102"), LoyaltyCloud.Common.Constants.LoyaltyConstants.Levels.Glow, 1000, 2),
        new(Guid.Parse("b1000000-0000-0000-0000-000000000103"), LoyaltyCloud.Common.Constants.LoyaltyConstants.Levels.Radiance, 3000, 3)
    ];

    private static MemberLevel CalculateLevel(int points, IReadOnlyList<TenantLoyaltyLevelDto> levels)
    {
        var ordered = levels.OrderBy(level => level.SortOrder).ToList();
        var selectedIndex = 0;
        for (var i = 0; i < ordered.Count; i++)
        {
            if (points >= ordered[i].Threshold)
                selectedIndex = i;
        }

        var selected = ordered[selectedIndex];
        var maxPoints = selectedIndex == ordered.Count - 1 ? int.MaxValue : ordered[selectedIndex + 1].Threshold - 1;
        return new MemberLevel(selected.Id, selected.Name, selected.Threshold, maxPoints, selected.SortOrder);
    }

    private static int Rank(string level, IReadOnlyList<TenantLoyaltyLevelDto> levels)
    {
        var match = levels.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, level, StringComparison.OrdinalIgnoreCase));
        return match?.SortOrder ?? 0;
    }

    public static Customer NewCustomer(string fullName = "Ana López", DateTime? dob = null) =>
        new(Guid.NewGuid(),
            KBeautyTenantId,
            fullName,
            email: $"{fullName.Replace(" ", ".").ToLowerInvariant()}@test.com",
            dateOfBirth: dob ?? new DateTime(1990, 3, 1),
            createdAtUtc: Now);

    public static LoyaltyCard NewCard(Guid? customerId = null, string? serial = null) =>
        new(Guid.NewGuid(), KBeautyTenantId, customerId ?? Guid.NewGuid(), serial ?? "KB-TEST001", Now);
}
