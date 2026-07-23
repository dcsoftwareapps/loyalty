using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Provisioning;
using LoyaltyCloud.Infrastructure;
using LoyaltyCloud.Infrastructure.KeyVault;
using LoyaltyCloud.Infrastructure.Services;
using LoyaltyCloud.Tools;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var command = args.FirstOrDefault();
if (string.Equals(command, "hash-password", StringComparison.OrdinalIgnoreCase))
{
    var passwordToHash = Environment.GetEnvironmentVariable("LOYALTYCLOUD_PASSWORD");
    if (string.IsNullOrWhiteSpace(passwordToHash))
    {
        Console.Error.WriteLine("Falta LOYALTYCLOUD_PASSWORD.");
        return 2;
    }

    Console.WriteLine(new PasswordHashingService().HashPassword(passwordToHash));
    return 0;
}

if (!IsKnownServiceCommand(command))
{
    PrintUsage();
    return 2;
}

var values = ParseArgs(args.Skip(1));

var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? Environments.Production;

var configuration = BuildConfiguration(args.Skip(1).ToArray(), environmentName);

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddSimpleConsole(options => options.SingleLine = true));
services.AddSingleton<IConfiguration>(configuration);
services.AddSingleton<IHostEnvironment>(new ToolHostEnvironment(environmentName, Directory.GetCurrentDirectory()));
services.AddApplication();
services.AddInfrastructure(configuration, new ToolHostEnvironment(environmentName, Directory.GetCurrentDirectory()));
services.AddScoped<TenantAdminPasswordTool>();

await using var provider = services.BuildServiceProvider(validateScopes: true);
using var scope = provider.CreateScope();

if (string.Equals(command, "reset-tenant-admin-password", StringComparison.OrdinalIgnoreCase))
{
    var tool = scope.ServiceProvider.GetRequiredService<TenantAdminPasswordTool>();
    return await tool.ResetPasswordAsync(
        values.GetValueOrDefault("tenant-slug"),
        values.GetValueOrDefault("admin-username"),
        Environment.GetEnvironmentVariable("LOYALTYCLOUD_ADMIN_PASSWORD"),
        Console.Out,
        Console.Error);
}

if (string.Equals(command, "list-tenant-admins", StringComparison.OrdinalIgnoreCase))
{
    var tool = scope.ServiceProvider.GetRequiredService<TenantAdminPasswordTool>();
    return await tool.ListAdminsAsync(
        values.GetValueOrDefault("tenant-slug"),
        Console.Out,
        Console.Error);
}

return await ProvisionTenantAsync(scope.ServiceProvider, values);

static IConfigurationRoot BuildConfiguration(string[] args, string environmentName)
{
    var root = FindRepositoryRoot();
    var apiDir = Path.Combine(root, "src", "LoyaltyCloud.API");

    var builder = new ConfigurationBuilder()
        .SetBasePath(apiDir)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
        .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
        .AddEnvironmentVariables()
        .AddCommandLine(args);

    var initialConfiguration = builder.Build();
    builder.AddLoyaltyCloudKeyVault(initialConfiguration["Azure:KeyVaultUri"]);

    return builder.Build();
}

static Dictionary<string, string?> ParseArgs(IEnumerable<string> args)
{
    var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    string? pendingKey = null;

    foreach (var arg in args)
    {
        if (arg.StartsWith("--", StringComparison.Ordinal))
        {
            pendingKey = arg[2..];
            result[pendingKey] = null;
            continue;
        }

        if (pendingKey is not null)
        {
            result[pendingKey] = arg;
            pendingKey = null;
        }
    }

    return result;
}

static string Require(IReadOnlyDictionary<string, string?> values, string key)
{
    if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        throw new ArgumentException($"Falta --{key}.");

    return value;
}

static bool IsKnownServiceCommand(string? command) =>
    string.Equals(command, "provision-tenant", StringComparison.OrdinalIgnoreCase)
    || string.Equals(command, "reset-tenant-admin-password", StringComparison.OrdinalIgnoreCase)
    || string.Equals(command, "list-tenant-admins", StringComparison.OrdinalIgnoreCase);

static async Task<int> ProvisionTenantAsync(IServiceProvider services, Dictionary<string, string?> values)
{
    var password = Environment.GetEnvironmentVariable("LOYALTYCLOUD_PROVISION_ADMIN_PASSWORD");
    if (string.IsNullOrWhiteSpace(password))
    {
        Console.Error.WriteLine("Falta LOYALTYCLOUD_PROVISION_ADMIN_PASSWORD.");
        return 2;
    }

    var sender = services.GetRequiredService<ISender>();
    var result = await sender.Send(new ProvisionTenantCommand(
        Slug: Require(values, "slug"),
        DisplayName: Require(values, "display-name"),
        TimeZoneId: values.GetValueOrDefault("timezone"),
        AdminUsername: Require(values, "admin-username"),
        AdminPassword: password,
        PrimaryColor: values.GetValueOrDefault("primary-color"),
        SecondaryColor: values.GetValueOrDefault("secondary-color"),
        SupportPhone: values.GetValueOrDefault("support-phone"),
        WhatsAppUrl: values.GetValueOrDefault("whatsapp-url"),
        InstagramUrl: values.GetValueOrDefault("instagram-url"),
        TermsUrl: values.GetValueOrDefault("terms-url")));

    if (result.IsFailure)
    {
        Console.Error.WriteLine(string.Join(Environment.NewLine, result.Errors));
        return 1;
    }

    Console.WriteLine($"Tenant provisioned. TenantId={result.Value.TenantId}; TenantSlug={result.Value.TenantSlug}; AdminUserId={result.Value.AdminUserId}; SubscriptionStatus={result.Value.SubscriptionStatus}");
    return 0;
}

static string FindRepositoryRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "src")))
        current = current.Parent;

    return current?.FullName ?? Directory.GetCurrentDirectory();
}

static void PrintUsage()
{
    Console.WriteLine("""
    Uso:
      set LOYALTYCLOUD_PROVISION_ADMIN_PASSWORD=<password>
      dotnet run --project src/LoyaltyCloud.Tools -- provision-tenant --slug beauty-room --display-name "Beauty Room" --admin-username owner

      set LOYALTYCLOUD_ADMIN_PASSWORD=<new-password>
      dotnet run --project src/LoyaltyCloud.Tools -- reset-tenant-admin-password --tenant-slug kbeauty --admin-username owner

      dotnet run --project src/LoyaltyCloud.Tools -- list-tenant-admins --tenant-slug kbeauty

      set LOYALTYCLOUD_PASSWORD=<password>
      dotnet run --project src/LoyaltyCloud.Tools -- hash-password
    """);
}

file sealed class ToolHostEnvironment : IHostEnvironment
{
    public ToolHostEnvironment(string environmentName, string contentRootPath)
    {
        EnvironmentName = environmentName;
        ContentRootPath = contentRootPath;
    }

    public string EnvironmentName { get; set; }
    public string ApplicationName { get; set; } = "LoyaltyCloud.Tools";
    public string ContentRootPath { get; set; }
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
        new Microsoft.Extensions.FileProviders.NullFileProvider();
}
