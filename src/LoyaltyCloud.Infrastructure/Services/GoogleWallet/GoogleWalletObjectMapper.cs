using LoyaltyCloud.Application.Common.Wallet;
using LoyaltyCloud.Infrastructure.Configuration;

namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

public sealed class GoogleWalletObjectMapper
{
    public GoogleWalletClassData ToClassData(string classId, GoogleWalletOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new GoogleWalletClassData(
            Id: classId,
            ProgramName: options.ProgramName,
            IssuerName: options.IssuerName,
            LogoUri: string.IsNullOrWhiteSpace(options.LogoUri) ? null : options.LogoUri.Trim(),
            HeroImageUri: string.IsNullOrWhiteSpace(options.HeroImageUri) ? null : options.HeroImageUri.Trim(),
            HexBackgroundColor: string.IsNullOrWhiteSpace(options.HexBackgroundColor) ? null : options.HexBackgroundColor.Trim());
    }

    public GoogleWalletObjectData ToObjectData(
        string objectId,
        string classId,
        MemberWalletData member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new GoogleWalletObjectData(
            Id: objectId,
            ClassId: classId,
            AccountName: member.FullName,
            AccountId: member.SerialNumber,
            PointsBalance: member.CurrentPoints,
            MembershipTier: member.Level,
            BarcodeValue: member.BarcodeValue,
            IsActive: member.IsActive,
            UpdatedAtUtc: member.LastActivityAt);
    }

    public Dictionary<string, object?> ToClassPayload(GoogleWalletClassData data)
    {
        var payload = new Dictionary<string, object?>
        {
            ["id"] = data.Id,
            ["issuerName"] = data.IssuerName,
            ["programName"] = data.ProgramName,
            ["reviewStatus"] = "UNDER_REVIEW"
        };

        if (!string.IsNullOrWhiteSpace(data.HexBackgroundColor))
            payload["hexBackgroundColor"] = data.HexBackgroundColor;

        if (!string.IsNullOrWhiteSpace(data.LogoUri))
        {
            payload["programLogo"] = ImageModule(data.LogoUri, $"{data.ProgramName} logo");
        }

        if (!string.IsNullOrWhiteSpace(data.HeroImageUri))
        {
            payload["heroImage"] = ImageModule(data.HeroImageUri, $"{data.ProgramName} hero");
        }

        return payload;
    }

    public Dictionary<string, object?> ToObjectPayload(GoogleWalletObjectData data)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = data.Id,
            ["classId"] = data.ClassId,
            ["state"] = data.IsActive ? "ACTIVE" : "INACTIVE",
            ["accountName"] = data.AccountName,
            ["accountId"] = data.AccountId,
            ["loyaltyPoints"] = new Dictionary<string, object?>
            {
                ["label"] = "Points",
                ["balance"] = new Dictionary<string, object?>
                {
                    ["int"] = data.PointsBalance
                }
            },
            ["barcode"] = new Dictionary<string, object?>
            {
                ["type"] = "QR_CODE",
                ["value"] = data.BarcodeValue,
                ["alternateText"] = data.AccountId
            },
            ["textModulesData"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = "membership-tier",
                    ["header"] = "Tier",
                    ["body"] = data.MembershipTier
                },
                new Dictionary<string, object?>
                {
                    ["id"] = "last-updated",
                    ["header"] = "Last updated",
                    ["body"] = data.UpdatedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'")
                }
            }
        };
    }

    private static Dictionary<string, object?> ImageModule(string uri, string description) =>
        new()
        {
            ["sourceUri"] = new Dictionary<string, object?>
            {
                ["uri"] = uri
            },
            ["contentDescription"] = new Dictionary<string, object?>
            {
                ["defaultValue"] = new Dictionary<string, object?>
                {
                    ["language"] = "en-US",
                    ["value"] = description
                }
            }
        };
}

