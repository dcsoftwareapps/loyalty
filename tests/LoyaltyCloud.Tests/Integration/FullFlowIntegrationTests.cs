using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyCloud.Application.Customers.Queries.GetCustomerBySerial;
using LoyaltyCloud.Application.Points.Commands.AddPoints;
using LoyaltyCloud.Application.Redemptions.Commands.RedeemReward;
using LoyaltyCloud.Application.Redemptions.Queries.GetRedemptionCatalog;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Common.Security;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

/// <summary>
/// Test end-to-end del flujo RC1: alta publica tenant-aware, consulta firmada,
/// suma de puntos, catalogo y canje.
/// </summary>
public sealed class FullFlowIntegrationTests : IntegrationTestBase
{
    private const string SharedSecret = "test-admin-api-shared-secret-with-enough-length";
    private const string TenantSlug = "kbeauty";

    public FullFlowIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task FullFlow_Register_AddPoints_Redeem_ShouldSucceed()
    {
        var registerPayload = new
        {
            firstName = "Ana",
            lastName = "Lopez",
            phone = "+52646" + Random.Shared.Next(1000000, 9999999)
        };

        using var registerResponse = await Client.PostAsJsonAsync($"/api/public/{TenantSlug}/join", registerPayload);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var registered = await registerResponse.Content.ReadFromJsonAsync<PublicJoinResponse>();
        Assert.NotNull(registered);
        Assert.StartsWith("KB-", registered!.SerialNumber);

        var serial = registered.SerialNumber;

        using var getCustomerResponse = await Client.SendAsync(CreateSignedRequest(
            HttpMethod.Get,
            $"/api/customers/{serial}",
            body: null,
            operatorId: "integration-test"));
        Assert.Equal(HttpStatusCode.OK, getCustomerResponse.StatusCode);

        var customer = await getCustomerResponse.Content.ReadFromJsonAsync<CustomerDetailDto>();
        Assert.NotNull(customer);
        Assert.Equal(50, customer!.CurrentPoints);
        Assert.Equal(LoyaltyConstants.Levels.Mist, customer.Level);

        using var addPointsRequest = CreateSignedRequest(
            HttpMethod.Post,
            "/api/points",
            new { serialNumber = serial, purchaseAmount = 3000m },
            operatorId: "integration-test");
        using var addResponse = await Client.SendAsync(addPointsRequest);
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);

        var addResult = await addResponse.Content.ReadFromJsonAsync<AddPointsResponse>();
        Assert.NotNull(addResult);
        Assert.Equal(300, addResult!.PointsAdded);
        Assert.Equal(350, addResult.NewTotal);

        using var catalogResponse = await Client.SendAsync(CreateSignedRequest(
            HttpMethod.Get,
            $"/api/redemptions/catalog/{serial}",
            body: null,
            operatorId: "integration-test"));
        Assert.Equal(HttpStatusCode.OK, catalogResponse.StatusCode);

        var catalog = await catalogResponse.Content.ReadFromJsonAsync<List<RewardCatalogItemDto>>();
        Assert.NotNull(catalog);
        Assert.NotEmpty(catalog!);

        var miniProduct = catalog.FirstOrDefault(r => r.PointsCost == 300);
        Assert.NotNull(miniProduct);
        Assert.True(miniProduct!.CanAfford);

        using var redeemRequest = CreateSignedRequest(
            HttpMethod.Post,
            "/api/redemptions",
            new
            {
                serialNumber = serial,
                rewardCatalogItemId = miniProduct.Id
            },
            operatorId: "integration-test");
        using var redeemResponse = await Client.SendAsync(redeemRequest);
        Assert.Equal(HttpStatusCode.Created, redeemResponse.StatusCode);

        var redemption = await redeemResponse.Content.ReadFromJsonAsync<RedemptionResponse>();
        Assert.NotNull(redemption);
        Assert.Equal(300, redemption!.PointsSpent);
        Assert.Equal(50, redemption.RemainingPoints);

        using var afterRedeemResponse = await Client.SendAsync(CreateSignedRequest(
            HttpMethod.Get,
            $"/api/customers/{serial}",
            body: null,
            operatorId: "integration-test"));
        Assert.Equal(HttpStatusCode.OK, afterRedeemResponse.StatusCode);

        var afterRedeem = await afterRedeemResponse.Content.ReadFromJsonAsync<CustomerDetailDto>();
        Assert.Equal(50, afterRedeem!.CurrentPoints);
        Assert.Equal(350, afterRedeem.LifetimePoints);
    }

    [Fact]
    public async Task AddPoints_ShouldReturnFail_WhenSerialNotFound()
    {
        using var req = CreateSignedRequest(
            HttpMethod.Post,
            "/api/points",
            new { serialNumber = "KB-NOEXIST", purchaseAmount = 100m },
            operatorId: "test");

        using var response = await Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterCustomer_ShouldReturnBadRequest_WhenPhoneIsInvalid()
    {
        using var response = await Client.PostAsJsonAsync($"/api/public/{TenantSlug}/join", new
        {
            firstName = "",
            lastName = "",
            phone = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCustomer_ShouldReturn404_WhenSerialDoesNotExist()
    {
        using var response = await Client.SendAsync(CreateSignedRequest(
            HttpMethod.Get,
            "/api/customers/KB-NOEXIST",
            body: null,
            operatorId: "test"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static HttpRequestMessage CreateSignedRequest(
        HttpMethod method,
        string path,
        object? body,
        string operatorId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var bodyBytes = body is null
            ? []
            : JsonSerializer.SerializeToUtf8Bytes(
                body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var signature = AdminApiSignature.CreateSignature(
            SharedSecret,
            method.Method,
            path,
            timestamp,
            TenantSlug,
            operatorId,
            bodyBytes);

        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new ByteArrayContent(bodyBytes);
            request.Content.Headers.ContentType = new("application/json");
        }

        request.Headers.Add(AdminApiSignature.TenantSlugHeader, TenantSlug);
        request.Headers.Add(AdminApiSignature.OperatorHeader, operatorId);
        request.Headers.Add(AdminApiSignature.TimestampHeader, timestamp);
        request.Headers.Add(AdminApiSignature.SignatureHeader, signature);
        return request;
    }

    private sealed record PublicJoinResponse(
        Guid CustomerId,
        string SerialNumber,
        string FullName,
        string Phone,
        bool AlreadyExists,
        string Message,
        string PassDownloadUrl);
}
