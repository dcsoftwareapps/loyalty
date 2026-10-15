using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SixLabors.ImageSharp;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "StampVisualAssets")]
public sealed class StampSignedPassTests
{
    [Theory]
    [InlineData(2, false)] [InlineData(10, false)] [InlineData(3, true)]
    public async Task Production_generator_signs_complete_stamps_package_without_any_points_reads(int progress, bool hasMessage)
    {
        var id = Guid.NewGuid(); var now = DateTime.UtcNow;
        var context = new TenantContext(); context.SetTenant(id, "test-stamps");
        var customer = new Customer(Guid.NewGuid(), id, "Ana Test", "internal@example.local", new DateTime(1990, 1, 1), now, "6465550001");
        var card = new LoyaltyCard(Guid.NewGuid(), id, customer.Id, "ST-TEST0001", now);
        var branding = new TenantWalletBrandingDto(id, "test-stamps", "Test", "Test", "Tarjeta de sellos",
            "rgb(255,255,255)", "rgb(0,0,0)", "rgb(45,58,64)", "#FFFFFF", null, null, 100, "CustomerName", null,
            "", "Cliente", true, false);
        var brand = new Mock<ITenantWalletBrandingReadService>();
        brand.Setup(x => x.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(branding);
        var program = new Mock<ITenantProgramReadService>();
        program.Setup(x => x.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(LoyaltyProgramType.Stamps);
        var projection = new StampWalletProjection(Guid.NewGuid(), Guid.NewGuid(), progress, 10, null, null,
            [new(5, Guid.NewGuid(), null), new(10, Guid.NewGuid(), null)]);
        var stamps = new Mock<IStampWalletProjectionService>();
        stamps.Setup(x => x.ReadAsync(card.SerialNumber, It.IsAny<CancellationToken>())).ReturnsAsync(projection);
        stamps.Setup(x => x.Render(projection, branding.BackgroundColor)).Returns(StampStripRenderer.Render(projection, branding.BackgroundColor));
        var pointsNotifications = new Mock<IWalletNotificationReadService>(MockBehavior.Strict);
        var message = hasMessage ? new WalletCustomMessage(Guid.NewGuid(), "Aviso", "Mensaje activo", "Detalle del mensaje", now.AddDays(1), "\uD83D\uDCE3 %@") : null;
        pointsNotifications.Setup(x => x.GetActiveContextAsync(card.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WalletNotificationContext(null, null, null, null, null, null, message, null,
                message is null ? null : new WalletRecentVisibleEvent(message.NotificationId, NotificationType.Custom, now, now, now.AddDays(1))));
        var levels = new Mock<ITenantLoyaltyLevelReadService>(MockBehavior.Strict);
        var transactions = new Mock<IPointTransactionRepository>(MockBehavior.Strict);
        var options = Options.Create(new ApplePassOptions
        {
            PassTypeIdentifier = "pass.test.stamps", TeamIdentifier = "TESTTEAM", OrganizationName = "Test",
            WebServiceURL = "https://test.invalid"
        });
        var assets = new TenantWalletAssetProvider(Options.Create(new AzureStorageOptions()), NullLogger<TenantWalletAssetProvider>.Instance);
        var generator = new PassGeneratorService(new TestSecrets(), pointsNotifications.Object, brand.Object, assets, levels.Object,
            context, transactions.Object, new Mock<ILevelProgressService>(MockBehavior.Strict).Object,
            new Mock<IDateTimeProvider>().Object, options, NullLogger<PassGeneratorService>.Instance, program.Object, stamps.Object);
        var bytes = await generator.GeneratePassAsync(card, customer);
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        byte[] Read(string name)
        {
            using var source = zip.GetEntry(name)!.Open(); using var output = new MemoryStream(); source.CopyTo(output); return output.ToArray();
        }
        using var json = JsonDocument.Parse(Read("pass.json"));
        var root = json.RootElement;
        Assert.Equal(card.SerialNumber, root.GetProperty("serialNumber").GetString());
        Assert.Equal(card.AuthenticationToken, root.GetProperty("authenticationToken").GetString());
        Assert.Equal(card.SerialNumber, root.GetProperty("barcodes")[0].GetProperty("message").GetString());
        Assert.Equal($"{progress} / 10", root.GetProperty("storeCard").GetProperty("auxiliaryFields")[0].GetProperty("value").GetString());
        Assert.Equal("SELLOS", root.GetProperty("storeCard").GetProperty("auxiliaryFields")[0].GetProperty("label").GetString());
        Assert.False(root.GetProperty("storeCard").GetProperty("auxiliaryFields")[0].TryGetProperty("changeMessage", out _));
        Assert.Equal("https://test.invalid", root.GetProperty("webServiceURL").GetString());
        var messageFields = root.GetProperty("storeCard").GetProperty("auxiliaryFields").EnumerateArray()
            .Where(x => x.GetProperty("key").GetString() == "custom_message").ToArray();
        if (hasMessage)
        {
            var field = Assert.Single(messageFields);
            Assert.Equal(string.Empty, field.GetProperty("label").GetString());
            Assert.Equal(message!.ShortMessage, field.GetProperty("value").GetString());
            Assert.Equal(message.ChangeMessage, field.GetProperty("changeMessage").GetString());
            Assert.Contains(message.LongMessage, root.GetProperty("storeCard").GetProperty("backFields").GetRawText());
        }
        else
        {
            Assert.Empty(messageFields);
            Assert.DoesNotContain("custom_message_detail", root.GetRawText());
        }
        foreach (var label in new[] { "PUNTOS", "NIVEL", "PRÓXIMO", "FALTAN" }) Assert.DoesNotContain(label, root.GetRawText());
        Assert.Empty(root.GetProperty("storeCard").GetProperty("primaryFields").EnumerateArray());
        using var manifest = JsonDocument.Parse(Read("manifest.json"));
        foreach (var entry in manifest.RootElement.EnumerateObject())
            Assert.Equal(entry.Value.GetString(), Convert.ToHexString(SHA1.HashData(Read(entry.Name))).ToLowerInvariant());
        var cms = new SignedCms(new ContentInfo(Read("manifest.json")), detached: true);
        cms.Decode(Read("signature")); cms.CheckSignature(verifySignatureOnly: true);
        Assert.Single(cms.SignerInfos.Cast<SignerInfo>());
        foreach (var name in new[] { "icon.png", "icon@2x.png", "icon@3x.png", "logo.png", "logo@2x.png", "logo@3x.png" }) Assert.NotNull(zip.GetEntry(name));
        for (var scale = 1; scale <= 3; scale++)
        {
            using var image = Image.Load(Read(scale == 1 ? "strip.png" : $"strip@{scale}x.png"));
            Assert.Equal(375 * scale, image.Width); Assert.Equal(144 * scale, image.Height);
        }
        pointsNotifications.Verify(x => x.GetActiveContextAsync(card.Id, It.IsAny<CancellationToken>()), Times.Once);
        pointsNotifications.VerifyNoOtherCalls(); levels.VerifyNoOtherCalls(); transactions.VerifyNoOtherCalls();
        // Test-only certificate: cryptographically signed package, not installable Apple-issued credentials.
        var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/stamps-wallet-production-tests"));
        Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, $"test-signed-{progress}-of-10.pkpass"), bytes);
    }

    private sealed class TestSecrets : IAppleWalletSecretsProvider
    {
        public Task<byte[]> GetPassCertificateBytesAsync(CancellationToken ct)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=Stamps Package Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            return Task.FromResult(certificate.Export(X509ContentType.Pfx, "test-only"));
        }
        public Task<string> GetPassCertificatePasswordAsync(CancellationToken ct) => Task.FromResult("test-only");
        public Task<byte[]?> GetWwdrCertificateBytesAsync(CancellationToken ct) => Task.FromResult<byte[]?>(null);
        public Task<string> GetApnPrivateKeyPemAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<string> GetApnKeyIdAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<string> GetApnTeamIdAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
