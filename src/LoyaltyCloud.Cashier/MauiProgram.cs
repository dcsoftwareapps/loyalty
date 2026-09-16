using LoyaltyCloud.Cashier.Services;
using Microsoft.Extensions.Logging;
using ZXing.Net.Maui.Controls;

namespace LoyaltyCloud.Cashier;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton(CashierApiOptions.Default);
        builder.Services.AddSingleton<ISecureTokenStore, MauiSecureTokenStore>();
        builder.Services.AddSingleton<CashierSessionService>();
        builder.Services.AddSingleton<CashierLoginPreferences>();
        builder.Services.AddSingleton<CashierAuthorizationHandler>();
        builder.Services.AddSingleton<IQrScannerService, MauiQrScannerService>();
        builder.Services.AddSingleton<CashierCustomerService>();
        builder.Services.AddSingleton<CashierRedemptionService>();
        builder.Services.AddSingleton<ICashierRewardRedemptionApi>(services =>
            services.GetRequiredService<CashierRedemptionService>());
        builder.Services.AddSingleton<ICashierMonetaryRedemptionApi>(services =>
            services.GetRequiredService<CashierRedemptionService>());
        builder.Services.AddSingleton<IRewardRedemptionPendingStore, MauiRewardRedemptionPendingStore>();
        builder.Services.AddSingleton(services => new RewardRedemptionCoordinator(
            services.GetRequiredService<ICashierRewardRedemptionApi>(),
            services.GetRequiredService<IRewardRedemptionPendingStore>(),
            () => services.GetRequiredService<CashierSessionService>().Current,
            services.GetRequiredService<CashierApiOptions>().BaseUri.AbsoluteUri));
        builder.Services.AddSingleton<IMonetaryRedemptionPendingStore, MauiMonetaryRedemptionPendingStore>();
        builder.Services.AddSingleton(services => new MonetaryRedemptionCoordinator(
            services.GetRequiredService<ICashierMonetaryRedemptionApi>(),
            services.GetRequiredService<IMonetaryRedemptionPendingStore>(),
            () => services.GetRequiredService<CashierSessionService>().Current,
            services.GetRequiredService<CashierApiOptions>().BaseUri.AbsoluteUri));
        builder.Services.AddSingleton<ICashierGiftCardApi, CashierGiftCardApi>();
        builder.Services.AddSingleton<IGiftCardPendingStore, MauiGiftCardPendingStore>();
        builder.Services.AddSingleton(services => new GiftCardRedemptionCoordinator(
            services.GetRequiredService<ICashierGiftCardApi>(),
            services.GetRequiredService<IGiftCardPendingStore>(),
            () => services.GetRequiredService<CashierSessionService>().Current,
            services.GetRequiredService<CashierApiOptions>().BaseUri.AbsoluteUri));
        builder.Services.AddSingleton<IGiftCardIssuancePendingStore, MauiGiftCardIssuancePendingStore>();
        builder.Services.AddSingleton(services => new GiftCardIssuanceCoordinator(
            services.GetRequiredService<ICashierGiftCardApi>(),
            services.GetRequiredService<IGiftCardIssuancePendingStore>(),
            () => services.GetRequiredService<CashierSessionService>().Current,
            services.GetRequiredService<CashierApiOptions>().BaseUri.AbsoluteUri));
        builder.Services.AddHttpClient<CashierApiClient>((services, client) =>
        {
            var options = services.GetRequiredService<CashierApiOptions>();
            client.BaseAddress = options.BaseUri;
        });
        builder.Services.AddHttpClient<AuthenticatedCashierApiClient>((services, client) =>
        {
            var options = services.GetRequiredService<CashierApiOptions>();
            client.BaseAddress = options.BaseUri;
        }).AddHttpMessageHandler<CashierAuthorizationHandler>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
