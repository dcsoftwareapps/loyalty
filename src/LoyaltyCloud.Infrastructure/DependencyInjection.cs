using System.Net;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.KeyVault;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Repositories;
using LoyaltyCloud.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LoyaltyCloud.Infrastructure;

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
        services.AddScoped<IPointLotRepository, PointLotRepository>();
        services.AddScoped<IPointCampaignRepository, PointCampaignRepository>();
        services.AddScoped<IRedemptionRepository, RedemptionRepository>();
        services.AddScoped<IRewardCatalogRepository, RewardCatalogRepository>();
        services.AddScoped<IProgramConfigRepository, ProgramConfigRepository>();
        services.AddScoped<IDeviceRegistrationRepository, DeviceRegistrationRepository>();
        services.AddScoped<ILoyaltyNotificationRepository, LoyaltyNotificationRepository>();
        services.AddScoped<ICustomNotificationCampaignRepository, CustomNotificationCampaignRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantAdminUserRepository, TenantAdminUserRepository>();
    }

    private static void AddCrossCuttingServices(IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<IMutableTenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<IDefaultTenantResolutionService, DefaultTenantResolutionService>();
        services.AddScoped<IOperationalTenantReadService, OperationalTenantReadService>();
        services.AddSingleton<ITenantExecutionRunner, TenantExecutionRunner>();
        services.AddScoped<IPasswordHashingService, PasswordHashingService>();

        services.AddScoped<IDashboardReadService, DashboardReadService>();
        services.AddScoped<ICustomerListReadService, CustomerListReadService>();
        services.AddScoped<ICustomerDetailReadService, CustomerDetailReadService>();
        services.AddScoped<IRedemptionHistoryReadService, RedemptionHistoryReadService>();
        services.AddScoped<IWalletNotificationReadService, WalletNotificationReadService>();
        services.AddScoped<ILoyaltyCardTenantLookup, LoyaltyCardTenantLookup>();
        services.AddScoped<IWalletTenantContextResolver, WalletTenantContextResolver>();
        services.AddScoped<IDeviceRegistrationPlatformReadService, DeviceRegistrationPlatformReadService>();
        services.AddScoped<ITenantWalletBrandingReadService, TenantWalletBrandingReadService>();
        services.AddScoped<IPublicTenantResolver, PublicTenantResolver>();
        services.AddScoped<IPointsExpirationNotificationReadService, PointsExpirationNotificationReadService>();
        services.AddScoped<IMonthlyProductNotificationReadService, MonthlyProductNotificationReadService>();
        services.AddScoped<IBirthdayBenefitNotificationReadService, BirthdayBenefitNotificationReadService>();
        services.AddScoped<IPointCampaignNotificationReadService, PointCampaignNotificationReadService>();
        services.AddScoped<ICustomNotificationAudienceReadService, CustomNotificationAudienceReadService>();
        services.AddScoped<ILoyaltyNotificationService, LoyaltyNotificationService>();
        services.AddScoped<INotificationChannelProcessor, AppleWalletNotificationChannelProcessor>();
    }

    private static void AddWalletServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment)
    {
        var isDevelopment = environment?.IsDevelopment() == true;
        var useRealPassSigning = configuration.GetValue<bool>("Wallet:UseRealPassSigning");
        var useRealApns = configuration.GetValue<bool>("Wallet:UseRealApns");

        if (isDevelopment)
        {
            Console.WriteLine(
                "Wallet DI: Environment={0}, Wallet.UseRealApns={1}, Wallet.UseRealPassSigning={2}, Registering IApnService={3}",
                environment?.EnvironmentName ?? "unknown",
                useRealApns,
                useRealPassSigning,
                useRealApns ? nameof(ApnService) : nameof(NoOpApnService));

            if (useRealPassSigning || useRealApns)
                services.AddScoped<IAppleWalletSecretsProvider, LocalAppleWalletSecretsProvider>();

            if (useRealPassSigning)
            {
                services.AddScoped<PassGeneratorService>();
                services.AddScoped<IPassGeneratorService>(sp => sp.GetRequiredService<PassGeneratorService>());
            }
            else
            {
                services.AddScoped<DevelopmentPassGeneratorService>();
                services.AddScoped<IPassGeneratorService>(sp => sp.GetRequiredService<DevelopmentPassGeneratorService>());
            }

            if (useRealApns)
            {
                ValidateLocalApnsConfiguration(configuration);
                AddApnHttpClient(services);
            }
            else
            {
                services.AddScoped<IApnService, NoOpApnService>();
            }

            return;
        }

        var keyVaultUri = configuration["Azure:KeyVaultUri"];
        if (string.IsNullOrWhiteSpace(keyVaultUri))
            throw new InvalidOperationException(
                "Falta Azure:KeyVaultUri para Wallet real fuera de Development. " +
                "Configura Key Vault o usa Development con Wallet:UseRealPassSigning=true para archivos locales.");

        services.AddLoyaltyCloudKeyVaultClient(keyVaultUri);
        services.AddScoped<IAppleWalletSecretsProvider, KeyVaultAppleWalletSecretsProvider>();
        services.AddScoped<IPassGeneratorService, PassGeneratorService>();
        Console.WriteLine(
            "Wallet DI: Environment={0}, Wallet.UseRealApns={1}, Wallet.UseRealPassSigning={2}, Registering IApnService={3}",
            environment?.EnvironmentName ?? "unknown",
            useRealApns,
            useRealPassSigning,
            nameof(ApnService));
        AddApnHttpClient(services);
    }

    private static void ValidateLocalApnsConfiguration(IConfiguration configuration)
    {
        Require("Wallet:UseRealApns", configuration["Wallet:UseRealApns"]);
        Require("Apple:ApnPrivateKeyPath", configuration["Apple:ApnPrivateKeyPath"]);
        Require("Apple:ApnKeyId", configuration["Apple:ApnKeyId"]);
        Require("Apple:TeamIdentifier", configuration["Apple:TeamIdentifier"]);
        Require("Apple:PassTypeIdentifier", configuration["Apple:PassTypeIdentifier"]);
        Require("Apple:ApnHost", configuration["Apple:ApnHost"]);

        var privateKeyPath = configuration["Apple:ApnPrivateKeyPath"]!;
        if (!File.Exists(privateKeyPath))
        {
            throw new InvalidOperationException(
                $"Wallet:UseRealApns=true but Apple:ApnPrivateKeyPath does not exist: {privateKeyPath}");
        }

        static void Require(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Wallet:UseRealApns=true but {key} is missing.");
        }
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
