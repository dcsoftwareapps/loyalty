using LoyaltyCloud.Admin.Auth;
using LoyaltyCloud.Admin.Middleware;
using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Infrastructure;
using LoyaltyCloud.Infrastructure.KeyVault;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Key Vault (opcional en dev).
builder.Configuration.AddLoyaltyCloudKeyVault(builder.Configuration["Azure:KeyVaultUri"]);

// =============================================================================
// Servicios
// =============================================================================

// Capas de negocio — Admin habla con Application/MediatR in-process, no HTTP.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

var apiBaseUrl = builder.Configuration["Admin:ApiBaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl))
    throw new InvalidOperationException("Falta Admin:ApiBaseUrl para que Admin invoque la API backend.");

builder.Services.AddHttpClient("LoyaltyCloudApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

// Blazor Web App con Interactive Server.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Auth básica con cookie — credenciales desde appsettings.
builder.Services.Configure<AdminAuthOptions>(builder.Configuration.GetSection(AdminAuthOptions.SectionName));
builder.Services.AddScoped<AdminAuthService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.Name = "loyaltycloud.admin.auth";
    });

// Política por defecto: todo requiere autenticación; las páginas que no la
// requieran usan [AllowAnonymous] (Login).
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddCascadingAuthenticationState();

// =============================================================================
// Pipeline
// =============================================================================
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var apnService = scope.ServiceProvider.GetRequiredService<IApnService>();
    app.Logger.LogInformation("Resolved IApnService={ApnService}", apnService.GetType().Name);
    LogConfigurationValueSource(app.Logger, app.Configuration, "Wallet:UseRealApns");
    LogConfigurationValueSource(app.Logger, app.Configuration, "Wallet:UseRealPassSigning");
}

if (app.Environment.IsDevelopment())
{
    await app.Services.SeedDevelopmentDataAsync(app.Environment);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.MapStaticAssets()
    .AllowAnonymous();
app.UseRouting();
app.UseMiddleware<DefaultTenantResolutionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorComponents<LoyaltyCloud.Admin.App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous();

// Endpoint POST para sign-out — Blazor no puede invocar SignOutAsync interactivo
// (necesita el HttpContext durante el ciclo de response), así que va por MVC mínimo.
app.MapPost("/logout", async (HttpContext ctx, AdminAuthService auth) =>
{
    await auth.SignOutAsync(ctx);
    return Results.Redirect("/login");
});

if (app.Environment.IsDevelopment())
{
    app.MapPost("/admin/dev-login", async (HttpContext ctx, AdminAuthService auth) =>
    {
        await auth.SignInAsync(ctx, "dev-owner");
        return Results.Redirect("/dashboard");
    })
    .AllowAnonymous()
    .DisableAntiforgery();
}

app.Run();

static void LogConfigurationValueSource(ILogger logger, IConfiguration configuration, string key)
{
    var value = configuration[key] ?? "<null>";
    var providers = configuration is IConfigurationRoot root
        ? root.Providers
            .Where(provider => provider.TryGet(key, out _))
            .Select(provider =>
            {
                provider.TryGet(key, out var providerValue);
                return $"{provider.GetType().Name}={providerValue ?? "<null>"}";
            })
            .ToArray()
        : [];

    logger.LogInformation(
        "Configuration {Key}={Value}; Providers={Providers}",
        key,
        value,
        providers.Length == 0 ? "<none>" : string.Join("; ", providers));
}
