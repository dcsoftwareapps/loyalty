using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Repositories;
using MediatR;

namespace LoyaltyCloud.Application.Config.Queries.GetProgramConfig;

/// <inheritdoc cref="GetProgramConfigQuery"/>
public sealed class GetProgramConfigHandler
    : IRequestHandler<GetProgramConfigQuery, Result<IReadOnlyList<ConfigDto>>>
{
    private readonly IProgramConfigRepository _config;

    public GetProgramConfigHandler(IProgramConfigRepository config)
    {
        _config = config;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ConfigDto>>> Handle(GetProgramConfigQuery _, CancellationToken ct)
    {
        var entries = await _config.GetAllAsync(ct);

        IReadOnlyList<ConfigDto> dtos = entries
            .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .Select(e => new ConfigDto(e.Key, e.Value, e.Description, e.UpdatedAt, e.UpdatedBy))
            .ToList()
            .AsReadOnly();

        return Result.Ok(dtos);
    }
}
