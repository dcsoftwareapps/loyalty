using Microsoft.Extensions.Configuration;

namespace KBeauty.Loyalty.Infrastructure.Services;

internal sealed class LocalAppleWalletSecretsProvider : IAppleWalletSecretsProvider
{
    private readonly IConfiguration _configuration;

    public LocalAppleWalletSecretsProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<byte[]> GetPassCertificateBytesAsync(CancellationToken cancellationToken)
    {
        var path = Required("Apple:PassCertificatePath");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Apple:PassCertificatePath apunta a '{path}', pero el archivo .p12 no existe.",
                path);

        return await File.ReadAllBytesAsync(path, cancellationToken);
    }

    public Task<string> GetPassCertificatePasswordAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Required("Apple:PassCertificatePassword"));

    public async Task<byte[]?> GetWwdrCertificateBytesAsync(CancellationToken cancellationToken)
    {
        var path = _configuration["Apple:WwdrCertificatePath"];
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Apple:WwdrCertificatePath apunta a '{path}', pero el certificado WWDR no existe.",
                path);

        return await File.ReadAllBytesAsync(path, cancellationToken);
    }

    public async Task<string> GetApnPrivateKeyPemAsync(CancellationToken cancellationToken)
    {
        var path = Required("Apple:ApnPrivateKeyPath");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Apple:ApnPrivateKeyPath apunta a '{path}', pero el archivo .p8 no existe.",
                path);

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    public Task<string> GetApnKeyIdAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Required("Apple:ApnKeyId"));

    public Task<string> GetApnTeamIdAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Required("Apple:TeamIdentifier"));

    private string Required(string key)
    {
        var value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Falta configuracion requerida '{key}' para Wallet:UseRealPassSigning=true. " +
                "Configura este valor con dotnet user-secrets o variables de entorno.");

        return value;
    }
}
