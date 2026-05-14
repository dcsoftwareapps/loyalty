namespace KBeauty.Loyalty.Common.Services;

/// <summary>
/// Abstracción del reloj del sistema. Se inyecta en lugar de usar
/// <see cref="DateTime.UtcNow"/> directamente para permitir mockear el tiempo
/// en tests (ej: bonos de cumpleaños, re-cualificación anual de Radiance).
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>Hora actual en UTC. Preferida para timestamps persistidos.</summary>
    DateTime UtcNow { get; }

    /// <summary>Fecha (sin hora) del día actual en UTC.</summary>
    DateTime Today { get; }
}
