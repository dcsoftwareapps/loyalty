using LoyaltyCloud.Application.Common.Wallet;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Infrastructure.Configuration;

namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

public sealed class GoogleWalletObjectMapper
{
    public GoogleWalletClassData ToClassData(
        string classId,
        GoogleWalletOptions options,
        TenantWalletBrandingDto branding)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(branding);

        var logoUri = BuildTenantLogoUri(options, branding.TenantId);
        return new GoogleWalletClassData(
            Id: classId,
            ProgramName: branding.DisplayName,
            IssuerName: branding.OrganizationName,
            LogoUri: logoUri,
            WideLogoUri: logoUri,
            HeroImageUri: null,
            HexBackgroundColor: branding.BackgroundHex);
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
            AccountName: member.DisplayName,
            AccountId: member.SerialNumber,
            PointsBalance: member.CurrentPoints,
            MembershipTier: member.Level,
            BarcodeValue: member.BarcodeValue,
            IsActive: member.IsActive,
            UpdatedAtUtc: member.LastActivityAt,
            PointsText: member.PointsText,
            LevelText: member.LevelText,
            NextLevelText: member.NextLevelText,
            RemainingPointsText: member.RemainingPointsText,
            BarcodeAlternateText: member.BarcodeAlternateText);
    }

    public Dictionary<string, object?> ToClassPayload(
        GoogleWalletClassData data,
        bool includeProgramLogo = true)
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

        if (includeProgramLogo && !string.IsNullOrWhiteSpace(data.LogoUri))
        {
            payload["programLogo"] = ImageModule(data.LogoUri, $"{data.ProgramName} logo");
        }

        if (!string.IsNullOrWhiteSpace(data.WideLogoUri))
        {
            payload["wideProgramLogo"] = ImageModule(data.WideLogoUri, $"{data.ProgramName} wide logo");
        }

        if (!string.IsNullOrWhiteSpace(data.HeroImageUri))
        {
            payload["heroImage"] = ImageModule(data.HeroImageUri, $"{data.ProgramName} hero");
        }

        payload["classTemplateInfo"] = BuildClassTemplateInfo();

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
                ["label"] = "PUNTOS",
                ["balance"] = new Dictionary<string, object?>
                {
                    ["int"] = data.PointsBalance
                }
            },
            ["barcode"] = new Dictionary<string, object?>
            {
                ["type"] = "QR_CODE",
                ["value"] = data.BarcodeValue,
                ["alternateText"] = data.BarcodeAlternateText
            },
            ["textModulesData"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = "member-name",
                    ["header"] = string.Empty,
                    ["body"] = data.AccountName
                },
                new Dictionary<string, object?>
                {
                    ["id"] = "points",
                    ["header"] = "PUNTOS",
                    ["body"] = data.PointsText
                },
                new Dictionary<string, object?>
                {
                    ["id"] = "level",
                    ["header"] = "NIVEL",
                    ["body"] = data.LevelText
                },
                new Dictionary<string, object?>
                {
                    ["id"] = "next-level",
                    ["header"] = "PR\u00d3XIMO",
                    ["body"] = data.NextLevelText
                },
                new Dictionary<string, object?>
                {
                    ["id"] = "remaining-points",
                    ["header"] = "FALTAN",
                    ["body"] = data.RemainingPointsText
                }
            }
        };
    }

    private static Dictionary<string, object?> BuildClassTemplateInfo() =>
        new()
        {
            ["cardTemplateOverride"] = new Dictionary<string, object?>
            {
                ["cardRowTemplateInfos"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["oneItem"] = new Dictionary<string, object?>
                        {
                            ["item"] = TemplateItem("object.textModulesData['member-name']")
                        }
                    },
                    new Dictionary<string, object?>
                    {
                        ["twoItems"] = new Dictionary<string, object?>
                        {
                            ["startItem"] = TemplateItem("object.textModulesData['points']"),
                            ["endItem"] = TemplateItem("object.textModulesData['level']")
                        }
                    },
                    new Dictionary<string, object?>
                    {
                        ["twoItems"] = new Dictionary<string, object?>
                        {
                            ["startItem"] = TemplateItem("object.textModulesData['next-level']"),
                            ["endItem"] = TemplateItem("object.textModulesData['remaining-points']")
                        }
                    }
                }
            }
        };

    private static string? BuildTenantLogoUri(GoogleWalletOptions options, Guid tenantId)
    {
        var configuredUri = string.IsNullOrWhiteSpace(options.LogoUri)
            ? options.WideLogoUri
            : options.LogoUri;
        if (!Uri.TryCreate(configuredUri, UriKind.Absolute, out var publicUri))
            return null;

        return new Uri(publicUri, $"/api/wallet-assets/google/{tenantId:D}/logo.png").ToString();
    }
    private static Dictionary<string, object?> TemplateItem(string firstFieldPath, string? secondFieldPath = null)
    {
        var item = new Dictionary<string, object?>
        {
            ["firstValue"] = FieldSelector(firstFieldPath)
        };

        if (!string.IsNullOrWhiteSpace(secondFieldPath))
            item["secondValue"] = FieldSelector(secondFieldPath);

        return item;
    }

    private static Dictionary<string, object?> FieldSelector(string fieldPath) =>
        new()
        {
            ["fields"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["fieldPath"] = fieldPath
                }
            }
        };

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

