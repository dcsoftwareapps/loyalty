using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class CashierMobileAppTests
{
    [Fact]
    [Trait("Category", "CashierMobile")]
    public void Cashier_mobile_project_is_added_as_maui_blazor_hybrid_app()
    {
        var project = Read("src", "LoyaltyCloud.Cashier", "LoyaltyCloud.Cashier.csproj");
        var solution = Read("LoyaltyCloud.sln");

        Assert.Contains("Microsoft.NET.Sdk.Razor", project);
        Assert.Contains("<UseMaui>true</UseMaui>", project);
        Assert.Contains("net9.0-android", project);
        Assert.Contains("net9.0-ios", project);
        Assert.Contains("Microsoft.AspNetCore.Components.WebView.Maui", project);
        Assert.Contains("LoyaltyCloud Caja", project);
        Assert.Contains("src\\LoyaltyCloud.Cashier\\LoyaltyCloud.Cashier.csproj", solution);
    }

    [Fact]
    [Trait("Category", "CashierMobile")]
    public void Cashier_mobile_login_uses_real_cashier_auth_contract()
    {
        var page = Read("src", "LoyaltyCloud.Cashier", "Components", "Pages", "Home.razor");
        var contracts = Read("src", "LoyaltyCloud.Cashier", "Services", "CashierContracts.cs");
        var client = Read("src", "LoyaltyCloud.Cashier", "Services", "CashierApiClient.cs");

        Assert.Contains("api/auth/cashier/login", client);
        Assert.Contains("TenantSlug", contracts);
        Assert.Contains("Username", contracts);
        Assert.Contains("Password", contracts);
        Assert.Contains("AccessToken", contracts);
        Assert.Contains("TokenType", contracts);
        Assert.Contains("ExpiresAtUtc", contracts);
        Assert.Contains("ExpiresInSeconds", contracts);
        Assert.Contains("UserId", contracts);
        Assert.Contains("Role", contracts);
        Assert.Contains("Negocio", page);
        Assert.Contains("Usuario", page);
        Assert.Contains("Contraseña", page);
        Assert.Contains("type=\"password\"", page);
        Assert.Contains("Iniciar sesión", page);
    }

    [Fact]
    [Trait("Category", "CashierMobile")]
    public void Cashier_mobile_uses_secure_storage_and_restores_or_clears_expired_session()
    {
        var session = Read("src", "LoyaltyCloud.Cashier", "Services", "CashierSessionService.cs");
        var page = Read("src", "LoyaltyCloud.Cashier", "Components", "Pages", "Home.razor");

        Assert.Contains("SecureStorage.Default", session);
        Assert.Contains("loyaltycloud.cashier.session.v1", session);
        Assert.Contains("RestoreAsync", session);
        Assert.Contains("session.IsExpired(DateTimeOffset.UtcNow)", session);
        Assert.Contains("await LogoutAsync();", session);
        Assert.Contains("Sessions.RestoreAsync()", page);
        Assert.Contains("Sessions.SignInAsync", page);
        Assert.Contains("Sessions.LogoutAsync", page);
        Assert.DoesNotContain("localStorage", session, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionStorage", session, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Preferences", session, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "CashierMobile")]
    public void Cashier_mobile_configures_stg_and_prod_api_base_urls_without_secrets()
    {
        var options = Read("src", "LoyaltyCloud.Cashier", "Services", "CashierApiOptions.cs");
        var program = Read("src", "LoyaltyCloud.Cashier", "MauiProgram.cs");
        var project = Read("src", "LoyaltyCloud.Cashier", "LoyaltyCloud.Cashier.csproj");

        Assert.Contains("https://loyaltycloud-api-stg-01.azurewebsites.net", options);
        Assert.Contains("https://api.loyaltycloud.net", options);
        Assert.Contains("LOYALTYCLOUD_PROD", options);
        Assert.Contains("LOYALTYCLOUD_STG", options);
        Assert.Contains("<CashierEnvironment", project);
        Assert.Contains("AddHttpClient<CashierApiClient>", program);
        Assert.Contains("AddHttpClient<AuthenticatedCashierApiClient>", program);
        Assert.DoesNotContain("AdminApi", options);
        Assert.DoesNotContain("SharedSecret", options);
        Assert.DoesNotContain("SigningKey", options);
        Assert.DoesNotContain("ConnectionStrings", options);
    }

    [Fact]
    [Trait("Category", "CashierMobile")]
    public void Authenticated_cashier_client_adds_bearer_only_to_loyaltycloud_api_and_clears_on_401()
    {
        var handler = Read("src", "LoyaltyCloud.Cashier", "Services", "CashierAuthorizationHandler.cs");

        Assert.Contains("AuthenticationHeaderValue(\"Bearer\", session.AccessToken)", handler);
        Assert.Contains("IsLoyaltyCloudApiRequest", handler);
        Assert.Contains("absolute.Host.Equals(_options.BaseUri.Host", handler);
        Assert.Contains("HttpStatusCode.Unauthorized", handler);
        Assert.Contains("ClearAfterUnauthorizedAsync", handler);
    }

    [Fact]
    [Trait("Category", "CashierMobile")]
    public void Cashier_mobile_phase_4a_does_not_implement_operational_flows_yet()
    {
        var source = string.Join(
            "\n",
            Directory.GetFiles(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Cashier"), "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        Assert.DoesNotContain("api/points", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api/redemptions", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api/customers", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("giftcards", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Barcode", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Camera", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AdminApi:SharedSecret", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CashierAuth:SigningKey", source, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(GetRepositoryRoot(), Path.Combine(parts)));

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LoyaltyCloud.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
