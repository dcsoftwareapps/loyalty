using System.Text.Json;
using LoyaltyCloud.API.Controllers;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Env = LoyaltyCloud.Tests.Integration.TenantProvisioningTests.ProvisioningTestEnvironment;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "StampOperations")]
public sealed class StampOperationsTests
{
    private const string Slug = "stamp-operations";
    private static async Task<(Guid TenantId, string Serial)> SetupAsync(Env env, int welcome = 0,
        StampReversalPolicy policy = StampReversalPolicy.AdminOnly, bool cashier = false)
    {
        var tenant = await env.ProvisionAsync(Slug, "Stamps", "owner", "Owner123!", programType: LoyaltyProgramType.Stamps);
        Assert.True(tenant.IsSuccess, tenant.Error);
        var id = tenant.Value.TenantId;
        var configured = await env.ConfigureStampsAsync(id, Slug, welcome, policy: policy);
        Assert.True(configured.IsSuccess, configured.Error);
        var joined = await env.JoinAsync(id, Slug, "Ana", "Stamp", "6465550001");
        Assert.True(joined.IsSuccess, joined.Error);
        if (cashier) await env.TenantReadAsync(id, Slug, async db =>
        {
            (await db.TenantAdminUsers.SingleAsync()).ChangeRole(TenantUserRole.Cashier);
            await db.SaveChangesAsync(); return true;
        });
        return (id, joined.Value.SerialNumber);
    }

    private static Task<Result<StampOperationResult>> Earn(Env env, Guid id, string serial, string key) =>
        env.SendAsOperatorAsync(id, Slug, new EarnStampCommand(serial, key));
    private static Task<Result<StampOperationResult>> Reverse(Env env, Guid id, string serial, string key) =>
        env.SendAsOperatorAsync(id, Slug, new ReverseLastStampCommand(serial, key));

    [Fact]
    public async Task Earn_unlocks_completes_replays_original_result_and_rolls_over_with_new_rules()
    {
        await using var env = await Env.CreateAsync();
        var (id, serial) = await SetupAsync(env, 4);
        var first = await Earn(env, id, serial, "first");
        Assert.True(first.IsSuccess, first.Error);
        Assert.True(first.Value.State.CanReverseLastEarn);
        Assert.Equal(5, first.Value.State.CurrentStamps);
        Assert.Equal("Mascarilla", Assert.Single(first.Value.UnlockedRewards).Name);
        for (var i = 0; i < 5; i++) Assert.True((await Earn(env, id, serial, "earn-" + i)).IsSuccess);
        var state = await env.SendAsOperatorAsync(id, Slug, new GetStampStateQuery(serial));
        Assert.Equal(10, state.Value.CurrentStamps); Assert.True(state.Value.CycleCompleted);
        Assert.Equal(2, state.Value.AvailableRewards.Count);
        var replay = await Earn(env, id, serial, "first");
        Assert.Equal(JsonSerializer.Serialize(first.Value), JsonSerializer.Serialize(replay.Value));
        Assert.True((await Reverse(env, id, serial, "first")).IsFailure);
        Assert.True((await env.ConfigureStampsAsync(id, Slug, 2, 8)).IsSuccess);
        var rollover = await Earn(env, id, serial, "rollover");
        Assert.Equal(1, rollover.Value.State.CurrentStamps); Assert.Equal(8, rollover.Value.State.MaxStamps);
        Assert.Equal(2, rollover.Value.State.CycleSequence);
        Assert.Equal(2, rollover.Value.State.AvailableRewards.Count);
        Assert.Equal(5, rollover.Value.State.AvailableRewards.Single(x => x.Name == "Mascarilla").RequiredStamps);
        for (var i = 0; i < 3; i++) Assert.True((await Earn(env, id, serial, "new-" + i)).IsSuccess);
        var current = await env.SendAsOperatorAsync(id, Slug, new GetStampStateQuery(serial));
        Assert.Equal(2, current.Value.AvailableRewards.Count(x => x.Name == "Mascarilla"));
        await env.TenantReadAsync(id, Slug, async db =>
        {
            Assert.Equal(2, await db.StampCycles.CountAsync());
            Assert.Equal(1, await db.StampTransactions.CountAsync(x => x.Type == StampTransactionType.Welcome));
            Assert.Equal(10, (await db.StampCycles.SingleAsync(x => x.Sequence == 1)).CurrentStamps);
            return true;
        });
    }

