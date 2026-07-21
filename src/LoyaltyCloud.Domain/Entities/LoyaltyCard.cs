using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Common;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Events;
using LoyaltyCloud.Domain.Exceptions;
using LoyaltyCloud.Domain.ValueObjects;

namespace LoyaltyCloud.Domain.Entities;

/// <summary>
/// Tarjeta de lealtad asociada 1:1 a una <see cref="Customer"/>.
/// Es el agregado central del dominio — encapsula el saldo, el nivel,
/// la lógica de acumulación y los domain events de cambio de nivel.
/// </summary>
public class LoyaltyCard : Entity
{
    /// <summary>FK a la clienta dueña de esta tarjeta.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Identificador imprimible/escaneable (ej: <c>KB-A7B9C2X</c>) — único.</summary>
    public string SerialNumber { get; private set; } = string.Empty;

    /// <summary>Saldo de puntos disponible para canjear.</summary>
    public int CurrentPoints { get; private set; }

    /// <summary>Puntos acumulados de por vida — nunca decrece. Métrica para reportes.</summary>
    public int LifetimePoints { get; private set; }

    /// <summary>Nombre del nivel actual: Mist / Glow / Radiance.</summary>
    public string Level { get; private set; } = LoyaltyConstants.Levels.Mist;

    /// <summary>Puntos ganados desde la fecha aniversario actual del nivel (re-cualificación Radiance).</summary>
    public int PointsEarnedThisYear { get; private set; }

    /// <summary>Fecha (UTC) en que se alcanzó el nivel actual. Sirve de ancla para el "año" de re-cualificación.</summary>
    public DateTime LevelAchievedAt { get; private set; }

    /// <summary>Última fecha (UTC) con cualquier actividad — usada para identificar tarjetas inactivas.</summary>
    public DateTime LastActivityAt { get; private set; }

    /// <summary>Permite desactivar la tarjeta sin perder historial.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Token requerido por Apple Wallet para autenticar las requests del pase.</summary>
    public string AuthenticationToken { get; private set; } = string.Empty;

    private LoyaltyCard() { }

    public LoyaltyCard(Guid id, Guid customerId, string serialNumber, DateTime nowUtc) : base(id)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId requerido.", nameof(customerId));
        if (string.IsNullOrWhiteSpace(serialNumber))
            throw new ArgumentException("Serial requerido.", nameof(serialNumber));

        CustomerId = customerId;
        SerialNumber = serialNumber.Trim().ToUpperInvariant();
        CurrentPoints = 0;
        LifetimePoints = 0;
        PointsEarnedThisYear = 0;
        Level = LoyaltyConstants.Levels.Mist;
        LevelAchievedAt = nowUtc;
        LastActivityAt = nowUtc;
        IsActive = true;

        // Token estable — Apple lo enviará en Authorization: ApplePass <token> en cada request.
        AuthenticationToken = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Suma puntos a la tarjeta y emite <see cref="PointsEarnedEvent"/>.
    /// El nivel se aplica aparte con la ventana movil de 12 meses.
    /// </summary>
    /// <param name="points">Cantidad de puntos a sumar — debe ser &gt; 0.</param>
    /// <param name="type">Naturaleza del movimiento (Purchase, BonusXxx).</param>
    /// <param name="config">Snapshot tipado de la configuración del programa.</param>
    /// <param name="dt">Reloj inyectable (se usa para LastActivityAt y LevelAchievedAt).</param>
    public void EarnPoints(int points, TransactionType type, ProgramConfigSnapshot config, IDateTimeProvider dt)
    {
        if (points <= 0)
            throw new ArgumentOutOfRangeException(nameof(points), "Los puntos a sumar deben ser positivos.");
        if (type == TransactionType.Redemption || type == TransactionType.Expiry || type == TransactionType.Expired)
            throw new ArgumentException("EarnPoints no admite tipos que restan saldo.", nameof(type));
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(dt);

        var now = dt.UtcNow;
        CurrentPoints += points;
        LifetimePoints += points;
        PointsEarnedThisYear += points;
        LastActivityAt = now;

        AddDomainEvent(new PointsEarnedEvent(Id, points, CurrentPoints, false, Level));
    }

