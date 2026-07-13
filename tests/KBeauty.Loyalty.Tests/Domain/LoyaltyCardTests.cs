using System.Reflection;
using KBeauty.Loyalty.Common.Constants;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Domain.Events;
using KBeauty.Loyalty.Domain.Exceptions;
using KBeauty.Loyalty.Domain.ValueObjects;
using Moq;
using Xunit;

namespace KBeauty.Loyalty.Tests.Domain;

public class LoyaltyCardTests
{
    private static readonly DateTime Now = new(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc);

    private static readonly ProgramConfigSnapshot Config = new(
        PointsPerPesoUnit: 10m,
        WelcomeBonusPoints: 50,
        ReferralBonusPoints: 150,
        BirthdayMultiplier: 2,
        PointsExpirationEnabled: true,
        PointsExpireAfterMonths: 12,
        LevelMistMin: 0,
        LevelGlowMin: 1000,
        LevelRadianceMin: 3000,
        RadianceRequalificationPoints: 500,
        RewardMiniProductPoints: 300,
        RewardFiftyOffPoints: 500,
        RewardFocusSkinPoints: 400,
        RewardMonthlyProductPoints: 700,
        RewardHundredOffCabinaPoints: 800,
        RewardFacialOffPoints: 1200);

    private static Mock<IDateTimeProvider> MockClock(DateTime? now = null)
    {
        var mock = new Mock<IDateTimeProvider>();
        mock.Setup(d => d.UtcNow).Returns(now ?? Now);
        mock.Setup(d => d.Today).Returns((now ?? Now).Date);
        return mock;
    }

