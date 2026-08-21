using LoyaltyCloud.Domain.Common;

namespace LoyaltyCloud.Domain.Entities;

public sealed class SubscriptionPlan : Entity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Currency { get; private set; } = "MXN";
    public decimal MonthlyPrice { get; private set; }
    public decimal ThreeMonthPrice { get; private set; }
    public decimal SixMonthPrice { get; private set; }
    public decimal TwelveMonthPrice { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    private SubscriptionPlan() { }
    public SubscriptionPlan(Guid id, string code, string name, string currency, DateTime nowUtc) : base(id)
    { Code = code.Trim().ToLowerInvariant(); Name = name.Trim(); Currency = currency.Trim().ToUpperInvariant(); CreatedAt = nowUtc; }
    public void Update(string name, string currency, decimal p1, decimal p3, decimal p6, decimal p12, bool active, DateTime nowUtc)
    {
        if (new[] { p1, p3, p6, p12 }.Any(x => x < 0)) throw new ArgumentOutOfRangeException(nameof(p1));
        Name = name.Trim(); Currency = currency.Trim().ToUpperInvariant(); MonthlyPrice = p1; ThreeMonthPrice = p3;
        SixMonthPrice = p6; TwelveMonthPrice = p12; IsActive = active; UpdatedAt = nowUtc;
    }
    public decimal PriceFor(int months) => months switch { 1 => MonthlyPrice, 3 => ThreeMonthPrice, 6 => SixMonthPrice, 12 => TwelveMonthPrice, _ => throw new ArgumentException("Periodo inválido.") };
}