    /// <summary>
    /// Aplica el nivel calculado por ventana movil de 12 meses.
    /// Actualiza LevelAchievedAt y LastActivityAt solo si el nivel cambia.
    /// </summary>
    public bool ApplyCalculatedLevel(MemberLevel calculatedLevel, IDateTimeProvider dt)
    {
        ArgumentNullException.ThrowIfNull(calculatedLevel);
        ArgumentNullException.ThrowIfNull(dt);

        if (string.Equals(Level, calculatedLevel.Name, StringComparison.Ordinal))
            return false;

        var oldLevel = Level;
        var now = dt.UtcNow;
        Level = calculatedLevel.Name;
        LevelAchievedAt = now;
        LastActivityAt = now;

        AddDomainEvent(new LevelUpgradedEvent(Id, oldLevel, calculatedLevel.Name));
        return true;
    }

    /// <summary>
    /// Descuenta puntos por un canje. Lanza <see cref="InsufficientPointsException"/>
    /// si el saldo no alcanza — el validator de Application debe prevenirlo antes.
    /// </summary>
    public void RedeemPoints(int points)
    {
        if (points <= 0)
            throw new ArgumentOutOfRangeException(nameof(points), "Los puntos a canjear deben ser positivos.");
        if (CurrentPoints < points)
            throw new InsufficientPointsException(points, CurrentPoints);

        CurrentPoints -= points;
    }

    /// <summary>
    /// Restaura puntos por cancelacion de canje. No afecta metricas de acumulacion
    /// como LifetimePoints ni PointsEarnedThisYear.
    /// </summary>
    public void RestorePoints(int points, IDateTimeProvider dt)
    {
        if (points <= 0)
            throw new ArgumentOutOfRangeException(nameof(points), "Los puntos a restaurar deben ser positivos.");
        ArgumentNullException.ThrowIfNull(dt);

        CurrentPoints += points;
        LastActivityAt = dt.UtcNow;
    }

    /// <summary>
    /// Expira puntos disponibles. No afecta LifetimePoints ni PointsEarnedThisYear.
    /// La expiracion no modifica directamente el progreso de nivel.
    /// </summary>
    public void ExpirePoints(int points, IDateTimeProvider dt)
    {
        if (points <= 0)
            throw new ArgumentOutOfRangeException(nameof(points), "Los puntos a expirar deben ser positivos.");
        if (CurrentPoints < points)
            throw new InsufficientPointsException(points, CurrentPoints);
        ArgumentNullException.ThrowIfNull(dt);

        CurrentPoints -= points;
        LastActivityAt = dt.UtcNow;
    }

    /// <summary>
    /// Indica si la tarjeta es Radiance y no acumuló suficientes puntos en su año
    /// vigente para mantener el nivel.
    /// </summary>
    /// <param name="dt">Reloj inyectable.</param>
    /// <param name="requiredPointsPerYear">Mínimo anual (default toma de Defaults).</param>
    public bool NeedsLevelRequalification(
        IDateTimeProvider dt,
        int requiredPointsPerYear = LoyaltyConstants.Defaults.RadianceRequalificationPoints)
    {
        // TODO Phase 3.x: remove this legacy annual requalification path after all callers use rolling PointTransaction sums.
        ArgumentNullException.ThrowIfNull(dt);

        if (!string.Equals(Level, LoyaltyConstants.Levels.Radiance, StringComparison.Ordinal))
            return false;

        // Solo evaluamos pasado el aniversario; antes de cumplir un año todavía no aplica.
        var anniversary = LevelAchievedAt.AddYears(1);
        if (dt.UtcNow < anniversary)
            return false;

        return PointsEarnedThisYear < requiredPointsPerYear;
    }

    /// <summary>Marca actividad sin movimiento de puntos (ej: la clienta abrió el pase).</summary>
    public void Touch(IDateTimeProvider dt)
    {
        ArgumentNullException.ThrowIfNull(dt);
        LastActivityAt = dt.UtcNow;
    }

    /// <summary>Desactiva la tarjeta. No afecta el historial.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Rota el token de autenticación de Apple Pass.</summary>
    public void RegenerateAuthenticationToken() =>
        AuthenticationToken = Guid.NewGuid().ToString("N");
}
