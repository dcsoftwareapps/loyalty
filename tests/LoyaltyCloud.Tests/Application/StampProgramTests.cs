using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using Xunit;

namespace LoyaltyCloud.Tests.Application;

[Trait("Category", "StampFoundation")]
public sealed class StampProgramTests
{
    [Theory]
    [InlineData(10, 0, true)]
    [InlineData(10, 2, true)]
    [InlineData(12, 11, true)]
    [InlineData(10, 10, false)]
    [InlineData(10, -1, false)]
    [InlineData(13, 0, false)]
    [InlineData(1, 0, false)]
    public void V1_configuration_validates_product_limits(int max, int welcome, bool valid)
    {
        var command = new SetStampProgramCommand(max, welcome, StampReversalPolicy.None, [new(max, "Reward")]);
        Assert.Equal(valid, new SetStampProgramValidator().Validate(command).IsValid);
    }

    [Fact]
    public void Reward_validation_requires_distinct_valid_thresholds_and_final_reward()
    {
        var validator = new SetStampProgramValidator();
        Assert.False(validator.Validate(new SetStampProgramCommand(10, 0, StampReversalPolicy.None, [new(5, "Reward")])).IsValid);
        Assert.False(validator.Validate(new SetStampProgramCommand(10, 0, StampReversalPolicy.None, [new(10, "A"), new(10, "B")])).IsValid);
        Assert.False(validator.Validate(new SetStampProgramCommand(10, 0, StampReversalPolicy.None, [new(10, "A"), new(11, "B")])).IsValid);
        Assert.False(validator.Validate(new SetStampProgramCommand(10, 0, StampReversalPolicy.None, [new(0, "A"), new(10, "B")])).IsValid);
        Assert.True(validator.Validate(new SetStampProgramCommand(10, 0, StampReversalPolicy.AdminAndCashier, [new(5, "A"), new(10, "B")])).IsValid);
    }

    [Fact]
    public void Domain_has_no_v1_maximum_and_checks_reward_and_cycle_tenant()
    {
        var program = new StampProgramVersion(Guid.NewGuid(), Guid.NewGuid(), 1, 20, 2, StampReversalPolicy.None, Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(20, program.MaxStamps);
        Assert.Throws<ArgumentOutOfRangeException>(() => new StampRewardDefinition(Guid.NewGuid(), program, 21, "Reward", null));
        var card = new LoyaltyCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "TEST", DateTime.UtcNow);
        Assert.Throws<ArgumentException>(() => new StampCycle(Guid.NewGuid(), card, program, DateTime.UtcNow));
        var ownCard = new LoyaltyCard(Guid.NewGuid(), program.TenantId, Guid.NewGuid(), "OWN", DateTime.UtcNow);
        var laterCycle = new StampCycle(Guid.NewGuid(), ownCard, program, DateTime.UtcNow, 2);
        Assert.Equal(0, laterCycle.CurrentStamps);
        Assert.Throws<ArgumentException>(() => StampTransaction.Welcome(laterCycle, DateTime.UtcNow));
    }
}
