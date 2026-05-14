using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KBeauty.Loyalty.Infrastructure.KeyVault;

/// <summary>
/// Extensiones para incorporar Azure Key Vault al pipeline de configuración
/// y al contenedor DI. Los secrets quedan disponibles vía
/// <c>IConfiguration["nombre-secret"]</c> y por <see cref="SecretClient"/>
/// inyectable cuando se necesitan en runtime (no solo en startup).
/// </summary>
public static class KeyVaultExtensions
{
    /// <summary>
    /// Agrega el vault al <see cref="ConfigurationManager"/>. Si <paramref name="vaultUri"/>
    /// es null/empty, no hace nada (útil para local dev sin Key Vault).
    /// </summary>
    public static IConfigurationBuilder AddKBeautyKeyVault(this IConfigurationBuilder builder, string? vaultUri)
    {
        if (string.IsNullOrWhiteSpace(vaultUri)) return builder;

        var credential = new DefaultAzureCredential(
            new DefaultAzureCredentialOptions
            {
                // En App Service usa la managed identity; localmente cae al Azure CLI / VS.
                ExcludeInteractiveBrowserCredential = false
            });

        builder.AddAzureKeyVault(new Uri(vaultUri), credential);
        return builder;
    }

    /// <summary>
    /// Registra un <see cref="SecretClient"/> singleton para lecturas en runtime
    /// (PassGeneratorService y ApnService lo usan para cert/.p8/keys).
    /// </summary>
    public static IServiceCollection AddKBeautyKeyVaultClient(this IServiceCollection services, string? vaultUri)
    {
        if (string.IsNullOrWhiteSpace(vaultUri))
        {
            // Registra un cliente "dummy" que falla en runtime con mensaje claro.
            // Esto deja levantar la app en dev sin KV configurado (con BlobStorage local).
            services.AddSingleton<SecretClient>(_ =>
                throw new InvalidOperationException(
                    "Azure:KeyVaultUri no configurado — no se puede crear SecretClient."));
            return services;
        }

        services.AddSingleton(new SecretClient(new Uri(vaultUri), new DefaultAzureCredential()));
        return services;
    }
}
