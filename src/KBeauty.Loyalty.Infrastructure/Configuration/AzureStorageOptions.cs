namespace KBeauty.Loyalty.Infrastructure.Configuration;

/// <summary>Configuración para Azure Blob Storage donde se almacenan los .pkpass.</summary>
public sealed class AzureStorageOptions
{
    public const string SectionName = "Azure:BlobStorage";

    /// <summary>Connection string al storage account (en local: Azurite; en prod: managed identity).</summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>Nombre del contenedor donde van los .pkpass.</summary>
    public string PassContainer { get; init; } = "passes";

    /// <summary>Duración (en minutos) de los SAS tokens generados para descarga.</summary>
    public int SasExpirationMinutes { get; init; } = 15;
}
