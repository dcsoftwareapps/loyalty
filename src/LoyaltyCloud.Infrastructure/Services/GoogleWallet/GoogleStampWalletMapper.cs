using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Infrastructure.Configuration;

namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

public sealed class GoogleStampWalletMapper
{
    public GoogleStampWalletClassData ToClassData(
        string classId,
        GoogleWalletOptions options,
        TenantWalletBrandingDto branding)
    {
        var logoUri = BuildTenantLogoUri(options, branding.TenantId);
        return new(
            classId,
            branding.DisplayName,
            branding.OrganizationName,
            logoUri,
            logoUri,
            branding.BackgroundHex);
    }

    public GoogleStampWalletObjectData ToObjectData(
        string objectId,
        string classId,
        string accountName,
        string serialNumber,
        bool isActive,
        DateTime updatedAtUtc,
        StampWalletProjection projection,
        string? progressImageUri)
    {
        var remaining = Math.Max(0, projection.MaxStamps - projection.CurrentStamps);
        var next = projection.Rewards
            .Where(x => x.State == StampWalletRewardState.Upcoming && x.RequiredStamps > projection.CurrentStamps)
            .OrderBy(x => x.RequiredStamps)
            .FirstOrDefault();
        var earned = projection.EarnedRewards ?? [];
        var availableRewards = earned.Where(x => x.State == StampWalletRewardState.Available).ToList();
        var redeemed = earned.Count(x => x.State == StampWalletRewardState.Redeemed);

        return new(
            objectId,
            classId,
            AccountName(accountName),
            serialNumber,
            projection.CurrentStamps,
            projection.MaxStamps,
            projection.CycleSequence,
            $"{projection.CurrentStamps} de {projection.MaxStamps} sellos",
            remaining == 0 ? "Tarjeta completa" : $"{remaining} sellos",
            next is null ? "Recompensa final alcanzada" : $"En el sello {next.RequiredStamps}",
            availableRewards.Count == 0 ? "Ninguna" : string.Join(", ", availableRewards.Select(x => x.ShortName).Distinct()),
            redeemed == 0 ? "Sin canjes en este ciclo" : redeemed == 1 ? "1 recompensa canjeada" : $"{redeemed} recompensas canjeadas",
            serialNumber,
            "Presenta este codigo en caja",
            progressImageUri,
            isActive,
            updatedAtUtc);
    }

    public Dictionary<string, object?> ToClassPayload(GoogleStampWalletClassData data)
    {
        var payload = new Dictionary<string, object?>
        {
            ["id"] = data.Id,
            ["issuerName"] = data.IssuerName,
            ["programName"] = data.ProgramName,
            ["reviewStatus"] = "UNDER_REVIEW",
            ["classTemplateInfo"] = ClassTemplate()
        };
        if (!string.IsNullOrWhiteSpace(data.HexBackgroundColor)) payload["hexBackgroundColor"] = data.HexBackgroundColor;
        if (!string.IsNullOrWhiteSpace(data.LogoUri)) payload["programLogo"] = Image(data.LogoUri, $"{data.ProgramName} logo");
        if (!string.IsNullOrWhiteSpace(data.WideLogoUri)) payload["wideProgramLogo"] = Image(data.WideLogoUri, $"{data.ProgramName} logo");
        return payload;
    }

    public Dictionary<string, object?> ToObjectPayload(GoogleStampWalletObjectData data)
    {
        var payload = new Dictionary<string, object?>
        {
            ["id"] = data.Id,
            ["classId"] = data.ClassId,
            ["state"] = data.IsActive ? "ACTIVE" : "INACTIVE",
            ["accountName"] = data.AccountName,
            ["accountId"] = data.AccountId,
            ["loyaltyPoints"] = new Dictionary<string, object?>
            {
                ["label"] = "SELLOS",
                ["balance"] = new Dictionary<string, object?> { ["int"] = data.CurrentStamps }
            },
            ["barcode"] = new Dictionary<string, object?>
            {
                ["type"] = "QR_CODE",
                ["value"] = data.BarcodeValue,
                ["alternateText"] = data.BarcodeAlternateText
            },
            ["textModulesData"] = TextModules(data)
        };
        if (!string.IsNullOrWhiteSpace(data.ProgressImageUri))
            payload["heroImage"] = Image(data.ProgressImageUri, $"Progreso: {data.ProgressText}");
        return payload;
    }

    private static object[] TextModules(GoogleStampWalletObjectData data) =>
    [
        Module("member-name", string.Empty, data.AccountName),
        Module("stamps-progress", "SELLOS", data.ProgressText),
        Module("remaining", "FALTAN", data.RemainingText),
        Module("next-reward", "PROXIMA RECOMPENSA", data.NextRewardText),
        Module("available-rewards", "DISPONIBLES", data.AvailableRewardsText),
        Module("reward-history", "CANJES DEL CICLO", data.RewardHistoryText)
    ];

    private static Dictionary<string, object?> Module(string id, string header, string body) =>
        new() { ["id"] = id, ["header"] = header, ["body"] = body };

    private static Dictionary<string, object?> ClassTemplate() => new()
    {
        ["cardTemplateOverride"] = new Dictionary<string, object?>
        {
            ["cardRowTemplateInfos"] = new object[]
            {
                One("object.textModulesData['member-name']"),
                Two("object.textModulesData['stamps-progress']", "object.textModulesData['remaining']"),
                Two("object.textModulesData['next-reward']", "object.textModulesData['available-rewards']")
            }
        }
    };

    private static Dictionary<string, object?> One(string path) => new()
    {
        ["oneItem"] = new Dictionary<string, object?> { ["item"] = Item(path) }
    };

    private static Dictionary<string, object?> Two(string start, string end) => new()
    {
        ["twoItems"] = new Dictionary<string, object?>
        {
            ["startItem"] = Item(start), ["endItem"] = Item(end)
        }
    };

    private static Dictionary<string, object?> Item(string path) => new()
    {
        ["firstValue"] = new Dictionary<string, object?>
        {
            ["fields"] = new[] { new Dictionary<string, object?> { ["fieldPath"] = path } }
        }
    };

    private static Dictionary<string, object?> Image(string uri, string description) => new()
    {
        ["sourceUri"] = new Dictionary<string, object?> { ["uri"] = uri },
        ["contentDescription"] = new Dictionary<string, object?>
        {
            ["defaultValue"] = new Dictionary<string, object?> { ["language"] = "es-MX", ["value"] = description }
        }
    };

    private static string? BuildTenantLogoUri(GoogleWalletOptions options, Guid tenantId)
    {
        var configured = string.IsNullOrWhiteSpace(options.LogoUri) ? options.WideLogoUri : options.LogoUri;
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var origin)
            || origin.Scheme != Uri.UriSchemeHttps || origin.IsLoopback) return null;
        return new Uri(origin, $"/api/wallet-assets/google/{tenantId:D}/logo.png").ToString();
    }

    private static string AccountName(string value)
    {
        var first = value?.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(first) ? "Cliente" : first;
    }
}
