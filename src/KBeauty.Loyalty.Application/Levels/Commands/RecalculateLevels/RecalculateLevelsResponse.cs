namespace KBeauty.Loyalty.Application.Levels.Commands.RecalculateLevels;

public sealed record RecalculateLevelsResponse(
    DateTime RunAt,
    int CardsProcessed,
    int CardsChanged,
    int CardsUpgraded,
    int CardsDowngraded,
    int WalletsNotified,
    IReadOnlyList<string> Warnings);
