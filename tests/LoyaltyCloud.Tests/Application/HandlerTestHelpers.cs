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

    public static Mock<ILevelCalculationService> LevelCalculator()
    {
        var mock = new Mock<ILevelCalculationService>();
        mock.Setup(s => s.CalculateLevel(It.IsAny<int>(), It.IsAny<ProgramConfigSnapshot>()))
            .Returns<int, ProgramConfigSnapshot>((points, config) => MemberLevel.FromPoints(points, config));
        mock.Setup(s => s.IsEligibleForLevelProgress(It.IsAny<TransactionType>()))
            .Returns<TransactionType>(LevelProgressTransactionTypes.Contains);
        mock.Setup(s => s.CompareLevels(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ProgramConfigSnapshot>()))
            .Returns<string, string, ProgramConfigSnapshot>((current, next, config) =>
                Rank(next, config).CompareTo(Rank(current, config)));
        return mock;
    }

    private static int Rank(string level, ProgramConfigSnapshot config)
    {
        if (string.Equals(level, LoyaltyCloud.Common.Constants.LoyaltyConstants.Levels.Radiance, StringComparison.OrdinalIgnoreCase))
            return config.LevelRadianceMin;
        if (string.Equals(level, LoyaltyCloud.Common.Constants.LoyaltyConstants.Levels.Glow, StringComparison.OrdinalIgnoreCase))
            return config.LevelGlowMin;

        return config.LevelMistMin;
    }

    public static Customer NewCustomer(string fullName = "Ana López", DateTime? dob = null) =>
        new(Guid.NewGuid(),
            fullName,
            email: $"{fullName.Replace(" ", ".").ToLowerInvariant()}@test.com",
            dateOfBirth: dob ?? new DateTime(1990, 3, 1),
            createdAtUtc: Now);

    public static LoyaltyCard NewCard(Guid? customerId = null, string? serial = null) =>
        new(Guid.NewGuid(), customerId ?? Guid.NewGuid(), serial ?? "KB-TEST001", Now);
}