    private static LoyaltyCard NewCard() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "KB-TEST001", Now);

    // =========================================================================
    // EarnPoints
    // =========================================================================

    [Fact]
    public void EarnPoints_ShouldCalculateCorrectPoints_WhenPurchaseIsValid()
    {
        var card = NewCard();
        var dt = MockClock();

        card.EarnPoints(100, TransactionType.Purchase, Config, dt.Object);

        Assert.Equal(100, card.CurrentPoints);
        Assert.Equal(100, card.LifetimePoints);
        Assert.Equal(100, card.PointsEarnedThisYear);
    }

    [Fact]
    public void EarnPoints_ShouldStoreDoubledPoints_WhenCallerAlreadyAppliedBirthdayMultiplier()
    {
        // El multiplicador x2 lo aplica el handler; la entidad solo persiste lo que recibe.
        // Este test verifica el contrato: 100 base × 2 = 200 finales reflejados en saldos.
        var card = NewCard();
        var dt = MockClock();
        var doubled = 100 * Config.BirthdayMultiplier;

        card.EarnPoints(doubled, TransactionType.Purchase, Config, dt.Object);

        Assert.Equal(200, card.CurrentPoints);
    }

    [Fact]
    public void EarnPoints_ShouldUpgradeLevel_WhenThresholdIsCrossed()
    {
        var card = NewCard();
        var dt = MockClock();

        card.EarnPoints(950, TransactionType.Purchase, Config, dt.Object);
        Assert.Equal(LoyaltyConstants.Levels.Mist, card.Level);

        card.EarnPoints(100, TransactionType.Purchase, Config, dt.Object);

        Assert.Equal(LoyaltyConstants.Levels.Glow, card.Level);
        Assert.Equal(1050, card.CurrentPoints);
    }

    [Fact]
    public void EarnPoints_ShouldRaiseLevelUpgradedEvent_WhenLevelChanges()
    {
        var card = NewCard();
        var dt = MockClock();

        card.EarnPoints(1100, TransactionType.Purchase, Config, dt.Object);

        var upgradeEvent = Assert.Single(card.DomainEvents.OfType<LevelUpgradedEvent>());
        Assert.Equal(LoyaltyConstants.Levels.Mist, upgradeEvent.OldLevel);
        Assert.Equal(LoyaltyConstants.Levels.Glow, upgradeEvent.NewLevel);

        var pointsEvent = Assert.Single(card.DomainEvents.OfType<PointsEarnedEvent>());
        Assert.True(pointsEvent.LevelChanged);
        Assert.Equal(1100, pointsEvent.PointsAdded);
        Assert.Equal(1100, pointsEvent.NewTotal);
    }

    [Fact]
    public void EarnPoints_ShouldNotRaiseLevelUpgraded_WhenStayingInSameLevel()
    {
        var card = NewCard();
        var dt = MockClock();

        card.EarnPoints(200, TransactionType.Purchase, Config, dt.Object);

        Assert.Empty(card.DomainEvents.OfType<LevelUpgradedEvent>());
        var pointsEvent = Assert.Single(card.DomainEvents.OfType<PointsEarnedEvent>());
        Assert.False(pointsEvent.LevelChanged);
    }

    [Fact]
    public void EarnPoints_ShouldThrow_WhenPointsAreZeroOrNegative()
    {
        var card = NewCard();
        var dt = MockClock();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            card.EarnPoints(0, TransactionType.Purchase, Config, dt.Object));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            card.EarnPoints(-10, TransactionType.Purchase, Config, dt.Object));
    }

    [Fact]
    public void EarnPoints_ShouldRejectRedemptionType()
    {
        var card = NewCard();
        var dt = MockClock();

        Assert.Throws<ArgumentException>(() =>
            card.EarnPoints(100, TransactionType.Redemption, Config, dt.Object));
    }

    // =========================================================================
    // RedeemPoints
    // =========================================================================

    [Fact]
    public void RedeemPoints_ShouldThrow_WhenBalanceLow()
    {
        var card = NewCard();
        // CurrentPoints starts at 0
        var ex = Assert.Throws<InsufficientPointsException>(() => card.RedeemPoints(100));
        Assert.Equal(100, ex.Required);
        Assert.Equal(0, ex.Available);
    }

    [Fact]
    public void RedeemPoints_ShouldDecrement_WhenBalanceSufficient()
    {
        var card = NewCard();
        var dt = MockClock();
        card.EarnPoints(500, TransactionType.Purchase, Config, dt.Object);

        card.RedeemPoints(300);

        Assert.Equal(200, card.CurrentPoints);
        // LifetimePoints no decrece
        Assert.Equal(500, card.LifetimePoints);
    }

    // =========================================================================
    // NeedsLevelRequalification
    // =========================================================================

    [Fact]
    public void NeedsLevelRequalification_ShouldReturnFalse_WhenNotRadiance()
    {
        var card = NewCard(); // Mist
        var dt = MockClock();

        Assert.False(card.NeedsLevelRequalification(dt.Object));
    }

    [Fact]
    public void NeedsLevelRequalification_ShouldReturnFalse_WhenWithinFirstYear()
    {
        var card = NewCard();
        var dt = MockClock();
        card.EarnPoints(3000, TransactionType.Purchase, Config, dt.Object); // → Radiance

        // No avanzamos el reloj — sigue en "now"
        Assert.False(card.NeedsLevelRequalification(dt.Object));
    }

    [Fact]
    public void NeedsLevelRequalification_ShouldReturnTrue_WhenRadianceAndInactive()
    {
        // Construye estado: Radiance hace >1 año + bajo en puntos del año.
        // Se necesita reflexión porque las propiedades son private set y nuestra API
        // pública no permite simular "el reset anual" — eso lo hace un job externo.
        var card = NewCard();
        var dt = MockClock();
        card.EarnPoints(3000, TransactionType.Purchase, Config, dt.Object);

        SetPrivate(card, nameof(LoyaltyCard.LevelAchievedAt), Now.AddYears(-2));
        SetPrivate(card, nameof(LoyaltyCard.PointsEarnedThisYear), 100);

        Assert.True(card.NeedsLevelRequalification(dt.Object, requiredPointsPerYear: 500));
    }

    [Fact]
    public void NeedsLevelRequalification_ShouldReturnFalse_WhenRadianceAndActive()
    {
        var card = NewCard();
        var dt = MockClock();
        card.EarnPoints(3000, TransactionType.Purchase, Config, dt.Object);

        SetPrivate(card, nameof(LoyaltyCard.LevelAchievedAt), Now.AddYears(-2));
        SetPrivate(card, nameof(LoyaltyCard.PointsEarnedThisYear), 600);

        Assert.False(card.NeedsLevelRequalification(dt.Object, requiredPointsPerYear: 500));
    }

    /// <summary>
    /// Setter privado vía reflexión — solo usado en pruebas para construir estados
    /// que la API pública no permite (simular "ya pasó un año").
    /// </summary>
    private static void SetPrivate(object target, string propertyName, object value)
    {
        var prop = target.GetType().GetProperty(propertyName,
            BindingFlags.Instance | BindingFlags.Public);
        var setter = prop?.GetSetMethod(nonPublic: true)
            ?? throw new InvalidOperationException($"No private setter for {propertyName}.");
        setter.Invoke(target, new[] { value });
    }
}
