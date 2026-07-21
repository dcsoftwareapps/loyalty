using LoyaltyCloud.API.Middleware;
using LoyaltyCloud.API.Configuration;
using LoyaltyCloud.API.Services;
using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Infrastructure;
using LoyaltyCloud.Infrastructure.KeyVault;

var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// Configuración: agrega Azure Key Vault si está configurado.
// Los secrets quedan disponibles como IConfiguration["nombre"].
// =============================================================================
builder.Configuration.AddLoyaltyCloudKeyVault(builder.Configuration["Azure:KeyVaultUri"]);

// =============================================================================
// Servicios: capas Application + Infrastructure se auto-registran.
// =============================================================================
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.Configure<LoyaltyMaintenanceOptions>(
    builder.Configuration.GetSection(LoyaltyMaintenanceOptions.SectionName));
builder.Services.AddHostedService<LoyaltyMaintenanceBackgroundService>();
builder.Services.Configure<LoyaltyNotificationOptions>(
    builder.Configuration.GetSection(LoyaltyNotificationOptions.SectionName));
builder.Services.Configure<CustomNotificationCampaignOptions>(
    builder.Configuration.GetSection(CustomNotificationCampaignOptions.SectionName));
builder.Services.AddHostedService<LoyaltyNotificationBackgroundService>();

// Controllers + OpenAPI/Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "LoyaltyCloud API", Version = "v1" });
});

// Manejo global de excepciones — IExceptionHandler (.NET 8+).
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// CORS: Admin (Blazor) y otros front-ends llaman aquí.
var origins = builder.Configuration["Cors:AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries)
              ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p
        .WithOrigins(origins)
        .AllowAnyMethod()
        .AllowAnyHeader());
});

// =============================================================================
// Pipeline HTTP
// =============================================================================
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var apnService = scope.ServiceProvider.GetRequiredService<IApnService>();
    app.Logger.LogInformation("Resolved IApnService={ApnService}", apnService.GetType().Name);
    LogConfigurationValueSource(app.Logger, app.Configuration, "Wallet:UseRealApns");
    LogConfigurationValueSource(app.Logger, app.Configuration, "Wallet:UseRealPassSigning");
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseMiddleware<DefaultTenantResolutionMiddleware>();

// Middleware Apple Pass — corre ANTES que MapControllers para bloquear /v1/*
// con auth inválida sin llegar al controller.
app.UseMiddleware<ApplePassAuthMiddleware>();

app.MapControllers();

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

/// <summary>Para tests de integración con WebApplicationFactory.</summary>
public partial class Program { }
