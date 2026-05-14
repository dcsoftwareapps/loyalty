using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Repositories;
using Moq;

namespace KBeauty.Loyalty.Tests.Application;

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

    public static Customer NewCustomer(string fullName = "Ana López", DateTime? dob = null) =>
        new(Guid.NewGuid(),
            fullName,
            email: $"{fullName.Replace(" ", ".").ToLowerInvariant()}@test.com",
            dateOfBirth: dob ?? new DateTime(1990, 3, 1),
            createdAtUtc: Now);

    public static LoyaltyCard NewCard(Guid? customerId = null, string? serial = null) =>
        new(Guid.NewGuid(), customerId ?? Guid.NewGuid(), serial ?? "KB-TEST001", Now);
}
