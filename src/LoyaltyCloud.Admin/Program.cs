using LoyaltyCloud.Admin.Auth;
using LoyaltyCloud.Admin.Middleware;
using LoyaltyCloud.Admin.Services;
using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Infrastructure;
using LoyaltyCloud.Infrastructure.KeyVault;
using LoyaltyCloud.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Key Vault (opcional en dev).
builder.Configuration.AddLoyaltyCloudKeyVault(builder.Configuration["Azure:KeyVaultUri"]);

// =============================================================================
// Servicios
// =============================================================================

// Capas de negocio — Admin habla con Application/MediatR in-process, no HTTP.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddScoped<AdminTenantContextInitializer>();
builder.Services.AddScoped<CircuitHandler, AdminTenantCircuitHandler>();
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AdminTenantContextBehavior<,>));

var apiBaseUrl = builder.Configuration["Admin:ApiBaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl))
    throw new InvalidOperationException("Falta Admin:ApiBaseUrl para que Admin invoque la API backend.");

builder.Services.AddHttpClient("LoyaltyCloudApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});
builder.Services.AddScoped<AdminApiPointsClient>();
builder.Services.AddScoped<AdminApiClient>();
builder.Services.AddSingleton<AdminDateTimeFormatter>();

// Blazor Web App con Interactive Server.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Auth básica con cookie — credenciales desde appsettings.
var adminAuthOptions = builder.Configuration
    .GetSection(AdminAuthOptions.SectionName)
    .Get<AdminAuthOptions>() ?? new AdminAuthOptions();
var adminSessionHours = Math.Max(1, adminAuthOptions.SessionHours);

builder.Services.Configure<AdminAuthOptions>(builder.Configuration.GetSection(AdminAuthOptions.SectionName));
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddScoped<LoyaltyCloud.Admin.Services.GiftCardFeatureState>();
builder.Services.AddScoped<TenantStaffService>();
builder.Services.Configure<SuperAdminAuthOptions>(builder.Configuration.GetSection(SuperAdminAuthOptions.SectionName));
builder.Services.AddScoped<SuperAdminAuthService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/platform/login";
        options.AccessDeniedPath = "/platform/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(adminSessionHours);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.Name = "loyaltycloud.admin.auth";
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                context.Response.Redirect(AdminLoginRedirects.BuildTenantAwareLoginRedirect(context.Request, context.RedirectUri));
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                context.Response.Redirect(AdminLoginRedirects.BuildTenantAwareLoginRedirect(context.Request, context.RedirectUri));
                return Task.CompletedTask;
            },
            OnValidatePrincipal = async context =>
            {
                var auth = context.HttpContext.RequestServices.GetRequiredService<AdminAuthService>();
                await auth.ValidatePrincipalAsync(context);
            }
        };
    })
    .AddCookie(SuperAdminAuthDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/platform/login";
        options.AccessDeniedPath = "/platform/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(
            Math.Max(1, builder.Configuration.GetValue<int?>("SuperAdmin:SessionHours") ?? 8));
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.Name = "loyaltycloud.platform.auth";
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                context.Response.Redirect(AdminLoginRedirects.BuildPlatformLoginRedirect(context.Request));
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                context.Response.Redirect(AdminLoginRedirects.BuildPlatformLoginRedirect(context.Request));
                return Task.CompletedTask;
            },
            OnValidatePrincipal = async context =>
            {
                var auth = context.HttpContext.RequestServices.GetRequiredService<SuperAdminAuthService>();
                await auth.ValidatePrincipalAsync(context);
            }
        };
    });

// Política por defecto: todo requiere autenticación; las páginas que no la
// requieran usan [AllowAnonymous] (Login).
builder.Services.AddAuthorization(TenantAuthorization.Configure);

builder.Services.AddScoped<IAuthorizationHandler, GiftCardsEnabledHandler>();
builder.Services.AddCascadingAuthenticationState();

