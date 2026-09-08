using LoyaltyCloud.Cashier.Services;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Cashier;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton(CashierApiOptions.Default);
        builder.Services.AddSingleton<ISecureTokenStore, MauiSecureTokenStore>();
        builder.Services.AddSingleton<CashierSessionService>();
        builder.Services.AddSingleton<CashierAuthorizationHandler>();
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
