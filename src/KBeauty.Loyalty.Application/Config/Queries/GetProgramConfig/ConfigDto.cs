namespace KBeauty.Loyalty.Application.Config.Queries.GetProgramConfig;

public sealed record ConfigDto(
    string Key,
    string Value,
    string? Description,
    DateTime UpdatedAt,
    string? UpdatedBy);
