using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Constants;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KBeauty.Loyalty.Infrastructure.Services;

/// <summary>Genera el archivo .pkpass firmado para Apple Wallet.</summary>
internal sealed class PassGeneratorService : IPassGeneratorService
{
    private readonly IAppleWalletSecretsProvider _secrets;
    private readonly IWalletNotificationReadService _walletNotifications;
    private readonly ApplePassOptions _options;
    private readonly ILogger<PassGeneratorService> _logger;

    private static readonly JsonSerializerOptions PassJsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = null
    };

    private const string LevelFieldKey = "level";
    private const string PointsExpiringFieldKey = "points_expiring";
    private const string MonthlyProductFieldKey = "monthly_product";
    private const string PointCampaignFieldKey = "point_campaign";
    private const string BirthdayBenefitFieldKey = "birthday_benefit";
    private const string CustomMessageFieldKey = "custom_message";

    private static readonly PassAssetSpec[] PassAssetSpecs =
    [
        new("icon.png", 29, 29),
        new("icon@2x.png", 58, 58),
        new("icon@3x.png", 87, 87),
        new("logo.png", 160, 50),
        new("logo@2x.png", 320, 100),
        new("logo@3x.png", 480, 150)
    ];

    public PassGeneratorService(
        IAppleWalletSecretsProvider secrets,
        IWalletNotificationReadService walletNotifications,
        IOptions<ApplePassOptions> options,
        ILogger<PassGeneratorService> logger)
    {
        _secrets = secrets;
        _walletNotifications = walletNotifications;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<byte[]> GeneratePassAsync(LoyaltyCard card, Customer customer, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(customer);

        var walletContext = await _walletNotifications.GetActiveContextAsync(card.Id, ct);
        var passJson = BuildPassJson(card, customer, walletContext);
        var passJsonBytes = JsonSerializer.SerializeToUtf8Bytes(passJson, PassJsonOpts);
        var assets = LoadPassAssets();

        var manifest = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pass.json"] = Sha1Hex(passJsonBytes)
        };

        foreach (var asset in assets)
            manifest[asset.Name] = Sha1Hex(asset.Bytes);

        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, PassJsonOpts);
        var signatureBytes = await SignManifestAsync(manifestBytes, ct);

        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddZipEntry(zip, "pass.json", passJsonBytes);
            AddZipEntry(zip, "manifest.json", manifestBytes);
            AddZipEntry(zip, "signature", signatureBytes);

            foreach (var asset in assets)
                AddZipEntry(zip, asset.Name, asset.Bytes);
        }

        _logger.LogInformation(
            "Pass .pkpass generado para serial {Serial} ({Bytes} bytes). Archivos: {Files}",
            card.SerialNumber,
            output.Length,
            string.Join(", ", new[] { "pass.json", "manifest.json", "signature" }.Concat(assets.Select(a => a.Name))));

        return output.ToArray();
    }

    public Task<string> GetPassDownloadUrlAsync(string serialNumber, CancellationToken ct = default) =>
        Task.FromResult($"/api/customers/{serialNumber}/pass");

    private object BuildPassJson(LoyaltyCard card, Customer customer, WalletNotificationContext walletContext)
    {
        EnsureRequiredOption(_options.PassTypeIdentifier, "Apple:PassTypeIdentifier");
        EnsureRequiredOption(_options.TeamIdentifier, "Apple:TeamIdentifier");
        EnsureRequiredOption(_options.WebServiceURL, "Apple:WebServiceURL");
        EnsureRequiredOption(_options.OrganizationName, "Apple:OrganizationName");

        var progress = BuildLevelProgress(card);
        var displayName = GetWalletDisplayName(customer);
        var levelChangeMessage = walletContext.RecentVisibleEvent?.Type == NotificationType.LevelChanged
            ? BuildLevelChangeMessage(card, walletContext.LevelChange)
            : null;
        var auxiliaryFields = BuildAuxiliaryFields(
            progress,
            levelChangeMessage,
            walletContext);
        var backFields = BuildBackFields(
            progress,
            walletContext.News,
            walletContext.MonthlyProduct,
            walletContext.BirthdayBenefit,
            walletContext.PointCampaign,
            walletContext.CustomMessage);

        _logger.LogInformation(
            "Apple Wallet pass fields for serial {Serial}: levelKey={LevelFieldKey}, levelValue={LevelValue}, levelChangeMessageIncluded={LevelChangeMessageIncluded}, pointsExpiringIncluded={PointsExpiringIncluded}, monthlyProductIncluded={MonthlyProductIncluded}, birthdayBenefitIncluded={BirthdayBenefitIncluded}, pointCampaignIncluded={PointCampaignIncluded}, recentVisibleEvent={RecentVisibleEvent}.",
            card.SerialNumber,
            LevelFieldKey,
            progress.LevelShortText,
            levelChangeMessage is not null,
            walletContext.PointsExpiring is not null,
            walletContext.MonthlyProduct is not null,
            walletContext.BirthdayBenefit is not null,
            walletContext.PointCampaign is not null,
            walletContext.RecentVisibleEvent?.Type.ToString() ?? "none");

        return new
        {
            formatVersion = 1,
            passTypeIdentifier = _options.PassTypeIdentifier,
            serialNumber = card.SerialNumber,
            teamIdentifier = _options.TeamIdentifier,
            webServiceURL = _options.WebServiceURL,
            authenticationToken = card.AuthenticationToken,
            organizationName = _options.OrganizationName,
            description = "Tarjeta de Lealtad K-Beauty",
            backgroundColor = "rgb(250,248,244)",
            foregroundColor = "rgb(28,28,28)",
            labelColor = "rgb(132,124,120)",
            storeCard = new
            {
                primaryFields = new[]
                {
                    new
                    {
                        key = "name",
                        label = string.Empty,
                        value = displayName,
                        textAlignment = "PKTextAlignmentCenter"
                    }
                },
                secondaryFields = Array.Empty<object>(),
                auxiliaryFields,
                backFields
            },
            barcodes = new[]
            {
                new
                {
                    format = "PKBarcodeFormatQR",
                    message = card.SerialNumber,
                    messageEncoding = "iso-8859-1",
                    altText = "Presenta este c\u00f3digo en caja"
                }
            }
        };
    }

    private object[] BuildAuxiliaryFields(
        PassProgress progress,
        string? levelChangeMessage,
        WalletNotificationContext walletContext)
    {
        var fields = new List<object>
        {
            new { key = "points", label = "PUNTOS", value = progress.PointsText }
        };

        if (string.IsNullOrWhiteSpace(levelChangeMessage))
        {
            fields.Add(new { key = LevelFieldKey, label = "NIVEL", value = progress.LevelShortText });
        }
        else
        {
            fields.Add(new
            {
                key = LevelFieldKey,
                label = "NIVEL",
                value = progress.LevelShortText,
                changeMessage = levelChangeMessage
            });
        }

        fields.Add(new { key = "next", label = "PR\u00d3XIMO", value = progress.NextLevelText });

        object? temporalField;
        TemporalFieldSelection selection;
        var source = "RecentVisibleEvent";

        if (walletContext.RecentVisibleEvent is not null)
        {
            temporalField = BuildRecentVisibleField(walletContext, out selection);
        }
        else
        {
            temporalField = BuildPermanentTemporalField(walletContext, out selection);
            source = temporalField is null ? "None" : "FallbackPermanent";
        }

        _logger.LogInformation(
            "Wallet temporal field selection: RecentVisibleEvent={RecentVisibleEvent}, TemporaryFieldSource={TemporaryFieldSource}, TemporaryFieldKey={TemporaryFieldKey}, TemporaryFieldValue={TemporaryFieldValue}, ChangeMessageIncluded={ChangeMessageIncluded}.",
            walletContext.RecentVisibleEvent is null ? "null" : walletContext.RecentVisibleEvent.Type.ToString(),
            source,
            selection.Key ?? "none",
            selection.Value ?? "none",
            selection.ChangeMessageIncluded);

        _logger.LogInformation(
            "custom_message: value={CustomMessageValue}, changeMessage={CustomMessageChangeMessage}, included={CustomMessageIncluded}.",
            walletContext.CustomMessage?.ShortMessage ?? "null",
            walletContext.CustomMessage?.ChangeMessage ?? "null",
            temporalField is not null && string.Equals(selection.Key, CustomMessageFieldKey, StringComparison.Ordinal));

        if (temporalField is not null)
            fields.Add(temporalField);

        return fields.ToArray();
    }

    private static object? BuildRecentVisibleField(
        WalletNotificationContext walletContext,
        out TemporalFieldSelection selection)
    {
        selection = TemporalFieldSelection.None;
        if (walletContext.RecentVisibleEvent is null)
            return null;

        return walletContext.RecentVisibleEvent.Type switch
        {
            NotificationType.LevelChanged => null,
            NotificationType.BirthdayBenefitStarted when walletContext.BirthdayBenefit is not null =>
                BuildBirthdayBenefitField(walletContext.BirthdayBenefit, includeChangeMessage: true, out selection),
            NotificationType.PointsExpiring when walletContext.PointsExpiring is not null =>
                BuildPointsExpiringField(walletContext.PointsExpiring, includeChangeMessage: true, out selection),
            NotificationType.MonthlyProductStarted when walletContext.MonthlyProduct is not null =>
                BuildMonthlyProductField(walletContext.MonthlyProduct, includeChangeMessage: true, out selection),
            NotificationType.PointCampaignStarted when walletContext.PointCampaign is not null =>
                BuildPointCampaignField(walletContext.PointCampaign, includeChangeMessage: true, out selection),
            NotificationType.Custom when walletContext.CustomMessage is not null =>
                BuildCustomMessageField(walletContext.CustomMessage, includeChangeMessage: true, out selection),
            _ => null
        };
    }

    private static object? BuildPermanentTemporalField(
        WalletNotificationContext walletContext,
        out TemporalFieldSelection selection)
    {
        selection = TemporalFieldSelection.None;
        if (walletContext.PointsExpiring is not null)
            return BuildPointsExpiringField(walletContext.PointsExpiring, includeChangeMessage: false, out selection);

        if (walletContext.BirthdayBenefit is not null)
            return BuildBirthdayBenefitField(walletContext.BirthdayBenefit, includeChangeMessage: false, out selection);

        if (walletContext.PointCampaign is not null)
            return BuildPointCampaignField(walletContext.PointCampaign, includeChangeMessage: false, out selection);

        if (walletContext.MonthlyProduct is not null)
            return BuildMonthlyProductField(walletContext.MonthlyProduct, includeChangeMessage: false, out selection);

        return null;
    }

    private static object BuildPointsExpiringField(
        WalletPointsExpiringMessage pointsExpiring,
        bool includeChangeMessage,
        out TemporalFieldSelection selection)
    {
        selection = new TemporalFieldSelection(PointsExpiringFieldKey, pointsExpiring.Value, includeChangeMessage);
        return includeChangeMessage
            ? new
            {
                key = PointsExpiringFieldKey,
                label = "POR EXPIRAR",
                value = pointsExpiring.Value,
                changeMessage = pointsExpiring.ChangeMessage
            }
            : new
            {
                key = PointsExpiringFieldKey,
                label = "POR EXPIRAR",
                value = pointsExpiring.Value
            };
    }

    private static object BuildCustomMessageField(
        WalletCustomMessage customMessage,
        bool includeChangeMessage,
        out TemporalFieldSelection selection)
    {
        selection = new TemporalFieldSelection(
            CustomMessageFieldKey,
            customMessage.ShortMessage,
            includeChangeMessage);

        return includeChangeMessage
            ? new
            {
                key = CustomMessageFieldKey,
                label = "NOVEDAD",
                value = customMessage.ShortMessage,
                changeMessage = customMessage.ChangeMessage
            }
            : new
            {
                key = CustomMessageFieldKey,
                label = "NOVEDAD",
                value = customMessage.ShortMessage
            };
    }

    private static object BuildBirthdayBenefitField(
        WalletBirthdayBenefitMessage birthdayBenefit,
        bool includeChangeMessage,
        out TemporalFieldSelection selection)
    {
        selection = new TemporalFieldSelection(BirthdayBenefitFieldKey, birthdayBenefit.Value, includeChangeMessage);
        return includeChangeMessage
            ? new
            {
                key = BirthdayBenefitFieldKey,
                label = "CUMPLEA\u00d1OS",
                value = birthdayBenefit.Value,
                changeMessage = birthdayBenefit.ChangeMessage
            }
            : new
            {
                key = BirthdayBenefitFieldKey,
                label = "CUMPLEA\u00d1OS",
                value = birthdayBenefit.Value
            };
    }

    private static object BuildMonthlyProductField(
        WalletMonthlyProductMessage monthlyProduct,
        bool includeChangeMessage,
        out TemporalFieldSelection selection)
    {
        selection = new TemporalFieldSelection(MonthlyProductFieldKey, monthlyProduct.Value, includeChangeMessage);
        return includeChangeMessage
            ? new
            {
                key = MonthlyProductFieldKey,
                label = "PRODUCTO DEL MES",
                value = monthlyProduct.Value,
                changeMessage = monthlyProduct.ChangeMessage
            }
            : new
            {
                key = MonthlyProductFieldKey,
                label = "PRODUCTO DEL MES",
                value = monthlyProduct.Value
            };
    }

    private static object BuildPointCampaignField(
        WalletPointCampaignMessage pointCampaign,
        bool includeChangeMessage,
        out TemporalFieldSelection selection)
    {
        selection = new TemporalFieldSelection(PointCampaignFieldKey, pointCampaign.Value, includeChangeMessage);
        return includeChangeMessage
            ? new
            {
                key = PointCampaignFieldKey,
                label = "PROMOCI\u00d3N",
                value = pointCampaign.Value,
                changeMessage = pointCampaign.ChangeMessage
            }
            : new
            {
                key = PointCampaignFieldKey,
                label = "PROMOCI\u00d3N",
                value = pointCampaign.Value
            };
    }

    private string? BuildLevelChangeMessage(LoyaltyCard card, WalletNotificationMessage? walletMessage)
    {
        if (walletMessage is null || walletMessage.Type != NotificationType.LevelChanged)
            return null;

        if (!TryReadLevelChangeMetadata(walletMessage.MetadataJson, out var previousLevel, out var newLevel, out var isUpgrade))
        {
            _logger.LogDebug(
                "Level changeMessage skipped for card {CardId}: missing or invalid metadata on notification {NotificationId}.",
                card.Id,
                walletMessage.Id);
            return null;
        }

        if (!isUpgrade)
            return null;

        if (!string.Equals(newLevel, card.Level, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug(
                "Level changeMessage skipped for card {CardId}: notification newLevel={NotificationLevel}, currentLevel={CurrentLevel}.",
                card.Id,
                newLevel,
                card.Level);
            return null;
        }

        _logger.LogInformation(
            "Level changeMessage included for card {CardId}: {PreviousLevel} -> {NewLevel}.",
            card.Id,
            previousLevel,
            newLevel);
        return "\ud83c\udf89 Ahora eres cliente %@";
    }

    private static bool TryReadLevelChangeMetadata(
        string? metadataJson,
        out string? previousLevel,
        out string? newLevel,
        out bool isUpgrade)
    {
        previousLevel = null;
        newLevel = null;
        isUpgrade = false;

        if (string.IsNullOrWhiteSpace(metadataJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            var root = doc.RootElement;
            previousLevel = root.TryGetProperty("previousLevel", out var previous)
                ? previous.GetString()
                : null;
            newLevel = root.TryGetProperty("newLevel", out var next)
                ? next.GetString()
                : null;
            isUpgrade = root.TryGetProperty("isUpgrade", out var upgrade) && upgrade.ValueKind == JsonValueKind.True;

            return !string.IsNullOrWhiteSpace(newLevel);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static object[] BuildBackFields(
        PassProgress progress,
        WalletNotificationMessage? walletMessage,
        WalletMonthlyProductMessage? monthlyProduct,
        WalletBirthdayBenefitMessage? birthdayBenefit,
        WalletPointCampaignMessage? pointCampaign,
        WalletCustomMessage? customMessage)
    {
        var fields = new List<object>();
        if (customMessage is not null)
        {
            fields.Add(new
            {
                key = "custom_message_detail",
                label = "NOVEDAD",
                value = $"{customMessage.Title}\n\n{customMessage.LongMessage}"
            });
        }

        if (walletMessage is not null)
        {
            fields.Add(new
            {
                key = "news",
                label = "NOVEDADES",
                value = walletMessage.Message
            });
        }

        if (monthlyProduct is not null)
        {
            fields.Add(new
            {
                key = "monthly_product_detail",
                label = "PRODUCTO DEL MES",
                value = monthlyProduct.BackValue
            });
        }

        if (birthdayBenefit is not null)
        {
            fields.Add(new
            {
                key = "birthday_benefit_detail",
                label = "BENEFICIO DE CUMPLEA\u00d1OS",
                value = birthdayBenefit.BackValue
            });
        }

        if (pointCampaign is not null)
        {
            fields.Add(new
            {
                key = "point_campaign_detail",
                label = "CAMPA\u00d1A ACTIVA",
                value = pointCampaign.BackValue
            });
        }

        fields.Add(new
        {
            key = "benefits",
            label = "Beneficios",
            value = "\u2022 Acumula puntos en cada compra.\n\n\u2022 Desbloquea recompensas exclusivas.\n\n\u2022 Accede a beneficios seg\u00fan tu nivel."
        });
        fields.Add(new
        {
            key = "progress",
            label = "Progreso",
            value = $"Nivel actual\n{progress.LevelShortText}\n\nPr\u00f3ximo nivel\n{progress.NextLevelText}\n\nPuntos restantes\n{progress.RemainingPointsText}"
        });
        fields.Add(new
        {
            key = "contact",
            label = string.Empty,
            value = "@kbeauty_mx\n\nkbeautymx.com\n\n+52 646 238 6962"
        });

        return fields.ToArray();
    }

    private static string GetWalletDisplayName(Customer customer)
    {
        var fullName = customer.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            return "Cliente K-Beauty";

        var firstName = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstName)
            ? "Cliente K-Beauty"
            : firstName;
    }

    private static PassProgress BuildLevelProgress(LoyaltyCard card)
    {
        var currentPoints = Math.Max(0, card.CurrentPoints);
        var level = card.Level;
        var levelDisplay = $"{level} Member \u2728";
        var levelShortText = $"{level} \u2728";

        if (string.Equals(level, LoyaltyConstants.Levels.Radiance, StringComparison.Ordinal) ||
            currentPoints >= LoyaltyConstants.Defaults.LevelRadianceMin)
        {
            return new PassProgress(
                levelDisplay,
                levelShortText,
                $"{currentPoints} pts",
                "\u2b50 M\u00e1ximo",
                "0 pts");
        }

        var nextLevel = string.Equals(level, LoyaltyConstants.Levels.Glow, StringComparison.Ordinal)
            ? LoyaltyConstants.Levels.Radiance
            : LoyaltyConstants.Levels.Glow;
        var targetPoints = string.Equals(nextLevel, LoyaltyConstants.Levels.Radiance, StringComparison.Ordinal)
            ? LoyaltyConstants.Defaults.LevelRadianceMin
            : LoyaltyConstants.Defaults.LevelGlowMin;
        var remainingPoints = Math.Max(0, targetPoints - currentPoints);

        return new PassProgress(
            levelDisplay,
            levelShortText,
            $"{currentPoints} pts",
            nextLevel,
            $"{remainingPoints} pts");
    }

    private IReadOnlyList<PassAsset> LoadPassAssets()
    {
        var assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets", "AppleWallet");
        var assets = new List<PassAsset>(PassAssetSpecs.Length);

        foreach (var spec in PassAssetSpecs)
        {
            var path = Path.Combine(assetsDir, spec.Name);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"Asset requerido para Apple Wallet no existe. Ruta esperada: {path}. " +
                    "Regeneralo con scripts/generate-wallet-assets.ps1.",
                    path);

            var bytes = File.ReadAllBytes(path);
            var (width, height) = ReadPngDimensions(bytes, path);

            if (width != spec.Width || height != spec.Height)
                throw new InvalidOperationException(
                    $"Asset Apple Wallet '{path}' tiene dimensiones {width}x{height}; " +
                    $"se esperaba {spec.Width}x{spec.Height}. " +
                    "Regeneralo con scripts/generate-wallet-assets.ps1.");

            _logger.LogInformation(
                "Asset Apple Wallet agregado: {File} ({Width}x{Height}, {Bytes} bytes)",
                spec.Name,
                width,
                height,
                bytes.Length);

            assets.Add(new PassAsset(spec.Name, bytes));
        }

        return assets;
    }

    private async Task<byte[]> SignManifestAsync(byte[] manifestBytes, CancellationToken ct)
    {
        var certBytes = await _secrets.GetPassCertificateBytesAsync(ct);
        var certPassword = await _secrets.GetPassCertificatePasswordAsync(ct);

        var certCollection = X509CertificateLoader.LoadPkcs12Collection(
            certBytes,
            certPassword,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);

        _logger.LogInformation(
            "Apple Wallet PKCS#12 loaded using strategy {Strategy}. CertificateCount={CertificateCount}, HasPrivateKey={HasPrivateKey}.",
            "X509CertificateLoader/EphemeralKeySet+Exportable",
            certCollection.Count,
            certCollection.OfType<X509Certificate2>().Any(c => c.HasPrivateKey));

        var passCert = certCollection
            .OfType<X509Certificate2>()
            .FirstOrDefault(c => c.HasPrivateKey)
            ?? throw new InvalidOperationException(
                "El .p12 de Apple Wallet no contiene un certificado con llave privada.");

        var wwdrCert = certCollection
            .OfType<X509Certificate2>()
            .FirstOrDefault(IsWwdrG4Certificate);

        wwdrCert ??= LoadBundledWwdrCertificate();

        if (wwdrCert is null)
        {
            var wwdrBytes = await _secrets.GetWwdrCertificateBytesAsync(ct);
            if (wwdrBytes is not null)
                wwdrCert = LoadCertificate(wwdrBytes);
        }

        if (wwdrCert is null || !IsWwdrG4Certificate(wwdrCert))
        {
            throw new InvalidOperationException(
                "No se encontro el certificado intermedio Apple WWDR G4. " +
                "Incluyelo dentro del .p12 o configura Apple:WwdrCertificatePath con el certificado WWDR G4.");
        }

        var contentInfo = new ContentInfo(manifestBytes);
        var cms = new SignedCms(contentInfo, detached: true);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, passCert)
        {
            IncludeOption = X509IncludeOption.EndCertOnly
        };
        signer.Certificates.Add(wwdrCert);
        signer.SignedAttributes.Add(new Pkcs9SigningTime(DateTime.UtcNow));

        cms.ComputeSignature(signer);
        var signatureBytes = cms.Encode();

        LogSignatureDebug(signatureBytes, passCert, wwdrCert);
        return signatureBytes;
    }

    private void LogSignatureDebug(
        byte[] signatureBytes,
        X509Certificate2 passCert,
        X509Certificate2 wwdrCert)
    {
        var signedCms = new SignedCms();
        signedCms.Decode(signatureBytes);

        var signerInfo = signedCms.SignerInfos[0];
        var signedAttributeOids = signerInfo.SignedAttributes
            .Cast<CryptographicAttributeObject>()
            .Select(a => $"{a.Oid?.FriendlyName ?? "unknown"} ({a.Oid?.Value})")
            .ToArray();

        var hasContentType = HasSignedAttribute(signerInfo, "1.2.840.113549.1.9.3");
        var hasMessageDigest = HasSignedAttribute(signerInfo, "1.2.840.113549.1.9.4");
        var hasSigningTime = HasSignedAttribute(signerInfo, "1.2.840.113549.1.9.5");

        _logger.LogInformation(
            "Firma PKCS#7 Apple Wallet generada. Detached={Detached}, PassCert='{PassSubject}', WWDR='{WwdrSubject}', CertsIncluidos={CertificateCount}, SignedAttributes=[{Attributes}]",
            true,
            passCert.Subject,
            wwdrCert.Subject,
            signedCms.Certificates.Count,
            string.Join(", ", signedAttributeOids));

        _logger.LogDebug(
            "Firma PKCS#7 signed attributes: contentType={ContentType}, messageDigest={MessageDigest}, signingTime={SigningTime}",
            hasContentType,
            hasMessageDigest,
            hasSigningTime);
    }

    private static bool HasSignedAttribute(SignerInfo signerInfo, string oid) =>
        signerInfo.SignedAttributes
            .Cast<CryptographicAttributeObject>()
            .Any(a => string.Equals(a.Oid?.Value, oid, StringComparison.Ordinal));

    private static X509Certificate2 LoadCertificate(byte[] bytes)
    {
        var text = Encoding.ASCII.GetString(bytes);
        return text.Contains("-----BEGIN CERTIFICATE-----", StringComparison.Ordinal)
            ? X509Certificate2.CreateFromPem(text)
            : X509CertificateLoader.LoadCertificate(bytes);
    }

    private static X509Certificate2? LoadBundledWwdrCertificate()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Certificates", "AppleWWDRCAG4.cer");
        return File.Exists(path)
            ? X509CertificateLoader.LoadCertificateFromFile(path)
            : null;
    }

    private static bool IsWwdrG4Certificate(X509Certificate2 cert) =>
        cert.Subject.Contains("Apple Worldwide Developer Relations", StringComparison.OrdinalIgnoreCase) &&
        (cert.Subject.Contains("G4", StringComparison.OrdinalIgnoreCase) ||
         cert.Issuer.Contains("G4", StringComparison.OrdinalIgnoreCase));

    private static void EnsureRequiredOption(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Falta configuracion requerida '{key}' para generar pases Apple Wallet reales.");
    }

    private static string Sha1Hex(byte[] data)
    {
        var hash = SHA1.HashData(data);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.AppendFormat("{0:x2}", b);
        return sb.ToString();
    }

    private static void AddZipEntry(ZipArchive zip, string name, byte[] content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    private static (int Width, int Height) ReadPngDimensions(byte[] bytes, string path)
    {
        if (bytes.Length < 24 ||
            bytes[0] != 0x89 ||
            bytes[1] != 0x50 ||
            bytes[2] != 0x4E ||
            bytes[3] != 0x47)
        {
            throw new InvalidOperationException($"Asset Apple Wallet '{path}' no es un PNG valido.");
        }

        return (ReadBigEndianInt32(bytes, 16), ReadBigEndianInt32(bytes, 20));
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset) =>
        (bytes[offset] << 24) |
        (bytes[offset + 1] << 16) |
        (bytes[offset + 2] << 8) |
        bytes[offset + 3];

    private sealed record PassAsset(string Name, byte[] Bytes);

    private sealed record PassAssetSpec(string Name, int Width, int Height);

    private sealed record PassProgress(
        string LevelDisplay,
        string LevelShortText,
        string PointsText,
        string NextLevelText,
        string RemainingPointsText);

    private sealed record TemporalFieldSelection(
        string? Key,
        string? Value,
        bool ChangeMessageIncluded)
    {
        public static TemporalFieldSelection None { get; } = new(null, null, false);
    }
}
