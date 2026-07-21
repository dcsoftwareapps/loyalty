using LoyaltyCloud.Common.Services;

namespace LoyaltyCloud.Infrastructure.Services;

/// <summary>
/// Implementación trivial de <see cref="IDateTimeProvider"/> envolviendo
/// <see cref="DateTime.UtcNow"/>. Existe (en vez de usar DateTime directo)
/// para que los tests inyecten relojes mockeados.
/// </summary>
internal sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Today => DateTime.UtcNow.Date;
}
