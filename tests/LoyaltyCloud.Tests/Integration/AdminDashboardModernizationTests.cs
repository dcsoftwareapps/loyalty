using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class AdminDashboardModernizationTests
{
    [Fact]
    [Trait("Category", "AdminDashboardUx")]
    public void Dashboard_uses_real_summary_data_with_clear_hierarchy_and_actions()
    {
        var source = Read("src", "LoyaltyCloud.Admin", "Pages", "Dashboard.razor");

        Assert.Contains("Resumen de tu programa de lealtad", source);
        Assert.Contains("dashboard.Customers.TotalCustomers", source);
        Assert.Contains("dashboard.Points.CurrentPointBalance", source);
        Assert.Contains("dashboard.RecentActivity", source);
        Assert.Contains("href=\"/scan\"", source);
        Assert.Contains("href=\"/redeem\"", source);
        Assert.Contains("href=\"/customers\"", source);
        Assert.DoesNotContain("+12%", source);
    }

    [Fact]
    [Trait("Category", "AdminDashboardUx")]
    public void Dashboard_has_professional_loading_empty_and_responsive_activity_states()
    {
        var source = Read("src", "LoyaltyCloud.Admin", "Pages", "Dashboard.razor");

        Assert.Contains("role=\"status\"", source);
        Assert.Contains("Aún no hay actividad", source);
        Assert.Contains("Las operaciones recientes de tus clientes aparecerán aquí.", source);
        Assert.Contains("class=\"kb-table-responsive\"", source);
        Assert.Contains("data-label=\"Actividad\"", source);
    }

    [Fact]
    [Trait("Category", "AdminDashboardUx")]
    public void Main_navigation_uses_one_icon_component_and_accessible_mobile_controls()
    {
        var layout = Read("src", "LoyaltyCloud.Admin", "Components", "Layout", "MainLayout.razor");
        var icons = Read("src", "LoyaltyCloud.Admin", "Components", "NavIcon.razor");

        Assert.Contains("aria-label=\"Navegación principal\"", layout);
        Assert.Contains("aria-label=\"Abrir navegación\"", layout);
        Assert.Contains("aria-expanded=\"@menuOpen\"", layout);
        Assert.Contains("kb-sidebar-backdrop", layout);
        Assert.Contains("<NavIcon", layout);
        Assert.Contains("<svg class=\"kb-nav-icon\"", icons);
        Assert.DoesNotContain("🛍️", layout);
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { GetRepositoryRoot() }.Concat(segments).ToArray()));

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LoyaltyCloud.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
