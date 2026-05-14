using System.Net;
using System.Net.Http.Json;
using KBeauty.Loyalty.Application.Customers.Commands.RegisterCustomer;
using KBeauty.Loyalty.Application.Customers.Queries.GetCustomerBySerial;
using KBeauty.Loyalty.Application.Points.Commands.AddPoints;
using KBeauty.Loyalty.Application.Redemptions.Commands.RedeemReward;
using KBeauty.Loyalty.Application.Redemptions.Queries.GetRedemptionCatalog;
using KBeauty.Loyalty.Common.Constants;
using Xunit;

namespace KBeauty.Loyalty.Tests.Integration;

/// <summary>
/// Test end-to-end: registra clienta → suma puntos → consulta catálogo → canjea.
/// Verifica que toda la cadena (API → Application → Infrastructure → DB) funcione
/// con la pipeline real (Validation + Logging behaviors, MediatR, EF).
/// </summary>
public sealed class FullFlowIntegrationTests : IntegrationTestBase
{
    public FullFlowIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task FullFlow_Register_AddPoints_Redeem_ShouldSucceed()
    {
        // ====================================================================
        // 1. Registrar clienta nueva
        // ====================================================================
        var registerPayload = new
        {
            fullName = "Ana López",
            email = $"ana.lopez+{Guid.NewGuid():N}@test.com",
            dateOfBirth = new DateTime(1990, 3, 15),
            phone = "+526461234567"
        };

        var registerResponse = await Client.PostAsJsonAsync("/api/customers", registerPayload);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var registered = await registerResponse.Content.ReadFromJsonAsync<RegisterCustomerResponse>();
        Assert.NotNull(registered);
        Assert.StartsWith("KB-", registered!.SerialNumber);
        Assert.Equal(50, registered.CurrentPoints); // welcome bonus
        Assert.Equal(LoyaltyConstants.Levels.Mist, registered.Level);

        var serial = registered.SerialNumber;

        // Verifica que el bono de bienvenida se persistió y la consulta lo refleja.
        var customer = await Client.GetFromJsonAsync<CustomerDetailDto>($"/api/customers/{serial}");
        Assert.NotNull(customer);
        Assert.Equal(50, customer!.CurrentPoints);
        Assert.Equal(LoyaltyConstants.Levels.Mist, customer.Level);

        // ====================================================================
        // 2. Sumar puntos por una compra grande para llegar a 350 (>= 300 mini producto)
        // ====================================================================
        var addPointsRequest = new HttpRequestMessage(HttpMethod.Post, "/api/points")
        {
            Content = JsonContent.Create(new { serialNumber = serial, purchaseAmount = 3000m })
        };
        addPointsRequest.Headers.Add("X-Operator-Id", "integration-test");

        var addResponse = await Client.SendAsync(addPointsRequest);
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);

        var addResult = await addResponse.Content.ReadFromJsonAsync<AddPointsResponse>();
        Assert.NotNull(addResult);
        Assert.Equal(300, addResult!.PointsAdded); // $3000 / 10 = 300 pts
        Assert.Equal(350, addResult.NewTotal);     // 50 welcome + 300

        // ====================================================================
        // 3. Consultar catálogo de canjes para esta clienta (Mist con 350 pts)
        // ====================================================================
        var catalog = await Client.GetFromJsonAsync<List<RewardCatalogItemDto>>(
            $"/api/redemptions/catalog/{serial}");
        Assert.NotNull(catalog);
        Assert.NotEmpty(catalog!);

        var miniProduct = catalog.FirstOrDefault(r => r.PointsCost == 300);
        Assert.NotNull(miniProduct);
        Assert.True(miniProduct!.CanAfford); // 350 >= 300

        // ====================================================================
        // 4. Canjear el mini producto
        // ====================================================================
        var redeemRequest = new HttpRequestMessage(HttpMethod.Post, "/api/redemptions")
        {
            Content = JsonContent.Create(new
            {
                serialNumber = serial,
                rewardCatalogItemId = miniProduct.Id
            })
        };
        redeemRequest.Headers.Add("X-Operator-Id", "integration-test");

        var redeemResponse = await Client.SendAsync(redeemRequest);
        Assert.Equal(HttpStatusCode.Created, redeemResponse.StatusCode);

        var redemption = await redeemResponse.Content.ReadFromJsonAsync<RedemptionResponse>();
        Assert.NotNull(redemption);
        Assert.Equal(300, redemption!.PointsSpent);
        Assert.Equal(50, redemption.RemainingPoints); // 350 - 300

        // ====================================================================
        // 5. Verificar el saldo final
        // ====================================================================
        var afterRedeem = await Client.GetFromJsonAsync<CustomerDetailDto>($"/api/customers/{serial}");
        Assert.Equal(50, afterRedeem!.CurrentPoints);
        Assert.Equal(350, afterRedeem.LifetimePoints); // lifetime NO decrece
    }

    [Fact]
    public async Task AddPoints_ShouldReturnFail_WhenSerialNotFound()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/points")
        {
            Content = JsonContent.Create(new { serialNumber = "KB-NOEXIST", purchaseAmount = 100m })
        };
        req.Headers.Add("X-Operator-Id", "test");

        var response = await Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterCustomer_ShouldReturnBadRequest_WhenEmailIsInvalid()
    {
        var response = await Client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Test",
            email = "not-an-email",
            dateOfBirth = new DateTime(1990, 1, 1)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCustomer_ShouldReturn404_WhenSerialDoesNotExist()
    {
        var response = await Client.GetAsync("/api/customers/KB-NOEXIST");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
