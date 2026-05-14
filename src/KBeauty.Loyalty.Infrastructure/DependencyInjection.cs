using System.Net;
using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Repositories;
using KBeauty.Loyalty.Infrastructure.Configuration;
using KBeauty.Loyalty.Infrastructure.KeyVault;
using KBeauty.Loyalty.Infrastructure.Persistence;
using KBeauty.Loyalty.Infrastructure.Repositories;
using KBeauty.Loyalty.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KBeauty.Loyalty.Infrastructure;

/// <summary>
/// Composición de raíz de Infrastructure. Una sola llamada
/// <see cref="AddInfrastructure"/> en API/Admin registra todo el grafo:
/// DbContext, repositorios, servicios de Wallet, storage, identidad, Key Vault.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        AddPersistence(services, configuration);
        AddOptions(services, configuration);
        AddRepositories(services);
        AddCrossCuttingServices(services);
        AddWalletServices(services, configuration, environment);
        AddStorageService(services);

        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Falta ConnectionStrings:DefaultConnection (en appsettings o Key Vault).");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            }));

        // AppDbContext también es la unidad de trabajo — un único scope por request.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApplePassOptions>(configuration.GetSection(ApplePassOptions.SectionName));
        services.Configure<AzureStorageOptions>(configuration.GetSection(AzureStorageOptions.SectionName));
    }

    private static void AddRepositories(IServiceCollection services)
    {
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ILoyaltyCardRepository, LoyaltyCardRepository>();
        services.AddScoped<IPointTransactionRepository, PointTransactionRepository>();
        services.AddScoped<IRedemptionRepository, RedemptionRepository>();
        services.AddScoped<IRewardCatalogRepository, RewardCatalogRepository>();
        services.AddScoped<IProgramConfigRepository, ProgramConfigRepository>();
        services.AddScoped<IDeviceRegistrationRepository, DeviceRegistrationRepository>();
    }

    private static void AddCrossCuttingServices(IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddScoped<IDashboardReadService, DashboardReadService>();
        services.AddScoped<ICustomerListReadService, CustomerListReadService>();
    }

    private static void AddWalletServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment)
    {
        // Cliente de Key Vault para leer cert + p8 + ids en runtime.
        services.AddKBeautyKeyVaultClient(configuration["Azure:KeyVaultUri"]);

        // En Development usamos un .pkpass mock no firmado para no depender de
        // Key Vault/certificados Apple durante pruebas funcionales locales.
        if (environment?.IsDevelopment() == true)
            services.AddScoped<IPassGeneratorService, DevelopmentPassGeneratorService>();
        else
            services.AddScoped<IPassGeneratorService, PassGeneratorService>();

        // APN cliente — HTTP/2 explícito y timeout corto (el push debe ser fire-and-forget).
        services
            .AddHttpClient<IApnService, ApnService>(client =>
            {
                client.DefaultRequestVersion = HttpVersion.Version20;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
                client.Timeout = TimeSpan.FromSeconds(10);
            });
    }

    private static void AddStorageService(IServiceCollection services)
    {
        services.AddScoped<IStorageService, BlobStorageService>();
    }
}
