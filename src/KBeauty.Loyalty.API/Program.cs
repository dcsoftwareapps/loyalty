using KBeauty.Loyalty.API.Middleware;
using KBeauty.Loyalty.Application;
using KBeauty.Loyalty.Infrastructure;
using KBeauty.Loyalty.Infrastructure.KeyVault;

var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// Configuración: agrega Azure Key Vault si está configurado.
// Los secrets quedan disponibles como IConfiguration["nombre"].
// =============================================================================
builder.Configuration.AddKBeautyKeyVault(builder.Configuration["Azure:KeyVaultUri"]);

// =============================================================================
// Servicios: capas Application + Infrastructure se auto-registran.
// =============================================================================
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Controllers + OpenAPI/Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "KBeauty Loyalty API", Version = "v1" });
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

// Middleware Apple Pass — corre ANTES que MapControllers para bloquear /v1/*
// con auth inválida sin llegar al controller.
app.UseMiddleware<ApplePassAuthMiddleware>();

app.MapControllers();

app.Run();

/// <summary>Para tests de integración con WebApplicationFactory.</summary>
public partial class Program { }
