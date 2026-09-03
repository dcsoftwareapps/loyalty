namespace LoyaltyCloud.Admin.Services;
public sealed class GiftCardFeatureState
{
    public event Func<bool, Task>? Changed;
    public async Task NotifyAsync(bool enabled)
    {
        if (Changed is null) return;
        foreach (var handler in Changed.GetInvocationList().Cast<Func<bool, Task>>()) await handler(enabled);
    }
}
