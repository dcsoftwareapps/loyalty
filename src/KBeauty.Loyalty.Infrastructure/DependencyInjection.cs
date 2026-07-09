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
/// Composicion raiz de Infrastructure: DbContext, repositorios, servicios de
/// Wallet, storage, identidad y adaptadores externos.
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

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApplePassOptions>(configuration.GetSection(ApplePassOptions.SectionName));
        services.Configure<AzureStorageOptions>(configuration.GetSection(AzureStorageOptions.SectionName));
        services.Configure<WalletOptions>(configuration.GetSection(WalletOptions.SectionName));
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
        services.AddScoped<IRedemptionHistoryReadService, RedemptionHistoryReadService>();
    }

    private static void AddWalletServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment)
    {
        var isDevelopment = environment?.IsDevelopment() == true;
        var useRealPassSigning = configuration.GetValue<bool>("Wallet:UseRealPassSigning");
        var useLocalRealWallet = isDevelopment && useRealPassSigning;

        if (useLocalRealWallet)
        {
            services.AddScoped<IAppleWalletSecretsProvider, LocalAppleWalletSecretsProvider>();
            services.AddScoped<IPassGeneratorService, PassGeneratorService>();
            AddApnHttpClient(services);
            return;
        }

        if (isDevelopment)
        {
            services.AddScoped<IPassGeneratorService, DevelopmentPassGeneratorService>();
            services.AddScoped<IApnService, NoOpApnService>();
            return;
        }

        var keyVaultUri = configuration["Azure:KeyVaultUri"];
        if (string.IsNullOrWhiteSpace(keyVaultUri))
            throw new InvalidOperationException(
                "Falta Azure:KeyVaultUri para Wallet real fuera de Development. " +
                "Configura Key Vault o usa Development con Wallet:UseRealPassSigning=true para archivos locales.");

        services.AddKBeautyKeyVaultClient(keyVaultUri);
        services.AddScoped<IAppleWalletSecretsProvider, KeyVaultAppleWalletSecretsProvider>();
        services.AddScoped<IPassGeneratorService, PassGeneratorService>();
        AddApnHttpClient(services);
    }

    private static void AddApnHttpClient(IServiceCollection services)
    {
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
