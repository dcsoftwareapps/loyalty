using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Common.Wallet;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Services.GoogleWallet;
using Microsoft.Extensions.Options;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class GoogleWalletMultiTenantIsolationTests
{
    private static readonly Guid KBeautyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TamalitosId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly GoogleWalletOptions WalletOptions = new()
    {
        IssuerId = "issuer-test",
        ClassSuffix = "loyalty",
        ObjectIdPrefix = "member",
        ProgramName = "GLOBAL MUST NOT BE USED",
        IssuerName = "GLOBAL MUST NOT BE USED",
        LogoUri = "https://api.example.test/api/wallet-assets/apple/logo@3x.png",
        HexBackgroundColor = "#FFFFFF"
    };

    [Fact]
    public void Tenant_payloads_use_only_their_own_branding_and_ids()
    {
        var ids = new GoogleWalletIdGenerator();
        var mapper = new GoogleWalletObjectMapper();
        var kbeautyClassId = ids.BuildClassId(WalletOptions, KBeautyId);
        var tamalitosClassId = ids.BuildClassId(WalletOptions, TamalitosId);
        var kbeauty = mapper.ToClassData(kbeautyClassId, WalletOptions, Branding(KBeautyId, "kbeauty", "KBeauty", "#111111"));
        var tamalitos = mapper.ToClassData(tamalitosClassId, WalletOptions, Branding(TamalitosId, "tamalitos", "Tamalitos", "#F59E0B"));

        Assert.NotEqual(kbeauty.Id, tamalitos.Id);
        Assert.Equal("KBeauty", kbeauty.ProgramName);
        Assert.Equal("#111111", kbeauty.HexBackgroundColor);
        Assert.Contains(KBeautyId.ToString("D"), kbeauty.LogoUri);
        Assert.Equal("Tamalitos", tamalitos.ProgramName);
        Assert.Equal("Tamalitos", tamalitos.IssuerName);
        Assert.Equal("#F59E0B", tamalitos.HexBackgroundColor);
        Assert.Contains(TamalitosId.ToString("D"), tamalitos.LogoUri);
        Assert.DoesNotContain("KBeauty", JsonSerializer.Serialize(mapper.ToClassPayload(tamalitos)), StringComparison.OrdinalIgnoreCase);

        var kbeautyObject = ids.BuildObjectId(WalletOptions, KBeautyId, "MEMBER-001");
        var tamalitosObject = ids.BuildObjectId(WalletOptions, TamalitosId, "MEMBER-001");
        Assert.NotEqual(kbeautyObject, tamalitosObject);
    }

    [Fact]
    public void Creation_order_and_branding_updates_do_not_cross_tenants()
    {
        var ids = new GoogleWalletIdGenerator();
        var mapper = new GoogleWalletObjectMapper();
        var originalA = mapper.ToClassData(ids.BuildClassId(WalletOptions, KBeautyId), WalletOptions, Branding(KBeautyId, "kbeauty", "KBeauty", "#111111"));

        _ = mapper.ToClassData(ids.BuildClassId(WalletOptions, TamalitosId), WalletOptions, Branding(TamalitosId, "tamalitos", "Tamalitos v2", "#00AA00"));
        var afterB = mapper.ToClassData(ids.BuildClassId(WalletOptions, KBeautyId), WalletOptions, Branding(KBeautyId, "kbeauty", "KBeauty", "#111111"));
        Assert.Equal(originalA, afterB);

        var firstB = mapper.ToClassData(ids.BuildClassId(WalletOptions, TamalitosId), WalletOptions, Branding(TamalitosId, "tamalitos", "Tamalitos", "#F59E0B"));
        var secondA = mapper.ToClassData(ids.BuildClassId(WalletOptions, KBeautyId), WalletOptions, Branding(KBeautyId, "kbeauty", "KBeauty", "#111111"));
        Assert.Equal("Tamalitos", firstB.ProgramName);
        Assert.Equal("KBeauty", secondA.ProgramName);
    }

    [Fact]
    public void Tenant_without_custom_wallet_fields_uses_neutral_tenant_branding_not_another_tenant()
    {
        var ids = new GoogleWalletIdGenerator();
        var mapper = new GoogleWalletObjectMapper();
        var neutral = Branding(TamalitosId, "tamalitos", "Tamalitos", "#7C3AED");

        var payload = mapper.ToClassPayload(mapper.ToClassData(ids.BuildClassId(WalletOptions, TamalitosId), WalletOptions, neutral));
        var json = JsonSerializer.Serialize(payload);

        Assert.Contains("Tamalitos", json);
        Assert.DoesNotContain("KBeauty", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(TamalitosId.ToString("D"), json);
    }

    [Fact]
    public async Task Parallel_generation_never_swaps_tenant_branding_or_ids()
    {
        var ids = new GoogleWalletIdGenerator();
        var mapper = new GoogleWalletObjectMapper();
        var failures = new ConcurrentQueue<string>();

        var tasks = Enumerable.Range(0, 100).Select(async index =>
        {
            await Task.Yield();
            var tenantId = index % 2 == 0 ? KBeautyId : TamalitosId;
            var name = index % 2 == 0 ? "KBeauty" : "Tamalitos";
            var color = index % 2 == 0 ? "#111111" : "#F59E0B";
            var branding = Branding(tenantId, name.ToLowerInvariant(), name, color);
            var classId = ids.BuildClassId(WalletOptions, tenantId);
            var objectId = ids.BuildObjectId(WalletOptions, tenantId, $"MEMBER-{index}");
            var data = mapper.ToClassData(classId, WalletOptions, branding);

            if (!data.Id.EndsWith(tenantId.ToString("N"), StringComparison.Ordinal) ||
                data.ProgramName != name ||
                data.HexBackgroundColor != color ||
                data.LogoUri?.Contains(tenantId.ToString("D"), StringComparison.Ordinal) != true ||
                !objectId.Contains(tenantId.ToString("N")[..12], StringComparison.Ordinal))
            {
                failures.Enqueue($"{tenantId}:{classId}:{objectId}");
            }
        });

        await Task.WhenAll(tasks);
        Assert.Empty(failures);
    }

    [Fact]
    public void Tamalitos_save_jwt_references_only_tamalitos_class_and_object()
    {
        var ids = new GoogleWalletIdGenerator();
        var classId = ids.BuildClassId(WalletOptions, TamalitosId);
        var objectId = ids.BuildObjectId(WalletOptions, TamalitosId, "TAM-001");
        var member = Member(TamalitosId, "TAM-001");
        var walletObject = new GoogleWalletObjectMapper().ToObjectData(objectId, classId, member);

        using var rsa = RSA.Create(2048);
        var factory = new GoogleWalletJwtFactory(Options.Create(new GoogleWalletOptions
        {
            SaveUrlBase = "https://pay.google.com/gp/v/save",
            Origins = ["https://admin.example.test"]
        }));
        var url = factory.CreateSaveUrl(
            new GoogleWalletCredentials("wallet@example.test", rsa.ExportPkcs8PrivateKeyPem(), "https://oauth2.googleapis.com/token"),
            walletObject,
            DateTime.UtcNow);
        var jwt = url.Split('/').Last();
        using var payload = JsonDocument.Parse(Decode(jwt.Split('.')[1]));
        var reference = payload.RootElement.GetProperty("payload").GetProperty("loyaltyObjects")[0];

        Assert.Equal(classId, reference.GetProperty("classId").GetString());
        Assert.Equal(objectId, reference.GetProperty("id").GetString());
        Assert.Contains(TamalitosId.ToString("N"), classId);
        Assert.DoesNotContain(KBeautyId.ToString("N"), payload.RootElement.GetRawText());
    }

    private static TenantWalletBrandingDto Branding(Guid tenantId, string slug, string name, string color) => new(
        tenantId, slug, name, name, $"Tarjeta {name}", "rgb(0,0,0)", "rgb(255,255,255)",
        "rgb(255,255,255)", color, null, null, "LoyaltyCloud", $"Cliente {name}", false, false);

    private static MemberWalletData Member(Guid tenantId, string serial) => new(
        tenantId, Guid.NewGuid(), Guid.NewGuid(), serial, "Cliente", null, null, 10, 10, "Nivel",
        DateTime.UtcNow, DateTime.UtcNow, true, serial, "Cliente", "10 pts", "Nivel", "Máximo", "—",
        "Presenta este código en caja");

    private static string Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
