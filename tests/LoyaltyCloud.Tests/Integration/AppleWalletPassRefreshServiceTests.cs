using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;
using Env = LoyaltyCloud.Tests.Integration.TenantProvisioningTests.ProvisioningTestEnvironment;

namespace LoyaltyCloud.Tests.Integration;

public sealed class AppleWalletPassRefreshServiceTests
{
    [Fact]
    public async Task Points_refresh_selects_only_registered_devices_for_the_target_card()
    {
        var clock = new Clock();
        var apn = CreateApn();
        await using var env = await CreateEnvironmentAsync(clock, apn);
        var member = await CreateMemberAsync(env, "wallet-points", LoyaltyProgramType.Points);
        var unrelated = await JoinMemberAsync(env, member.TenantId, member.Slug, "6465550002");
        await RegisterAsync(env, member, "points-one", "points-two");
        await RegisterAsync(env, unrelated, "unrelated");
        clock.UtcNow = clock.UtcNow.AddMinutes(10);

        var result = await RefreshAsync(env, member, PassUpdateReason.PointsAdded);

        Assert.Equal(member.TenantId, result.TenantId);
        Assert.Equal(new[] { member.Serial }, result.SerialNumbers);
        Assert.Equal(1, result.CardsTouched);
        Assert.Equal(2, result.DevicesFound);
        Assert.Equal(2, result.PushesAttempted);
        Assert.Equal(2, result.PushesAccepted);
        Assert.Equal(0, result.PushesFailed);
        Assert.True(result.AllAccepted);
        VerifyPush(apn, "points-one", PassUpdateReason.PointsAdded);
        VerifyPush(apn, "points-two", PassUpdateReason.PointsAdded);
        apn.VerifyNoOtherCalls();
        await AssertCardAsync(env, member, clock.UtcNow);
        await AssertCardAsync(env, unrelated, Clock.Initial);
    }

    [Fact]
    public async Task Committed_stamp_earn_refreshes_all_registered_devices_and_preserves_its_event()
    {
        var clock = new Clock();
        var apn = CreateApn();
        await using var env = await CreateEnvironmentAsync(clock, apn);
        var member = await CreateMemberAsync(env, "wallet-stamps", LoyaltyProgramType.Stamps);
        await RegisterAsync(env, member, "stamp-one", "stamp-two");
        clock.UtcNow = clock.UtcNow.AddMinutes(10);

        var earned = await env.SendAsOperatorAsync(member.TenantId, member.Slug,
            new EarnStampCommand(member.Serial, "wallet-refresh-earn"));

        Assert.True(earned.IsSuccess, earned.Error);
        Assert.Equal(1, earned.Value.State.CurrentStamps);
        VerifyPush(apn, "stamp-one", PassUpdateReason.StampEarned);
        VerifyPush(apn, "stamp-two", PassUpdateReason.StampEarned);
        apn.VerifyNoOtherCalls();
        Assert.True(await AnnouncementEnabledAsync(env, member));
        await AssertCardAsync(env, member, clock.UtcNow);
    }

