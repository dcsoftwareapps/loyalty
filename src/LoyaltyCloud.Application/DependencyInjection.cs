using System.Reflection;
using FluentValidation;
using LoyaltyCloud.Application.Common.Behaviors;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LoyaltyCloud.Application;

/// <summary>
/// Composición de raíz de la capa Application. Registra MediatR, validators,
/// y los pipeline behaviors (Validation primero, Logging por fuera).
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Inyecta MediatR (escanea handlers en este assembly), FluentValidation
    /// (escanea validators) y los pipeline behaviors en el orden correcto.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // El orden importa: el último que se agrega corre primero (envuelve).
            // Quiero que validación corra ANTES de logging (para que loguear vea
            // el resultado fallido), así que registro Logging primero y Validation
            // después — Validation queda como el behavior más interno.
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddScoped<ILevelCalculationService, LevelCalculationService>();
        services.AddScoped<IPointCampaignSelector, PointCampaignSelector>();

        return services;
    }
}
