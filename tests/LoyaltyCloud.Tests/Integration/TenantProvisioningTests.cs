extern alias AdminApp;

using System.Security.Claims;
using AdminApp::LoyaltyCloud.Admin.Auth;
using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Customers.Commands.JoinCustomer;
using LoyaltyCloud.Application.Provisioning;
using LoyaltyCloud.Application.Points.Commands.AddPoints;
using LoyaltyCloud.Application.Points.Commands.ExpirePoints;
using LoyaltyCloud.Application.Redemptions.Commands.RedeemReward;
using LoyaltyCloud.Application.Redemptions.Commands.RedeemMonetaryDiscount;
using LoyaltyCloud.Application.Redemptions.Commands.ConfirmRedemption;
using LoyaltyCloud.Application.Redemptions.Commands.CancelRedemption;
using LoyaltyCloud.Application.Levels.Commands.RecalculateLevels;
using LoyaltyCloud.Application.Config.Commands.UpdateProgramConfig;
using LoyaltyCloud.Application.Rewards.Commands.CreateReward;
using LoyaltyCloud.Application.GiftCards;
using LoyaltyCloud.Application.Levels;
using LoyaltyCloud.Application.Levels.Commands.UpdateTenantLoyaltyLevels;
using LoyaltyCloud.Application.Campaigns.Commands.CreatePointCampaign;
using LoyaltyCloud.Application.SuperAdmin.Commands.DeleteTenant;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class TenantProvisioningTests
{
    private const string AdminPassword = "Provision123!";

    [Fact]
    [Trait("Category", "TenantProgramType")]
    public void Tenant_defaults_to_points_and_has_no_public_program_type_mutator()
    {
        var tenant = new Tenant(Guid.NewGuid(), "default", "Default", "America/Tijuana", DateTime.UtcNow);
        Assert.Equal(LoyaltyProgramType.Points, tenant.ProgramType);
        Assert.False(typeof(Tenant).GetProperty(nameof(Tenant.ProgramType))!.SetMethod!.IsPublic);
        Assert.DoesNotContain(typeof(Tenant).GetMethods(), method => method.Name == "ChangeProgramType");
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tenant(Guid.NewGuid(), "invalid", "Invalid",
            "America/Tijuana", DateTime.UtcNow, programType: (LoyaltyProgramType)99));
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Provisioning_creates_complete_trial_tenant()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();

        var result = await env.ProvisionAsync("beauty-room", "Beauty Room", "owner", AdminPassword);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("beauty-room", result.Value.TenantSlug);
        Assert.NotEqual(Guid.Empty, result.Value.TenantId);
        Assert.NotEqual(Guid.Empty, result.Value.AdminUserId);
        Assert.Equal(TenantSubscriptionStatus.Trial.ToString(), result.Value.SubscriptionStatus);

        var row = await env.PlatformReadAsync(async db => await db.Tenants
            .Where(t => t.Id == result.Value.TenantId)
            .Select(t => new
            {
                Tenant = t,
                Branding = t.Branding!,
                Subscription = t.Subscription!,
                Admin = db.TenantAdminUsers.IgnoreQueryFilters().Single(u => u.TenantId == t.Id),
                ConfigCount = db.ProgramConfigs.IgnoreQueryFilters().Count(c => c.TenantId == t.Id),
                LevelCount = db.TenantLoyaltyLevels.IgnoreQueryFilters().Count(l => l.TenantId == t.Id)
            })
            .SingleAsync());

        Assert.Equal("Beauty Room", row.Tenant.DisplayName);
        Assert.Equal(LoyaltyProgramType.Points, row.Tenant.ProgramType);
        Assert.True(row.Tenant.IsActive);
        Assert.Equal("#111827", row.Branding.PrimaryColor);
        Assert.Equal("#F3F4F6", row.Branding.SecondaryColor);
        Assert.Equal(TenantSubscriptionStatus.Trial, row.Subscription.Status);
        Assert.Equal("trial", row.Subscription.PlanCode);
        Assert.NotNull(row.Subscription.CurrentPeriodStart);
        Assert.NotNull(row.Subscription.CurrentPeriodEnd);
        Assert.True(row.Subscription.IsOperational(DateTime.UtcNow));
        Assert.Equal("owner", row.Admin.Username);
        Assert.Equal("OWNER", row.Admin.NormalizedUsername);
        Assert.True(row.Admin.IsActive);
        Assert.NotEqual(AdminPassword, row.Admin.PasswordHash);
        Assert.DoesNotContain(AdminPassword, row.Admin.PasswordHash, StringComparison.Ordinal);
        Assert.Equal(TenantProvisioningDefaults.ProgramConfigRows.Count, row.ConfigCount);
        Assert.Equal(TenantProvisioningDefaults.LoyaltyLevels.Count, row.LevelCount);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    [Trait("Category", "MTLevel1")]
    public async Task Provisioning_creates_default_loyalty_levels_for_new_tenant()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();

        var result = await env.ProvisionAsync("levels-spa", "Levels Spa", "owner", AdminPassword);

        Assert.True(result.IsSuccess, result.Error);

        var levels = await env.PlatformReadAsync(async db => await db.TenantLoyaltyLevels
            .IgnoreQueryFilters()
            .Where(level => level.TenantId == result.Value.TenantId)
            .OrderBy(level => level.SortOrder)
            .Select(level => new { level.Name, level.NormalizedName, level.Threshold, level.SortOrder, level.IsActive })
            .ToListAsync());

        Assert.Collection(
            levels,
            level =>
            {
                Assert.Equal(LoyaltyConstants.Levels.Mist, level.Name);
                Assert.Equal("MIST", level.NormalizedName);
                Assert.Equal(LoyaltyConstants.Defaults.LevelMistMin, level.Threshold);
                Assert.Equal(1, level.SortOrder);
                Assert.True(level.IsActive);
            },
            level =>
            {
                Assert.Equal(LoyaltyConstants.Levels.Glow, level.Name);
                Assert.Equal("GLOW", level.NormalizedName);
                Assert.Equal(LoyaltyConstants.Defaults.LevelGlowMin, level.Threshold);
                Assert.Equal(2, level.SortOrder);
                Assert.True(level.IsActive);
            },
            level =>
            {
                Assert.Equal(LoyaltyConstants.Levels.Radiance, level.Name);
                Assert.Equal("RADIANCE", level.NormalizedName);
                Assert.Equal(LoyaltyConstants.Defaults.LevelRadianceMin, level.Threshold);
                Assert.Equal(3, level.SortOrder);
                Assert.True(level.IsActive);
            });
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Provisioned_admin_can_authenticate()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        await env.ProvisionAsync("spa-admin", "Spa Admin", "owner", AdminPassword);

        var login = await env.SignInAsync("spa-admin", "owner", AdminPassword);

        Assert.Equal(AdminLoginResult.Success, login);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Join_works_for_provisioned_tenant_and_is_isolated()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var provisioned = await env.ProvisionAsync("skin-lab", "Skin Lab", "owner", AdminPassword);

        var join = await env.JoinAsync(provisioned.Value.TenantId, "skin-lab", "Ana", "Tenant", "6465550001");

        Assert.True(join.IsSuccess, join.Error);
        Assert.StartsWith("KB-", join.Value.SerialNumber, StringComparison.Ordinal);

        var tenantId = await env.PlatformReadAsync(async db => await db.Customers
            .IgnoreQueryFilters()
            .Where(c => c.NormalizedPhone == "6465550001")
            .Select(c => c.TenantId)
            .SingleAsync());
        Assert.Equal(provisioned.Value.TenantId, tenantId);
        Assert.NotEqual(TenantSeed.KBeautyTenantId, tenantId);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Duplicate_slug_fails_without_upsert()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var first = await env.ProvisionAsync("duplicate-spa", "Duplicate Spa", "owner", AdminPassword);
        var second = await env.ProvisionAsync("duplicate-spa", "Duplicate Spa 2", "owner2", AdminPassword);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal("El identificador del negocio ya está en uso.", second.Error);

        var count = await env.PlatformReadAsync(db => db.Tenants.CountAsync(t => t.Slug == "duplicate-spa"));
        Assert.Equal(1, count);
    }

    [Theory]
    [Trait("Category", "TenantProvisioning")]
    [InlineData("../kbeauty")]
    [InlineData("bella_salon")]
    [InlineData("bella/salon")]
    [InlineData("Bella Salon")]
    public async Task Invalid_slug_fails(string slug)
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();

        var result = await env.ProvisionAsync(slug, "Invalid Slug", "owner", AdminPassword);

        Assert.True(result.IsFailure);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Short_password_fails_and_does_not_create_tenant()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();

        var result = await env.ProvisionAsync("short-pass", "Short Pass", "owner", "short");

        Assert.True(result.IsFailure);
        var exists = await env.PlatformReadAsync(db => db.Tenants.AnyAsync(t => t.Slug == "short-pass"));
        Assert.False(exists);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Dangerous_url_fails_and_does_not_create_tenant()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();

        var result = await env.ProvisionAsync(
            "bad-url",
            "Bad URL",
            "owner",
            AdminPassword,
            instagramUrl: "javascript:alert(1)");

        Assert.True(result.IsFailure);
        var exists = await env.PlatformReadAsync(db => db.Tenants.AnyAsync(t => t.Slug == "bad-url"));
        Assert.False(exists);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Invalid_colors_are_normalized_to_generic_defaults()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();

        var result = await env.ProvisionAsync(
            "color-defaults",
            "Color Defaults",
            "owner",
            AdminPassword,
            primaryColor: "bad",
            secondaryColor: "also-bad");

        Assert.True(result.IsSuccess, result.Error);
        var branding = await env.PlatformReadAsync(db => db.TenantBrandings.SingleAsync(b => b.TenantId == result.Value.TenantId));
        Assert.Equal("#111827", branding.PrimaryColor);
        Assert.Equal("#F3F4F6", branding.SecondaryColor);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Same_admin_username_can_exist_in_different_tenants()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();

        var first = await env.ProvisionAsync("same-owner-a", "Same Owner A", "owner", AdminPassword);
        var second = await env.ProvisionAsync("same-owner-b", "Same Owner B", "owner", AdminPassword);

        Assert.True(first.IsSuccess, first.Error);
        Assert.True(second.IsSuccess, second.Error);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Provisioning_does_not_require_default_tenant_configuration()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();

        var result = await env.ProvisionAsync("no-default", "No Default", "owner", AdminPassword);

        Assert.True(result.IsSuccess, result.Error);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    [Trait("Category", "PlatformTenantDeletion")]
    public async Task Platform_admin_can_hard_delete_tenant_and_preserve_other_tenants()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var target = await env.ProvisionAsync("delete-me", "Delete Me", "owner", AdminPassword);
        var other = await env.ProvisionAsync("keep-me", "Keep Me", "owner", AdminPassword);
        Assert.True(target.IsSuccess, target.Error);
        Assert.True(other.IsSuccess, other.Error);

        var joined = await env.JoinAsync(target.Value.TenantId, "delete-me", "Ana", "Delete", "6465559999");
        Assert.True(joined.IsSuccess, joined.Error);

        var delete = await env.DeleteTenantAsync(target.Value.TenantId, "delete-me");

        Assert.True(delete.IsSuccess, delete.Error);
        var counts = await env.PlatformReadAsync(async db => new
        {
            Tenants = await db.Tenants.IgnoreQueryFilters().CountAsync(t => t.Id == target.Value.TenantId),
            AdminUsers = await db.TenantAdminUsers.IgnoreQueryFilters().CountAsync(t => t.TenantId == target.Value.TenantId),
            Branding = await db.TenantBrandings.CountAsync(t => t.TenantId == target.Value.TenantId),
            Subscription = await db.TenantSubscriptions.CountAsync(t => t.TenantId == target.Value.TenantId),
            Configs = await db.ProgramConfigs.IgnoreQueryFilters().CountAsync(t => t.TenantId == target.Value.TenantId),
            Levels = await db.TenantLoyaltyLevels.IgnoreQueryFilters().CountAsync(t => t.TenantId == target.Value.TenantId),
            Customers = await db.Customers.IgnoreQueryFilters().CountAsync(t => t.TenantId == target.Value.TenantId),
            Cards = await db.LoyaltyCards.IgnoreQueryFilters().CountAsync(t => t.TenantId == target.Value.TenantId),
            OtherTenant = await db.Tenants.IgnoreQueryFilters().CountAsync(t => t.Id == other.Value.TenantId),
            OtherAdmin = await db.TenantAdminUsers.IgnoreQueryFilters().CountAsync(t => t.TenantId == other.Value.TenantId)
        });

        Assert.Equal(0, counts.Tenants);
        Assert.Equal(0, counts.AdminUsers);
        Assert.Equal(0, counts.Branding);
        Assert.Equal(0, counts.Subscription);
        Assert.Equal(0, counts.Configs);
        Assert.Equal(0, counts.Levels);
        Assert.Equal(0, counts.Customers);
        Assert.Equal(0, counts.Cards);
        Assert.Equal(1, counts.OtherTenant);
        Assert.Equal(1, counts.OtherAdmin);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    [Trait("Category", "PlatformTenantDeletion")]
    public async Task Hard_delete_requires_exact_slug_confirmation()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var tenant = await env.ProvisionAsync("exact-slug", "Exact Slug", "owner", AdminPassword);
        Assert.True(tenant.IsSuccess, tenant.Error);

        var delete = await env.DeleteTenantAsync(tenant.Value.TenantId, "EXACT-SLUG");

        Assert.True(delete.IsFailure);
        Assert.Equal("El slug de confirmacion no coincide.", delete.Error);
        var exists = await env.PlatformReadAsync(db => db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == tenant.Value.TenantId));
        Assert.True(exists);
    }

    [Theory]
    [Trait("Category", "TenantProgramType")]
    [InlineData(LoyaltyProgramType.Points)]
    [InlineData(LoyaltyProgramType.Stamps)]
    public async Task Provisioning_persists_selected_program_and_preserves_login_and_customer_identity(LoyaltyProgramType programType)
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var provisioned = await env.ProvisionAsync("program-tenant", "Program Tenant", "owner", AdminPassword, programType: programType);
        Assert.True(provisioned.IsSuccess, provisioned.Error);
        var id = provisioned.Value.TenantId;
        Assert.Equal(programType, await env.PlatformReadAsync(async db => (await db.Tenants.SingleAsync(t => t.Id == id)).ProgramType));
        Assert.Equal(AdminLoginResult.Success, await env.SignInAsync("program-tenant", "owner", AdminPassword));
        if (programType == LoyaltyProgramType.Stamps) await env.ConfigureStampsAsync(id, "program-tenant");
        var join = await env.JoinAsync(id, "program-tenant", "Ana", "Tenant", "6465550001");
        Assert.True(join.IsSuccess, join.Error);
        var card = await env.PlatformReadAsync(db => db.LoyaltyCards.IgnoreQueryFilters().SingleAsync(c => c.TenantId == id));
        Assert.Equal(join.Value.SerialNumber, card.SerialNumber);
        Assert.True(card.IsActive);
        Assert.False(string.IsNullOrWhiteSpace(card.AuthenticationToken));
        if (programType == LoyaltyProgramType.Stamps)
        {
            Assert.Equal(0, card.CurrentPoints);
            Assert.Equal(0, await env.PlatformReadAsync(db => db.PointTransactions.IgnoreQueryFilters().CountAsync(t => t.TenantId == id)));
            Assert.Equal(0, await env.PlatformReadAsync(db => db.PointLots.IgnoreQueryFilters().CountAsync(t => t.TenantId == id)));
            Assert.Equal(0, await env.PlatformReadAsync(db => db.ProgramConfigs.IgnoreQueryFilters().CountAsync(t => t.TenantId == id)));
            Assert.Equal(0, await env.PlatformReadAsync(db => db.TenantLoyaltyLevels.IgnoreQueryFilters().CountAsync(t => t.TenantId == id)));
        }
        else
        {
            Assert.True(card.CurrentPoints > 0);
            var earn = await env.SendAsync(id, "program-tenant", new AddPointsCommand(card.SerialNumber, 100, "owner"));
            Assert.True(earn.IsSuccess, earn.Error);
            var reward = await env.SendAsync(id, "program-tenant", new CreateRewardCommand("Reward", "Test", 1, "Mist", false, null, null, true));
            Assert.True(reward.IsSuccess, reward.Error);
            var redeem = await env.SendAsync(id, "program-tenant", new RedeemRewardCommand(card.SerialNumber, reward.Value.Id, "owner"));
            Assert.True(redeem.IsSuccess, redeem.Error);
            var monetary = await env.SendAsync(id, "program-tenant", new RedeemMonetaryDiscountCommand(card.SerialNumber, 10, "owner"));
            Assert.True(monetary.IsSuccess, monetary.Error);
        }
    }

    [Fact]
    [Trait("Category", "TenantProgramType")]
    public async Task Stamps_rejects_points_writes_before_handlers_modify_data()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var provisioned = await env.ProvisionAsync("stamps-only", "Stamps", "owner", AdminPassword, programType: LoyaltyProgramType.Stamps);
        var id = provisioned.Value.TenantId;
        await env.ConfigureStampsAsync(id, "stamps-only");
        var join = await env.JoinAsync(id, "stamps-only", "Ana", "Tenant", "6465550001");
        Assert.True(join.IsSuccess, join.Error);
        object[] commands =
        [
            new AddPointsCommand(join.Value.SerialNumber, 100, "owner"),
            new RedeemRewardCommand(join.Value.SerialNumber, Guid.NewGuid(), "owner"),
            new RedeemMonetaryDiscountCommand(join.Value.SerialNumber, 10, "owner"),
            new ConfirmRedemptionCommand(Guid.NewGuid(), "owner"),
            new CancelRedemptionCommand(Guid.NewGuid(), "owner"),
            new ExpirePointsCommand("owner"), new RecalculateLevelsCommand("owner"),
            new UpdateProgramConfigCommand([new(LoyaltyConstants.ConfigKeys.WelcomeBonusPoints, "10")], "owner"),
            new CreateRewardCommand("Reward", "Test", 10, "Mist", false, null, null, true),
            new CreatePointCampaignCommand("Campaign", "Test", 2, null, "All", DateTime.UtcNow, DateTime.UtcNow.AddDays(1)),
            new UpdateTenantLoyaltyLevelsCommand([new(null, "Mist", 0), new(null, "Glow", 1000), new(null, "Radiance", 3000)], "owner")
        ];
        foreach (var command in commands)
        {
            var result = Assert.IsAssignableFrom<Result>(await env.SendObjectAsync(id, "stamps-only", command));
            Assert.True(result.IsFailure);
            Assert.Contains("solo esta disponible para programas de Puntos", result.Error);
        }
        Assert.Equal(0, await env.PlatformReadAsync(db => db.PointTransactions.IgnoreQueryFilters().CountAsync()));
        Assert.Equal(0, await env.PlatformReadAsync(db => db.Redemptions.IgnoreQueryFilters().CountAsync()));
        var levels = await env.UpdateLevelsDirectlyAsync(id, "stamps-only");
        Assert.True(levels.IsFailure);
        Assert.Contains("solo esta disponible para programas de Puntos", levels.Error);
    }

    [Fact]
    [Trait("Category", "TenantProgramType")]
    public async Task Invalid_program_type_is_rejected_without_creating_tenant()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var result = await env.ProvisionAsync("invalid-program", "Invalid", "owner", AdminPassword,
            programType: (LoyaltyProgramType)99);
        Assert.True(result.IsFailure);
        Assert.Equal(0, await env.PlatformReadAsync(db => db.Tenants.CountAsync()));
    }

    [Fact]
    [Trait("Category", "TenantProgramType")]
    public async Task Program_guards_use_current_tenant_and_preserve_cross_tenant_card_isolation()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var points = await env.ProvisionAsync("points-a", "Points A", "owner", AdminPassword);
        var stamps = await env.ProvisionAsync("stamps-b", "Stamps B", "owner", AdminPassword, programType: LoyaltyProgramType.Stamps);
        var memberA = await env.JoinAsync(points.Value.TenantId, "points-a", "Ana", "A", "6465550001");
        await env.ConfigureStampsAsync(stamps.Value.TenantId, "stamps-b");
        var memberB = await env.JoinAsync(stamps.Value.TenantId, "stamps-b", "Ana", "B", "6465550002");
        Assert.True(memberA.IsSuccess, memberA.Error);
        Assert.True(memberB.IsSuccess, memberB.Error);
        var denied = await env.SendAsync(stamps.Value.TenantId, "stamps-b", new AddPointsCommand(memberA.Value.SerialNumber, 100, "owner"));
        Assert.True(denied.IsFailure);
        Assert.Contains("solo esta disponible para programas de Puntos", denied.Error);
        var wrongCard = await env.SendAsync(points.Value.TenantId, "points-a", new AddPointsCommand(memberB.Value.SerialNumber, 100, "owner"));
        Assert.True(wrongCard.IsFailure);
        var allowed = await env.SendAsync(points.Value.TenantId, "points-a", new AddPointsCommand(memberA.Value.SerialNumber, 100, "owner"));
        Assert.True(allowed.IsSuccess, allowed.Error);
        Assert.Equal(0, await env.PlatformReadAsync(db => db.PointTransactions.IgnoreQueryFilters().CountAsync(t => t.TenantId == stamps.Value.TenantId)));
    }

    [Theory]
    [Trait("Category", "TenantProgramType")]
    [InlineData(LoyaltyProgramType.Points)]
    [InlineData(LoyaltyProgramType.Stamps)]
    public async Task Gift_cards_can_be_issued_and_redeemed_for_both_programs(LoyaltyProgramType programType)
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var provisioned = await env.ProvisionAsync("gift-program", "Gift Program", "owner", AdminPassword, programType: programType);
        await env.GiftCardRoundTripAsync(provisioned.Value.TenantId, "gift-program");
    }

    [Fact]
    [Trait("Category", "TenantProgramType")]
    public async Task Program_type_cannot_be_changed_after_persistence()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var result = await env.ProvisionAsync("immutable", "Immutable", "owner", AdminPassword);
        await env.PlatformReadAsync(async db =>
        {
            var tenant = await db.Tenants.SingleAsync(t => t.Id == result.Value.TenantId);
            db.Entry(tenant).Property(t => t.ProgramType).CurrentValue = LoyaltyProgramType.Stamps;
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            return true;
        });
        Assert.Equal(LoyaltyProgramType.Points, await env.PlatformReadAsync(async db => (await db.Tenants.SingleAsync(t => t.Id == result.Value.TenantId)).ProgramType));
    }

    [Fact]
    [Trait("Category", "TenantProgramType")]
    public async Task Migration_backfills_existing_tenant_as_points()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync("20260915063406_AddRedemptionIdempotencyKey");
        await env.PlatformReadAsync(async db =>
        {
            var id = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Tenants (Id, Slug, DisplayName, TimeZoneId, IsActive, CreatedAt) VALUES ({id}, {"legacy"}, {"Legacy"}, {"America/Tijuana"}, {true}, {DateTime.UtcNow})");
            await db.Database.MigrateAsync();
            Assert.Equal(LoyaltyProgramType.Points, (await db.Tenants.SingleAsync()).ProgramType);
            return true;
        });
    }

    [Theory]
    [Trait("Category", "StampFoundation")]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Stamp_versions_and_registration_preserve_rules_and_audit_welcome(int welcome)
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var tenant = await env.ProvisionAsync("stamp-foundation", "Stamps", "owner", AdminPassword, programType: LoyaltyProgramType.Stamps);
        var id = tenant.Value.TenantId;
        var first = await env.ConfigureStampsAsync(id, "stamp-foundation", welcome);
        Assert.True(first.IsSuccess, first.Error);
        Assert.Equal(1, first.Value.Version);
        var joined = await env.JoinAsync(id, "stamp-foundation", "Ana", "Stamp", "6465550001");
        Assert.True(joined.IsSuccess, joined.Error);
        var second = await env.ConfigureStampsAsync(id, "stamp-foundation", 3);
        Assert.True(second.IsSuccess, second.Error);
        Assert.Equal(2, second.Value.Version);
        await env.TenantReadAsync(id, "stamp-foundation", async db =>
        {
            Assert.Equal(1, await db.StampProgramVersions.CountAsync(x => x.IsActive));
            var old = await db.StampProgramVersions.SingleAsync(x => x.Id == first.Value.Id);
            Assert.False(old.IsActive);
            Assert.Equal(welcome, old.WelcomeStamps);
            var cycle = await db.StampCycles.SingleAsync();
            Assert.Equal(first.Value.Id, cycle.StampProgramVersionId);
            Assert.Equal(joined.Value.SerialNumber, cycle.LoyaltyCardSerialNumber);
            Assert.Equal(1, cycle.Sequence);
            Assert.Equal(welcome, cycle.CurrentStamps);
            Assert.Null(cycle.CompletedAt);
            Assert.NotEmpty(cycle.RowVersion);
            var ledger = await db.StampTransactions.ToListAsync();
            Assert.Equal(welcome == 0 ? 0 : 1, ledger.Count);
            if (welcome > 0)
            {
                Assert.Equal(StampTransactionType.Welcome, ledger[0].Type);
                Assert.Equal(welcome, ledger[0].Delta);
                Assert.Null(ledger[0].OperatorUserId);
                Assert.Equal("welcome", ledger[0].IdempotencyKey);
            }
            db.Entry(old).Property(x => x.MaxStamps).CurrentValue = 8;
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            return true;
        });
        var newMember = await env.JoinAsync(id, "stamp-foundation", "Otra", "Stamp", "6465550002");
        Assert.True(newMember.IsSuccess, newMember.Error);
        await env.TenantReadAsync(id, "stamp-foundation", async db =>
        {
            Assert.Equal(second.Value.Id, (await db.StampCycles.SingleAsync(x => x.LoyaltyCardSerialNumber == newMember.Value.SerialNumber)).StampProgramVersionId);
            return true;
        });
    }

    [Fact]
    [Trait("Category", "StampFoundation")]
    public async Task Stamps_without_configuration_fails_without_partial_registration_and_points_have_no_cycles()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var stamps = await env.ProvisionAsync("unconfigured", "Stamps", "owner", AdminPassword, programType: LoyaltyProgramType.Stamps);
        var denied = await env.JoinAsync(stamps.Value.TenantId, "unconfigured", "Ana", "Stamp", "6465550001");
        Assert.True(denied.IsFailure);
        Assert.Contains("configuracion activa", denied.Error);
        Assert.Equal(0, await env.PlatformReadAsync(db => db.Customers.IgnoreQueryFilters().CountAsync()));
        Assert.Equal(0, await env.PlatformReadAsync(db => db.LoyaltyCards.IgnoreQueryFilters().CountAsync()));
        var points = await env.ProvisionAsync("points-foundation", "Points", "owner", AdminPassword);
        Assert.True((await env.JoinAsync(points.Value.TenantId, "points-foundation", "Ana", "Points", "6465550001")).IsSuccess);
        Assert.Equal(0, await env.PlatformReadAsync(db => db.StampCycles.IgnoreQueryFilters().CountAsync()));
        var unauthenticated = await env.SendAsync(stamps.Value.TenantId, "unconfigured", new LoyaltyCloud.Application.Stamps.SetStampProgramCommand(
            10, 0, StampReversalPolicy.None, [new(10, "Reward")]));
        Assert.True(unauthenticated.IsFailure);
    }

    [Fact]
    [Trait("Category", "StampFoundation")]
    public async Task Stamp_sql_constraints_and_tenant_filters_prevent_invalid_links_and_duplicates()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var a = await env.ProvisionAsync("stamp-a", "A", "owner", AdminPassword, programType: LoyaltyProgramType.Stamps);
        var b = await env.ProvisionAsync("stamp-b", "B", "owner", AdminPassword, programType: LoyaltyProgramType.Stamps);
        var program = await env.ConfigureStampsAsync(a.Value.TenantId, "stamp-a");
        Assert.True(program.IsSuccess, program.Error);
        Assert.Null(await env.SendAsync(b.Value.TenantId, "stamp-b", new LoyaltyCloud.Application.Stamps.GetActiveStampProgramQuery()));
        Assert.True((await env.JoinAsync(a.Value.TenantId, "stamp-a", "Ana", "A", "6465550001")).IsSuccess);
        await env.TenantReadAsync(a.Value.TenantId, "stamp-a", async db =>
        {
            var active = await db.StampProgramVersions.SingleAsync();
            var card = await db.LoyaltyCards.SingleAsync();
            db.Add(new StampCycle(Guid.NewGuid(), card, active, DateTime.UtcNow));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            db.Add(new StampRewardDefinition(Guid.NewGuid(), active, 10, "Duplicate", null));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            db.Add(new StampProgramVersion(Guid.NewGuid(), a.Value.TenantId, 2, 10, 0, StampReversalPolicy.None, active.CreatedByUserId, DateTime.UtcNow));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            var foreign = new StampProgramVersion(Guid.NewGuid(), b.Value.TenantId, 1, 10, 0, StampReversalPolicy.None, active.CreatedByUserId, DateTime.UtcNow);
            db.Add(foreign);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            return true;
        });
    }

    [Fact]
    [Trait("Category", "StampFoundation")]
    public async Task Concurrent_stamp_configuration_is_monotonic_and_cashier_cannot_configure()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var tenant = await env.ProvisionAsync("stamp-concurrency", "Stamps", "owner", AdminPassword, programType: LoyaltyProgramType.Stamps);
        var results = await Task.WhenAll(env.ConfigureStampsAsync(tenant.Value.TenantId, "stamp-concurrency"),
            env.ConfigureStampsAsync(tenant.Value.TenantId, "stamp-concurrency", 0));
        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error));
        Assert.Equal(new[] { 1, 2 }, results.Select(x => x.Value.Version).OrderBy(x => x));
        await env.TenantReadAsync(tenant.Value.TenantId, "stamp-concurrency", async db =>
        {
            Assert.Equal(1, await db.StampProgramVersions.CountAsync(x => x.IsActive));
            (await db.TenantAdminUsers.SingleAsync()).ChangeRole(TenantUserRole.Cashier);
            await db.SaveChangesAsync();
            return true;
        });
        var denied = await env.ConfigureStampsAsync(tenant.Value.TenantId, "stamp-concurrency");
        Assert.True(denied.IsFailure);
        Assert.Contains("TenantAdmin", denied.Error);
    }

    [Fact]
    [Trait("Category", "StampFoundation")]
    public async Task Stamp_foreign_keys_reject_cross_tenant_program_reference_in_sql()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var a = await env.ProvisionAsync("stamp-fk-a", "A", "owner", AdminPassword, programType: LoyaltyProgramType.Stamps);
        var b = await env.ProvisionAsync("stamp-fk-b", "B", "owner", AdminPassword, programType: LoyaltyProgramType.Stamps);
        await env.ConfigureStampsAsync(a.Value.TenantId, "stamp-fk-a");
        var program = await env.TenantReadAsync(a.Value.TenantId, "stamp-fk-a", db => db.StampProgramVersions.AsNoTracking().SingleAsync());
        await env.TenantReadAsync(b.Value.TenantId, "stamp-fk-b", async db =>
        {
            var reward = new StampRewardDefinition(Guid.NewGuid(), program, 1, "Invalid link", null);
            db.Add(reward);
            db.Entry(reward).Property(x => x.TenantId).CurrentValue = b.Value.TenantId;
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            return true;
        });
    }

    [Fact]
    [Trait("Category", "StampAdmin")]
    public async Task Saving_equivalent_stamp_rules_in_any_reward_order_does_not_create_a_version()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var tenant = await env.ProvisionAsync("stamp-noop", "Stamps", "owner", AdminPassword, programType: LoyaltyProgramType.Stamps);
        var first = await env.ConfigureStampsAsync(tenant.Value.TenantId, "stamp-noop");
        Assert.True(first.IsSuccess, first.Error);
        var same = await env.ConfigureStampsAsync(tenant.Value.TenantId, "stamp-noop");
        Assert.Equal(first.Value.Id, same.Value.Id);
        var reordered = await env.SendAsOperatorAsync(tenant.Value.TenantId, "stamp-noop", new LoyaltyCloud.Application.Stamps.SetStampProgramCommand(
            10, 2, StampReversalPolicy.AdminOnly, first.Value.Rewards.Reverse().ToList()));
        Assert.Equal(first.Value.Id, reordered.Value.Id);
        await env.TenantReadAsync(tenant.Value.TenantId, "stamp-noop", async db =>
        {
            Assert.Equal(1, await db.StampProgramVersions.CountAsync()); return true;
        });
        var edited = await env.SendAsOperatorAsync(tenant.Value.TenantId, "stamp-noop", new LoyaltyCloud.Application.Stamps.SetStampProgramCommand(
            10, 2, StampReversalPolicy.AdminOnly, [new(5, "Nueva recompensa"), new(10, "Producto")]));
        Assert.Equal(2, edited.Value.Version);
    }

    internal sealed class ProvisioningTestEnvironment : IAsyncDisposable
    {
        private readonly ServiceProvider _services;

        private ProvisioningTestEnvironment(ServiceProvider services)
        {
            _services = services;
        }

        public static async Task<ProvisioningTestEnvironment> CreateAsync(string? migrationTarget = null, Action<IServiceCollection>? configure = null)
        {
            var dbName = "LoyaltyCloud_MT3E_" + Guid.NewGuid().ToString("N");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;",
                    ["Azure:KeyVaultUri"] = "",
                    ["Azure:BlobStorage:ConnectionString"] = "UseDevelopmentStorage=true",
                    ["Apple:PassTypeIdentifier"] = "pass.com.kbeautymx.loyalty",
                    ["Apple:TeamIdentifier"] = "TESTTEAM01",
                    ["Apple:WebServiceURL"] = "https://test.local",
                    ["Apple:OrganizationName"] = "LoyaltyCloud Test",
                    ["Wallet:UseRealPassSigning"] = "false",
                    ["Wallet:UseRealApns"] = "false",
                    ["Provisioning:TrialDays"] = "14"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.AddInfrastructure(configuration, new TestHostEnvironment());
            services.RemoveAll<IStorageService>();
            services.AddScoped<IStorageService, InMemoryStorageService>();
            configure?.Invoke(services);
            services.Configure<AdminAuthOptions>(_ => { });
            services.AddScoped<AdminAuthService>();
            services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie();

            var provider = services.BuildServiceProvider(validateScopes: true);
            var env = new ProvisioningTestEnvironment(provider);
            await env.InitializeAsync(migrationTarget);
            return env;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                using var scope = _services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
            }
            finally
            {
                await _services.DisposeAsync();
            }
        }

        public async Task<Result<ProvisionTenantResult>> ProvisionAsync(
            string slug,
            string displayName,
            string adminUsername,
            string password,
            string? primaryColor = null,
            string? secondaryColor = null,
            string? instagramUrl = null,
            LoyaltyProgramType programType = LoyaltyProgramType.Points)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProvisionTenantCommand(
                slug,
                displayName,
                TimeZoneId: null,
                adminUsername,
                password,
                PrimaryColor: primaryColor,
                SecondaryColor: secondaryColor,
                InstagramUrl: instagramUrl,
                ProgramType: programType));
        }

        public async Task<T> SendAsync<T>(Guid tenantId, string slug, IRequest<T> command)
        {
            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, slug);
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
        }

        public async Task<Result<LoyaltyCloud.Application.Stamps.StampProgramDto>> ConfigureStampsAsync(Guid tenantId, string slug, int welcome = 2,
            int max = 10, StampReversalPolicy policy = StampReversalPolicy.AdminOnly)
        {
            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, slug);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var adminId = await db.TenantAdminUsers.Select(x => x.Id).SingleAsync();
            var context = CreateHttpContext(scope.ServiceProvider);
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, adminId.ToString())], "Test"));
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
            var existing = await scope.ServiceProvider.GetRequiredService<IStampProgramService>().ReadActiveAsync();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new LoyaltyCloud.Application.Stamps.SetStampProgramCommand(
                max, welcome, policy, [new(max / 2, "Mascarilla", RewardIdentityId: existing?.Rewards[0].RewardIdentityId),
                    new(max, "Producto", RewardIdentityId: existing?.Rewards[1].RewardIdentityId)]));
        }

        public async Task<T> InTenantScopeAsync<T>(Guid tenantId, string slug, Func<IServiceProvider, Task<T>> action, bool authenticate = true)
        {
            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, slug);
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = null;
            if (authenticate)
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var id = await db.TenantAdminUsers.Select(x => x.Id).SingleAsync();
                var context = CreateHttpContext(scope.ServiceProvider);
                context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "Test"));
                scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
            }
            return await action(scope.ServiceProvider);
        }

        public async Task<T> SendAsOperatorAsync<T>(Guid tenantId, string slug, IRequest<T> command)
        {
            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, slug);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userId = await db.TenantAdminUsers.Select(x => x.Id).SingleAsync();
            var context = CreateHttpContext(scope.ServiceProvider);
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test"));
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
        }

        public async Task<object?> SendObjectAsync(Guid tenantId, string slug, object command)
        {
            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, slug);
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
        }

        public async Task<Result<UpdateTenantLoyaltyLevelsResultDto>> UpdateLevelsDirectlyAsync(Guid tenantId, string slug)
        {
            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, slug);
            return await scope.ServiceProvider.GetRequiredService<ITenantLoyaltyLevelManagementService>().UpdateAsync(
                [new(null, "Mist", 0), new(null, "Glow", 1000), new(null, "Radiance", 3000)], "owner");
        }

        public async Task GiftCardRoundTripAsync(Guid tenantId, string slug)
        {
            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, slug);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var adminId = await db.TenantAdminUsers.Select(user => user.Id).SingleAsync();
            var context = CreateHttpContext(scope.ServiceProvider);
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, adminId.ToString()), new Claim(ClaimTypes.Name, "owner")], "Test"));
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
            var gifts = scope.ServiceProvider.GetRequiredService<IGiftCardService>();
            await gifts.UpdateSettingsAsync(new(true, true, true, true, GiftCardExpirationMode.Never, null,
                "MXN", "Gift", "#111111", "#FFFFFF", null, null, null, null));
            var issued = await gifts.IssueAsync(new(100, null, "Ana", null, null, null, null));
            var redeemed = await gifts.RedeemAsync(issued.Card.Code, 10, "program-test", null, null);
            Assert.True(redeemed.Success, redeemed.Error);
            Assert.Equal(90, redeemed.Detail!.Card.CurrentBalance);
        }

        public async Task<AdminLoginResult> SignInAsync(string tenantSlug, string username, string password)
        {
            using var scope = _services.CreateScope();
            var context = CreateHttpContext(scope.ServiceProvider);
            return await scope.ServiceProvider.GetRequiredService<AdminAuthService>()
                .TrySignInAsync(context, tenantSlug, username, password);
        }

        public async Task<Result<JoinCustomerResponse>> JoinAsync(
            Guid tenantId,
            string tenantSlug,
            string firstName,
            string lastName,
            string phone)
        {
            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, tenantSlug);
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new JoinCustomerCommand(firstName, lastName, phone));
        }

        public async Task<Result> DeleteTenantAsync(Guid tenantId, string confirmationSlug)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new DeleteTenantCommand(tenantId, confirmationSlug));
        }

        public async Task<T> PlatformReadAsync<T>(Func<AppDbContext, Task<T>> query)
        {
            using var scope = _services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        public async Task<T> TenantReadAsync<T>(Guid tenantId, string slug, Func<AppDbContext, Task<T>> query)
        {
            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, slug);
            return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        private async Task InitializeAsync(string? migrationTarget)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync(migrationTarget);
        }

        private static DefaultHttpContext CreateHttpContext(IServiceProvider services)
        {
            var context = new DefaultHttpContext
            {
                RequestServices = services
            };
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("admin.test");
            return context;
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "LoyaltyCloud.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class InMemoryStorageService : IStorageService
    {
        private readonly Dictionary<string, byte[]> _passes = new(StringComparer.OrdinalIgnoreCase);

        public Task<string> UploadPassAsync(string serialNumber, byte[] passBytes, CancellationToken ct = default)
        {
            _passes[serialNumber] = passBytes;
            return Task.FromResult($"https://test.local/passes/{serialNumber}.pkpass");
        }

        public Task<byte[]?> DownloadPassAsync(string serialNumber, CancellationToken ct = default)
        {
            _passes.TryGetValue(serialNumber, out var passBytes);
            return Task.FromResult(passBytes);
        }
    }
}
