using KBeauty.Loyalty.Admin.Auth;
using KBeauty.Loyalty.Application;
using KBeauty.Loyalty.Infrastructure;
using KBeauty.Loyalty.Infrastructure.KeyVault;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Key Vault (opcional en dev).
builder.Configuration.AddKBeautyKeyVault(builder.Configuration["Azure:KeyVaultUri"]);

// =============================================================================
// Servicios
// =============================================================================

// Capas de negocio — Admin habla con Application/MediatR in-process, no HTTP.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

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
        options.Cookie.Name = "kbeauty.admin.auth";
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorComponents<KBeauty.Loyalty.Admin.App>()
    .AddInteractiveServerRenderMode();

// Endpoint POST para sign-out — Blazor no puede invocar SignOutAsync interactivo
// (necesita el HttpContext durante el ciclo de response), así que va por MVC mínimo.
app.MapPost("/logout", async (HttpContext ctx, AdminAuthService auth) =>
{
    await auth.SignOutAsync(ctx);
    return Results.Redirect("/login");
});

app.Run();
