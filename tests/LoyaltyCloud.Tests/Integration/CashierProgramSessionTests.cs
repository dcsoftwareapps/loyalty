using System.Text.Json;
using LoyaltyCloud.Cashier.Services;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "StampCashier")]
public sealed class CashierProgramSessionTests
{
    [Theory]
    [InlineData("Points")]
    [InlineData("Stamps")]
    public async Task Session_round_trip_preserves_program_and_identity(string program)
    {
        var store = new Store();
        var session = new CashierSession("test-token", "Bearer", DateTimeOffset.UtcNow.AddHours(1), "testtenant", Guid.NewGuid(), "operator", "Cashier", program);
        await new CashierSessionService(store).SignInAsync(session);
        var restored = new CashierSessionService(store);
        var recovered = await restored.RestoreAsync();
        Assert.Equal(session.AccessToken, recovered!.AccessToken);
        Assert.Equal(program, recovered.ProgramType);
        Assert.False(string.IsNullOrWhiteSpace(recovered.SessionId));
        await restored.LogoutAsync();
        Assert.Null(await new CashierSessionService(store).RestoreAsync());
    }

    [Fact]
    public async Task Legacy_session_without_program_requires_new_login_instead_of_assuming_points()
    {
        var store = new Store();
        await store.SetAsync("loyaltycloud.cashier.session.v1", JsonSerializer.Serialize(new
        {
            accessToken = "test-token", expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1), tenantSlug = "testtenant"
        }));
        Assert.Null(await new CashierSessionService(store).RestoreAsync());
        Assert.Null(await store.GetAsync("loyaltycloud.cashier.session.v1"));
    }

    private sealed class Store : ISecureTokenStore
    {
        private readonly Dictionary<string, string> values = [];
        public Task<string?> GetAsync(string key) => Task.FromResult(values.GetValueOrDefault(key));
        public Task SetAsync(string key, string value) { values[key] = value; return Task.CompletedTask; }
        public void Remove(string key) => values.Remove(key);
    }
}