const string PublicSignupRateLimitPolicy = "PublicSignup";
var signupPermitLimit = Math.Max(1, builder.Configuration.GetValue<int?>("RateLimiting:Signup:PermitLimit") ?? 10);
var signupWindowMinutes = Math.Max(1, builder.Configuration.GetValue<int?>("RateLimiting:Signup:WindowMinutes") ?? 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "Demasiados intentos. Espera unos minutos e inténtalo nuevamente.",
            cancellationToken);
    };
    options.AddPolicy(PublicSignupRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = signupPermitLimit,
                Window = TimeSpan.FromMinutes(signupWindowMinutes),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

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
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<SuperAdminAuthenticationMiddleware>();
app.UseMiddleware<AdminTenantContextMiddleware>();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorComponents<LoyaltyCloud.Admin.App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous();

app.MapPost("/signup", async (
    [FromForm] PublicSignupForm form,
    HttpContext context,
    IAntiforgery antiforgery,
    ISender sender,
    AdminAuthService auth,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    await antiforgery.ValidateRequestAsync(context);

    if (!string.Equals(form.Password, form.ConfirmPassword, StringComparison.Ordinal))
        return Results.Redirect("/signup?error=password-mismatch");

    try
    {
        var result = await sender.Send(
            new LoyaltyCloud.Application.SelfServiceSignup.SelfServiceSignupCommand(
                form.Email,
                form.Password,
                form.BusinessDisplayName,
                form.Slug,
                form.TimeZoneId,
                form.SignupAttemptId),
            ct);

        if (result.IsFailure)
        {
            var code = result.Error.Contains("identificador del negocio", StringComparison.OrdinalIgnoreCase)
                ? "slug-conflict"
                : "invalid";
            return Results.Redirect($"/signup?error={code}");
        }

        var login = await auth.TrySignInAsync(
            context,
            result.Value.TenantSlug,
            $"{result.Value.TenantSlug}-owner",
            form.Password,
            ct);

        return login == AdminLoginResult.Success
            ? Results.Redirect(auth.GetAuthenticatedLandingPath(context.User))
            : Results.Redirect($"/{Uri.EscapeDataString(result.Value.TenantSlug)}/login");
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Public self-service signup failed unexpectedly.");
        return Results.Redirect("/signup?error=unexpected");
    }
})
    .AllowAnonymous()
    .RequireRateLimiting(PublicSignupRateLimitPolicy);

// Endpoint POST para sign-out — Blazor no puede invocar SignOutAsync interactivo
// (necesita el HttpContext durante el ciclo de response), así que va por MVC mínimo.
app.MapGet("/giftcards/claim/{token}/wallet/apple", async (string token, LoyaltyCloud.Application.GiftCards.IGiftCardClaimService claims, CancellationToken ct) =>
{
    try
    {
        var pass = await claims.GetApplePassAsync(token, ct);
        return Results.File(pass.Bytes, "application/vnd.apple.pkpass", $"giftcard-{pass.SerialNumber}.pkpass");
    }
    catch (Exception) { return Results.NotFound(); }
}).AllowAnonymous();

app.MapGet("/giftcards/claim/{token}/wallet/google", (string token) =>
{
    var path = $"api/public/giftcards/claim/{Uri.EscapeDataString(token)}/wallet/google";
    return Results.Redirect(new Uri(new Uri(apiBaseUrl.TrimEnd('/') + "/"), path).ToString());
}).AllowAnonymous();
app.MapPost("/logout", async (HttpContext ctx, AdminAuthService auth) =>
{
    var loginPath = auth.GetLoginPathForCurrentPrincipal(ctx);
    await auth.SignOutAsync(ctx);
    return Results.Redirect(loginPath);
});

app.MapPost("/platform/logout", async (HttpContext ctx, SuperAdminAuthService auth) =>
{
    await auth.SignOutAsync(ctx);
    return Results.Redirect("/platform/login");
});

if (app.Environment.IsDevelopment())
{
    app.MapPost("/platform/developer-login", async (HttpContext ctx, SuperAdminAuthService auth) =>
    {
        var result = await auth.TryDeveloperSignInAsync(ctx);
        return result == SuperAdminLoginResult.Success
            ? Results.Redirect("/platform/tenants")
            : Results.NotFound();
    }).AllowAnonymous();
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

public partial class Program { }
