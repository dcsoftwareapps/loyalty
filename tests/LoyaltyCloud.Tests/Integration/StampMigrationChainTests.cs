using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;
using Env = LoyaltyCloud.Tests.Integration.TenantProvisioningTests.ProvisioningTestEnvironment;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "StampMigrations")]
public sealed class StampMigrationChainTests
{
    [Fact]
    public async Task Clean_local_database_applies_entire_chain_and_snapshot_matches_model()
    {
        // The environment creates a unique LocalDB database and migrates from zero.
        await using var env = await Env.CreateAsync();
        await env.PlatformReadAsync(async db =>
        {
            Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            var id = db.Model.FindEntityType(typeof(StampCycle))!.FindProperty(nameof(StampCycle.Id));
            Assert.NotNull(id); Assert.Equal(typeof(Guid), id.ClrType);
            var column = db.Model.FindEntityType(typeof(StampRewardIdentity))!.FindProperty(nameof(StampRewardIdentity.UpcomingImageBlobName));
            Assert.NotNull(column); Assert.True(column.IsNullable); Assert.Equal(1024, column.GetMaxLength());
            return true;
        });
        var operation = Assert.IsType<AddColumnOperation>(Assert.Single(new AddStampUpcomingRewardImage().UpOperations));
        Assert.Equal("StampRewardIdentities", operation.Table); Assert.Equal("UpcomingImageBlobName", operation.Name);
        Assert.True(operation.IsNullable); Assert.Null(operation.DefaultValue); Assert.Null(operation.DefaultValueSql);
    }

    [Fact]
    public async Task Upgrade_preserves_existing_available_image_and_defaults_upcoming_to_null()
    {
        await using var env = await Env.CreateAsync("20261004022555_AddCashierRefreshTokens");
        const string slug = "legacy-image";
        var tenant = (await env.ProvisionAsync(slug, "Legacy", "owner", "Owner123!", programType: LoyaltyProgramType.Stamps)).Value.TenantId;
        var identity = Guid.NewGuid(); const string image = "legacy/available.png";
        await env.TenantReadAsync(tenant, slug, async db =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO StampRewardIdentities (Id, TenantId, ImageBlobName) VALUES ({identity}, {tenant}, {image})");
            return true;
        });
        await env.PlatformReadAsync(async db => { await db.Database.MigrateAsync(); return true; });
        await env.TenantReadAsync(tenant, slug, async db =>
        {
            var row = await db.StampRewardIdentities.SingleAsync(x => x.Id == identity);
            Assert.Equal(image, row.ImageBlobName); Assert.Null(row.UpcomingImageBlobName); return true;
        });
    }
}