    [Fact]
    public async Task Final_reversal_voids_reward_reopens_cycle_and_can_reearn_without_duplicate_reversal()
    {
        await using var env = await Env.CreateAsync();
        var (id, serial) = await SetupAsync(env, 9);
        Assert.True((await Reverse(env, id, serial, "welcome-denied")).IsFailure);
        var earn = await Earn(env, id, serial, "final");
        var rewardId = Assert.Single(earn.Value.UnlockedRewards).Id;
        var reversed = await Reverse(env, id, serial, "reverse-final");
        Assert.True(reversed.IsSuccess, reversed.Error);
        Assert.Equal(9, reversed.Value.State.CurrentStamps); Assert.False(reversed.Value.State.CycleCompleted);
        Assert.Empty(reversed.Value.State.AvailableRewards);
        var replay = await Reverse(env, id, serial, "reverse-final");
        Assert.Equal(JsonSerializer.Serialize(reversed.Value), JsonSerializer.Serialize(replay.Value));
        Assert.True((await env.SendAsOperatorAsync(id, Slug, new RedeemStampRewardCommand(serial, rewardId, "void-redeem"))).IsFailure);
        var newEarn = await Earn(env, id, serial, "final-again");
        Assert.True(newEarn.IsSuccess, newEarn.Error); Assert.NotEqual(rewardId, Assert.Single(newEarn.Value.UnlockedRewards).Id);
        await env.TenantReadAsync(id, Slug, async db =>
        {
            var reversal = await db.StampTransactions.SingleAsync(x => x.Type == StampTransactionType.Reversal);
            Assert.Equal(-1, reversal.Delta); Assert.NotNull(reversal.ReversesTransactionId); Assert.NotNull(reversal.OperatorUserId);
            Assert.Equal(EarnedStampRewardStatus.Voided, (await db.EarnedStampRewards.SingleAsync(x => x.Id == rewardId)).Status);
            return true;
        });
    }

    [Fact]
    public async Task Concurrent_redemption_has_one_winner_does_not_consume_stamps_and_blocks_reversal()
    {
        await using var env = await Env.CreateAsync();
        var (id, serial) = await SetupAsync(env, 4);
        var earned = await Earn(env, id, serial, "unlock");
        var reward = Assert.Single(earned.Value.UnlockedRewards);
        var outcomes = await Task.WhenAll(env.SendAsOperatorAsync(id, Slug, new RedeemStampRewardCommand(serial, reward.Id, "redeem-a")),
            env.SendAsOperatorAsync(id, Slug, new RedeemStampRewardCommand(serial, reward.Id, "redeem-b")));
        Assert.Single(outcomes, x => x.IsSuccess); Assert.Single(outcomes, x => x.IsFailure);
        var winnerKey = outcomes[0].IsSuccess ? "redeem-a" : "redeem-b";
        var retry = await env.SendAsOperatorAsync(id, Slug, new RedeemStampRewardCommand(serial, reward.Id, winnerKey));
        Assert.True(retry.IsSuccess); Assert.Equal(5, retry.Value.State.CurrentStamps);
        Assert.Empty(retry.Value.State.AvailableRewards);
        Assert.False(retry.Value.State.CanReverseLastEarn);
        var reversal = await Reverse(env, id, serial, "blocked");
        Assert.True(reversal.IsFailure); Assert.Contains("canjeada", reversal.Error);
        await env.TenantReadAsync(id, Slug, async db =>
        {
            var row = await db.EarnedStampRewards.SingleAsync();
            Assert.NotNull(row.RedeemedAt); Assert.NotNull(row.RedeemedByUserId);
            Assert.Empty(await db.StampTransactions.Where(x => x.Type == StampTransactionType.Reversal).ToListAsync());
            return true;
        });
    }

    [Fact]
    public async Task Concurrent_earn_and_retry_do_not_lose_or_duplicate_updates()
    {
        await using var env = await Env.CreateAsync();
        var (id, serial) = await SetupAsync(env);
        var outcomes = await Task.WhenAll(Earn(env, id, serial, "a"), Earn(env, id, serial, "b"));
        Assert.All(outcomes, x => Assert.True(x.IsSuccess, x.Error));
        Assert.Equal(new[] { 1, 2 }, outcomes.Select(x => x.Value.State.CurrentStamps).OrderBy(x => x));
        var duplicates = await Task.WhenAll(Earn(env, id, serial, "same"), Earn(env, id, serial, "same"));
        Assert.All(duplicates, x => Assert.True(x.IsSuccess, x.Error));
        Assert.All(duplicates, x => Assert.Equal(3, x.Value.State.CurrentStamps));
        await env.TenantReadAsync(id, Slug, async db =>
        {
            Assert.Equal(3, await db.StampTransactions.CountAsync(x => x.Type == StampTransactionType.Earn));
            Assert.Equal(3, await db.StampOperationReceipts.CountAsync()); return true;
        });
        var other = await env.JoinAsync(id, Slug, "Otra", "Stamp", "6465550002");
        Assert.True((await Earn(env, id, other.Value.SerialNumber, "same")).IsFailure);
    }

    [Theory]
    [InlineData(StampReversalPolicy.None, false, false)]
    [InlineData(StampReversalPolicy.AdminOnly, false, true)]
    [InlineData(StampReversalPolicy.AdminOnly, true, false)]
    [InlineData(StampReversalPolicy.AdminAndCashier, true, true)]
    [InlineData(StampReversalPolicy.AdminAndCashier, false, true)]
    public async Task Reversal_uses_cycle_policy_and_real_operator_role(StampReversalPolicy policy, bool cashier, bool allowed)
    {
        await using var env = await Env.CreateAsync();
        var (id, serial) = await SetupAsync(env, policy: policy, cashier: cashier);
        var earned = await Earn(env, id, serial, "earn");
        Assert.True(earned.IsSuccess);
        Assert.Equal(allowed, earned.Value.State.CanReverseLastEarn);
        if (!cashier) Assert.True((await env.ConfigureStampsAsync(id, Slug, 0, policy: StampReversalPolicy.None)).IsSuccess);
        var reverse = await Reverse(env, id, serial, "reverse");
        Assert.Equal(allowed, reverse.IsSuccess);
    }

