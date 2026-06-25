using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KBeauty.Loyalty.Tests.Integration;

public sealed class AppleWalletWebServiceTests : IntegrationTestBase
{
    private const string PassTypeIdentifier = "pass.com.kbeautymx.loyalty";

    public AppleWalletWebServiceTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task RegistrationList_WithoutAuthorization_ReturnsAppleUpdatePayload()
    {
        var testPass = await SeedRegisteredPassAsync();

        var response = await Client.GetAsync(
            $"/v1/devices/{testPass.DeviceLibraryIdentifier}/registrations/{PassTypeIdentifier}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        Assert.Contains(
            testPass.SerialNumber,
            root.GetProperty("serialNumbers").EnumerateArray().Select(item => item.GetString()));
        Assert.True(root.GetProperty("lastUpdated").ValueKind == JsonValueKind.String);
        Assert.True(long.TryParse(root.GetProperty("lastUpdated").GetString(), out _));
    }

    [Fact]
    public async Task RegistrationList_WithAssociatedAuthorization_ReturnsOk()
    {
        var testPass = await SeedRegisteredPassAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/v1/devices/{testPass.DeviceLibraryIdentifier}/registrations/{PassTypeIdentifier}");
        request.Headers.TryAddWithoutValidation("Authorization", $"ApplePass {testPass.AuthenticationToken}");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RegistrationList_WithMalformedAuthorization_ReturnsUnauthorized()
    {
        var testPass = await SeedRegisteredPassAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/v1/devices/{testPass.DeviceLibraryIdentifier}/registrations/{PassTypeIdentifier}");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer invalid");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RegistrationList_WithUnmatchedButWellFormedAuthorization_DoesNotBlockWallet()
    {
        var testPass = await SeedRegisteredPassAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/v1/devices/{testPass.DeviceLibraryIdentifier}/registrations/{PassTypeIdentifier}");
        request.Headers.TryAddWithoutValidation(
            "Authorization",
            $"ApplePass {Guid.NewGuid():N}");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PassEndpoint_StillRequiresMatchingApplePassAuthorization()
    {
        var testPass = await SeedRegisteredPassAsync();
        var url = $"/v1/passes/{PassTypeIdentifier}/{testPass.SerialNumber}";

        var unauthorized = await Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", $"ApplePass {testPass.AuthenticationToken}");

        var authorized = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        Assert.Equal("application/vnd.apple.pkpass", authorized.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnknownPassType_ReturnsNotFound()
    {
        var testPass = await SeedRegisteredPassAsync();

        var response = await Client.GetAsync(
            $"/v1/devices/{testPass.DeviceLibraryIdentifier}/registrations/pass.com.other");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RegisterAndUnregisterDevice_StillRequireMatchingAuthorization()
    {
        var testPass = await SeedRegisteredPassAsync(registerDevice: false);
        var registrationUrl =
            $"/v1/devices/{testPass.DeviceLibraryIdentifier}/registrations/{PassTypeIdentifier}/{testPass.SerialNumber}";

        var unauthorized = await Client.PostAsJsonAsync(
            registrationUrl,
            new { pushToken = $"push-{Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var registerRequest = new HttpRequestMessage(HttpMethod.Post, registrationUrl)
        {
            Content = JsonContent.Create(new { pushToken = $"push-{Guid.NewGuid():N}" })
        };
        registerRequest.Headers.TryAddWithoutValidation(
            "Authorization",
            $"ApplePass {testPass.AuthenticationToken}");

        var registered = await Client.SendAsync(registerRequest);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, registrationUrl);
        deleteRequest.Headers.TryAddWithoutValidation(
            "Authorization",
            $"ApplePass {testPass.AuthenticationToken}");

        var deleted = await Client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
    }

    [Fact]
    public async Task AppleLogEndpoint_ReturnsOk()
    {
        var response = await Client.PostAsJsonAsync(
            "/v1/log",
            new { logs = new[] { "Wallet test message" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<TestPass> SeedRegisteredPassAsync(bool registerDevice = true)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var customerId = Guid.NewGuid();
        var serial = $"KB-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var deviceLibraryIdentifier = $"device-{Guid.NewGuid():N}";

        var customer = new Customer(
            customerId,
            "Wallet Integration Test",
            $"wallet-{Guid.NewGuid():N}@test.local",
            new DateTime(1990, 1, 1),
            now);

        var card = new LoyaltyCard(Guid.NewGuid(), customerId, serial, now);
        db.Customers.Add(customer);
        db.LoyaltyCards.Add(card);

        if (registerDevice)
        {
            db.DeviceRegistrations.Add(new DeviceRegistration(
                Guid.NewGuid(),
                deviceLibraryIdentifier,
                PassTypeIdentifier,
                serial,
                $"push-{Guid.NewGuid():N}",
                now));
        }

        await db.SaveChangesAsync();

        return new TestPass(
            serial,
            deviceLibraryIdentifier,
            card.AuthenticationToken);
    }

    private sealed record TestPass(
        string SerialNumber,
        string DeviceLibraryIdentifier,
        string AuthenticationToken);
}
