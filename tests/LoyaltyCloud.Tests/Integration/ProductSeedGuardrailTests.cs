using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class ProductSeedGuardrailTests
{
    [Fact]
    [Trait("Category", "TenantProvisioning")]
    [Trait("Category", "PlatformTenantDeletion")]
    public void Product_model_does_not_seed_kbeauty_tenant()
    {
        var root = FindRepositoryRoot();
        var appDbContext = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Infrastructure", "Persistence", "AppDbContext.cs"));
        var snapshot = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Infrastructure", "Persistence", "Migrations", "AppDbContextModelSnapshot.cs"));
        var seedPath = Path.Combine(root, "src", "LoyaltyCloud.Infrastructure", "Persistence", "Seed");

        Assert.DoesNotContain("TenantSeed.Apply", appDbContext, StringComparison.Ordinal);
        Assert.DoesNotContain("ProgramConfigSeed.Apply", appDbContext, StringComparison.Ordinal);
        var seedFiles = Directory.Exists(seedPath)
            ? Directory.GetFiles(seedPath, "*.cs", SearchOption.AllDirectories)
            : [];
        Assert.Empty(seedFiles);
        Assert.DoesNotContain("Slug = \"kbeauty\"", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("b1000000-0000-0000-0000-000000000001", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "PlatformTenantDeletion")]
    public void Platform_tenant_detail_requires_slug_confirmation_for_hard_delete()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Pages", "PlatformTenantDetail.razor"));

        Assert.Contains("Eliminar tenant", page, StringComparison.Ordinal);
        Assert.Contains("delete-confirmation", page, StringComparison.Ordinal);
        Assert.Contains("string.Equals(deleteConfirmationSlug, tenant.Slug, StringComparison.Ordinal)", page, StringComparison.Ordinal);
        Assert.Contains("DeleteTenantCommand", page, StringComparison.Ordinal);
        Assert.Contains("/platform/tenants?deleted=1", page, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LoyaltyCloud.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be found.");
    }
}
