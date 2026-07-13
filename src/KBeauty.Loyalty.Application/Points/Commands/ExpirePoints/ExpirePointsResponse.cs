namespace KBeauty.Loyalty.Application.Points.Commands.ExpirePoints;

public sealed record ExpirePointsResponse(
    DateTime RunAt,
    bool Enabled,
    int ClientsProcessed,
    int ClientsAffected,
    int LotsExpired,
    int PointsExpired,
    int WalletsNotified,
    IReadOnlyList<string> Warnings);
