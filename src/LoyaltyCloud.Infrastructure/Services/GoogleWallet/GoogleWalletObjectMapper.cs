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
        bool includeReviewStatus = true)
    {
        var payload = new Dictionary<string, object?>
        {
            ["id"] = data.Id,
            ["issuerName"] = data.IssuerName,
            ["programName"] = data.ProgramName
        };

        if (includeReviewStatus)
            payload["reviewStatus"] = "UNDER_REVIEW";

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
                },
                new Dictionary<string, object?>
                {
                    ["id"] = "barcode-caption",
                    ["header"] = string.Empty,
                    ["body"] = data.BarcodeAlternateText
                },
                new Dictionary<string, object?>
                {
                    ["id"] = "last-updated",
                    ["header"] = "\u00daLTIMA ACTUALIZACI\u00d3N",
                    ["body"] = data.UpdatedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'")
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
                        ["threeItems"] = new Dictionary<string, object?>
                        {
                            ["startItem"] = TemplateItem("object.textModulesData['points']"),
                            ["middleItem"] = TemplateItem("object.textModulesData['level']"),
                            ["endItem"] = TemplateItem(
                                "object.textModulesData['next-level']",
                                "object.textModulesData['remaining-points']")
                        }
                    }
                }
            },
            ["cardBarcodeSectionDetails"] = new Dictionary<string, object?>
            {
                ["firstBottomDetail"] = new Dictionary<string, object?>
                {
                    ["fieldSelector"] = FieldSelector("object.textModulesData['barcode-caption']")
                }
            },
            ["detailsTemplateOverride"] = new Dictionary<string, object?>
            {
                ["detailsItemInfos"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["item"] = TemplateItem("object.textModulesData['remaining-points']")
                    },
                    new Dictionary<string, object?>
                    {
                        ["item"] = TemplateItem("object.textModulesData['last-updated']")
                    }
                }
            }
        };

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

