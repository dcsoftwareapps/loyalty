using System.Reflection;
using KBeauty.Loyalty.Common.Constants;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Domain.Repositories;
using MediatR;

namespace KBeauty.Loyalty.Application.Config.Commands.UpdateProgramConfig;

/// <inheritdoc cref="UpdateProgramConfigCommand"/>
public sealed class UpdateProgramConfigHandler : IRequestHandler<UpdateProgramConfigCommand, Result>
{
    private readonly IProgramConfigRepository _config;
    private readonly IUnitOfWork _uow;

    // Set de claves válidas, construido por reflexión sobre LoyaltyConstants.ConfigKeys.
    // Así, cuando agregamos una clave nueva al constants file, automáticamente se acepta
    // — sin tocar el handler.
    private static readonly HashSet<string> AllowedKeys = BuildAllowedKeys();

    public UpdateProgramConfigHandler(IProgramConfigRepository config, IUnitOfWork uow)
    {
        _config = config;
        _uow = uow;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(UpdateProgramConfigCommand command, CancellationToken ct)
    {
        // Rechaza claves no listadas en LoyaltyConstants.ConfigKeys (evita typos
        // o intentos de inyectar claves arbitrarias desde el panel admin).
        var invalid = command.Entries
            .Select(e => e.Key)
            .Where(k => !AllowedKeys.Contains(k))
            .ToList();

        if (invalid.Count > 0)
            return Result.Fail($"Claves no reconocidas: {string.Join(", ", invalid)}.");

        foreach (var entry in command.Entries)
            await _config.UpsertAsync(entry.Key, entry.Value, command.UpdatedBy, ct);

        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static HashSet<string> BuildAllowedKeys()
    {
        return typeof(LoyaltyConstants.ConfigKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