    [Fact]
    public async Task Branding_refresh_suppresses_only_the_target_stamp_context()
    {
        var clock = new Clock();
        var apn = CreateApn();
        await using var env = await CreateEnvironmentAsync(clock, apn);
        var target = await CreateMemberAsync(env, "wallet-context-a", LoyaltyProgramType.Stamps);
        var unrelated = await JoinMemberAsync(env, target.TenantId, target.Slug, "6465550002");
        var foreign = await CreateMemberAsync(env, "wallet-context-b", LoyaltyProgramType.Stamps);
        foreach (var member in new[] { target, unrelated, foreign })
        {
            var earned = await env.SendAsOperatorAsync(member.TenantId, member.Slug,
                new EarnStampCommand(member.Serial, "context-" + member.Serial));
            Assert.True(earned.IsSuccess, earned.Error);
            Assert.True(await AnnouncementEnabledAsync(env, member));
        }
        await RegisterAsync(env, target, "target-context");
        await RegisterAsync(env, unrelated, "unrelated-context");
        await RegisterAsync(env, foreign, "foreign-context");
        apn.Invocations.Clear();
        clock.UtcNow = clock.UtcNow.AddMinutes(10);

        var result = await RefreshAsync(env, target, PassUpdateReason.BrandingUpdated);

        Assert.Equal(1, result.CardsTouched);
        Assert.Equal(1, result.PushesAccepted);
        VerifyPush(apn, "target-context", PassUpdateReason.BrandingUpdated);
        apn.VerifyNoOtherCalls();
        Assert.False(await AnnouncementEnabledAsync(env, target));
        Assert.True(await AnnouncementEnabledAsync(env, unrelated));
        Assert.True(await AnnouncementEnabledAsync(env, foreign));
        await AssertCardAsync(env, target, clock.UtcNow);
        await AssertCardAsync(env, unrelated, Clock.Initial);
        await AssertCardAsync(env, foreign, Clock.Initial);
    }

    [Fact]
    public async Task Refresh_rejects_a_card_from_another_tenant()
    {
        var clock = new Clock();
        var apn = CreateApn();
        await using var env = await CreateEnvironmentAsync(clock, apn);
        var current = await CreateMemberAsync(env, "wallet-isolation-a", LoyaltyProgramType.Points);
        var foreign = await CreateMemberAsync(env, "wallet-isolation-b", LoyaltyProgramType.Points);
        await RegisterAsync(env, foreign, "foreign-device");
        clock.UtcNow = clock.UtcNow.AddMinutes(10);

        var result = await env.InTenantScopeAsync(current.TenantId, current.Slug,
            sp => sp.GetRequiredService<IAppleWalletPassRefreshService>()
                .RefreshCardAsync(foreign.TenantId, foreign.CardId, PassUpdateReason.PointsAdded));

        Assert.Equal(0, result.CardsTouched);
        Assert.Equal(0, result.DevicesFound);
        Assert.Equal(0, result.PushesAttempted);
        apn.VerifyNoOtherCalls();
        await AssertCardAsync(env, foreign, Clock.Initial);
    }

    [Fact]
    public async Task Tenant_refresh_updates_only_installed_passes_in_that_tenant()
    {
        var clock = new Clock();
        var apn = CreateApn();
        await using var env = await CreateEnvironmentAsync(clock, apn);
        var target = await CreateMemberAsync(env, "wallet-batch-a", LoyaltyProgramType.Points);
        var uninstalled = await JoinMemberAsync(env, target.TenantId, target.Slug, "6465550002");
        var foreign = await CreateMemberAsync(env, "wallet-batch-b", LoyaltyProgramType.Points);
        await RegisterAsync(env, target, "installed");
        await RegisterAsync(env, foreign, "foreign-installed");
        clock.UtcNow = clock.UtcNow.AddMinutes(10);

        var result = await env.InTenantScopeAsync(target.TenantId, target.Slug,
            sp => sp.GetRequiredService<IAppleWalletPassRefreshService>()
                .RefreshTenantInstalledPassesAsync(target.TenantId, PassUpdateReason.BrandingUpdated));

        Assert.Equal(new[] { target.Serial }, result.SerialNumbers);
        Assert.Equal(1, result.CardsTouched);
        Assert.Equal(1, result.DevicesFound);
        VerifyPush(apn, "installed", PassUpdateReason.BrandingUpdated);
        apn.VerifyNoOtherCalls();
        await AssertCardAsync(env, target, clock.UtcNow);
        await AssertCardAsync(env, uninstalled, Clock.Initial);
        await AssertCardAsync(env, foreign, Clock.Initial);
    }

    private static Task<Env> CreateEnvironmentAsync(Clock clock, Mock<IApnService> apn) =>
        Env.CreateAsync(nowUtc: clock.UtcNow, configure: services =>
        {
            services.RemoveAll<IDateTimeProvider>();
            services.AddSingleton<IDateTimeProvider>(clock);
            services.RemoveAll<IApnService>();
            services.AddSingleton<IApnService>(apn.Object);
        });

