using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class NoDefaultTenantStaticTests
{
    [Fact]
    [Trait("Category", "NoDefaultTenant")]
    public void Source_does_not_reference_default_tenant_configuration_or_service()
    {
        var root = GetRepositoryRoot();
        var files = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal)
                || path.EndsWith(".razor", StringComparison.Ordinal)
                || path.EndsWith(".json", StringComparison.Ordinal))
            .ToArray();

        var forbidden = new[]
        {
            "Default" + "Tenant" + "Slug",
            "Default" + "Tenant" + "Resolution" + "Service",
            "Tenancy" + ":"
        };

        var matches = files
            .SelectMany(file =>
            {
                var text = File.ReadAllText(file);
                return forbidden
                    .Where(text.Contains)
                    .Select(pattern => $"{Path.GetRelativePath(root, file)} contains {pattern}");
            })
            .ToArray();

        Assert.Empty(matches);
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "src")))
            current = current.Parent;

        return current?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