    [Fact]
    public async Task Concurrent_rollover_creates_one_cycle_and_reversal_never_reopens_an_older_cycle()
    {
        await using var env = await Env.CreateAsync();
        var (id, serial) = await SetupAsync(env, 9);
        Assert.True((await Earn(env, id, serial, "complete")).IsSuccess);
        var results = await Task.WhenAll(Earn(env, id, serial, "roll-a"), Earn(env, id, serial, "roll-b"));
        Assert.All(results, r => Assert.True(r.IsSuccess, r.Error));
        Assert.All(results, r => Assert.Equal(2, r.Value.State.CycleSequence));
        Assert.Equal(new[] { 1, 2 }, results.Select(x => x.Value.State.CurrentStamps).OrderBy(x => x));
        var first = await Reverse(env, id, serial, "undo-b");
        Assert.Equal(1, first.Value.State.CurrentStamps);
        var second = await Reverse(env, id, serial, "undo-a");
        Assert.Equal(0, second.Value.State.CurrentStamps);
        Assert.True((await Reverse(env, id, serial, "cannot-undo-old")).IsFailure);
        await env.TenantReadAsync(id, Slug, async db =>
        {
            Assert.Equal(2, await db.StampCycles.CountAsync());
            Assert.Equal(10, (await db.StampCycles.SingleAsync(x => x.Sequence == 1)).CurrentStamps);
            return true;
        });
    }

    [Fact]
    public async Task Earn_and_reversal_concurrent_keep_ledger_and_progress_consistent()
    {
        await using var env = await Env.CreateAsync();
        var (id, serial) = await SetupAsync(env);
        Assert.True((await Earn(env, id, serial, "initial")).IsSuccess);
        var operations = await Task.WhenAll(Earn(env, id, serial, "concurrent"), Reverse(env, id, serial, "undo"));
        Assert.All(operations, r => Assert.True(r.IsSuccess, r.Error));
        await env.TenantReadAsync(id, Slug, async db =>
        {
            Assert.Equal(1, (await db.StampCycles.SingleAsync()).CurrentStamps);
            Assert.Equal(1, await db.StampTransactions.SumAsync(x => x.Delta));
            return true;
        });
    }

    [Fact]
    public async Task Points_and_cross_tenant_operations_are_rejected_and_quantity_is_not_accepted()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StampsController.OperationRequest>("{\"IdempotencyKey\":\"k\",\"quantity\":100}"));
        await using var env = await Env.CreateAsync();
        var (id, serial) = await SetupAsync(env);
        var points = await env.ProvisionAsync("points-ops", "Points", "owner", "Owner123!");
        var rejected = await env.SendAsOperatorAsync(points.Value.TenantId, "points-ops", new EarnStampCommand(serial, "key"));
        Assert.True(rejected.IsFailure); Assert.Contains("Sellos", rejected.Error);
        var b = await env.ProvisionAsync("stamp-other", "Other", "owner", "Owner123!", programType: LoyaltyProgramType.Stamps);
        var cross = await env.SendAsOperatorAsync(b.Value.TenantId, "stamp-other", new EarnStampCommand(serial, "cross"));
        Assert.True(cross.IsFailure);
        Assert.True((await env.SendAsync(id, Slug, new EarnStampCommand(serial, "anonymous"))).IsFailure);
    }

    [Fact]
    public async Task Earned_snapshot_survives_definition_removal_and_receipt_key_is_sql_unique()
    {
        await using var env = await Env.CreateAsync();
        var (id, serial) = await SetupAsync(env, 4);
        var result = await Earn(env, id, serial, "snapshot");
        var rewardId = Assert.Single(result.Value.UnlockedRewards).Id;
        await env.TenantReadAsync(id, Slug, async db =>
        {
            var definition = await db.StampRewardDefinitions.SingleAsync(x => x.RequiredStamps == 5);
            db.Remove(definition); await db.SaveChangesAsync();
            var receipt = await db.StampOperationReceipts.SingleAsync();
            db.Add(new StampOperationReceipt(id, receipt.IdempotencyKey, "Earn", serial, null,
                receipt.OperatorUserId, receipt.ResponseJson, DateTime.UtcNow));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            return true;
        });
        var state = await env.SendAsOperatorAsync(id, Slug, new GetStampStateQuery(serial));
        Assert.Equal("Mascarilla", Assert.Single(state.Value.AvailableRewards).Name);
        Assert.True((await env.SendAsOperatorAsync(id, Slug, new RedeemStampRewardCommand(serial, rewardId, "redeem-snapshot"))).IsSuccess);
    }
}