    private static Mock<IApnService> CreateApn()
    {
        var apn = new Mock<IApnService>(MockBehavior.Strict);
        apn.Setup(x => x.SendPassUpdateAsync(It.IsAny<string>(), It.IsAny<PassUpdateReason>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(ApnPushResult.Accepted(200));
        return apn;
    }

    private static void VerifyPush(Mock<IApnService> apn, string token, PassUpdateReason reason) =>
        apn.Verify(x => x.SendPassUpdateAsync(token, reason, It.IsAny<CancellationToken>()), Times.Once);

    private static async Task<Member> CreateMemberAsync(Env env, string slug, LoyaltyProgramType program)
    {
        var tenant = await env.ProvisionAsync(slug, "Wallet Regression", "owner", "Owner123!", programType: program);
        Assert.True(tenant.IsSuccess, tenant.Error);
        if (program == LoyaltyProgramType.Stamps)
        {
            var configured = await env.SendAsOperatorAsync(tenant.Value.TenantId, slug,
                new SetStampProgramCommand(5, 0, StampReversalPolicy.AdminOnly, [new(5, "Reward")]));
            Assert.True(configured.IsSuccess, configured.Error);
        }
        return await JoinMemberAsync(env, tenant.Value.TenantId, slug, "6465550001");
    }

    private static async Task<Member> JoinMemberAsync(Env env, Guid tenantId, string slug, string phone)
    {
        var joined = await env.JoinAsync(tenantId, slug, "Ana", "Test", phone);
        Assert.True(joined.IsSuccess, joined.Error);
        var card = await env.TenantReadAsync(tenantId, slug, db => db.LoyaltyCards.AsNoTracking()
            .SingleAsync(c => c.SerialNumber == joined.Value.SerialNumber));
        return new Member(tenantId, slug, card.Id, card.SerialNumber, card.CurrentPoints, card.LifetimePoints);
    }

    private static Task RegisterAsync(Env env, Member member, params string[] tokens) =>
        env.TenantWriteAsync(member.TenantId, member.Slug, async db =>
        {
            foreach (var token in tokens)
                db.DeviceRegistrations.Add(new DeviceRegistration(Guid.NewGuid(), member.TenantId,
                    "device-" + token, "pass.test", member.Serial, token, Clock.Initial));
            await db.SaveChangesAsync();
        });

    private static Task<AppleWalletPassRefreshResult> RefreshAsync(Env env, Member member, PassUpdateReason reason) =>
        env.InTenantScopeAsync(member.TenantId, member.Slug,
            sp => sp.GetRequiredService<IAppleWalletPassRefreshService>()
                .RefreshCardAsync(member.TenantId, member.CardId, reason));

    private static Task<bool> AnnouncementEnabledAsync(Env env, Member member) =>
        env.TenantReadAsync(member.TenantId, member.Slug, db => db.StampPassEventContexts
            .Where(c => c.SerialNumber == member.Serial).Select(c => c.AnnouncementEnabled).SingleAsync());

    private static async Task AssertCardAsync(Env env, Member member, DateTime expectedActivity)
    {
        var card = await env.TenantReadAsync(member.TenantId, member.Slug,
            db => db.LoyaltyCards.AsNoTracking().SingleAsync(c => c.Id == member.CardId));
        Assert.Equal(expectedActivity, card.LastActivityAt);
        Assert.Equal(member.InitialPoints, card.CurrentPoints);
        Assert.Equal(member.InitialLifetimePoints, card.LifetimePoints);
    }

    private sealed record Member(Guid TenantId, string Slug, Guid CardId, string Serial,
        int InitialPoints, int InitialLifetimePoints);

    private sealed class Clock : IDateTimeProvider
    {
        public static readonly DateTime Initial = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
        public DateTime UtcNow { get; set; } = Initial;
        public DateTime Today => UtcNow.Date;
    }
}
