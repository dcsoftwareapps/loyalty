using KBeauty.Loyalty.Domain.ValueObjects;
using Xunit;

namespace KBeauty.Loyalty.Tests.Domain;

public class PointsValueObjectTests
{
    [Fact]
    public void Points_ShouldNotAllowNegativeValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Points(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Points(-1000));
    }

    [Fact]
    public void Points_ShouldAllowZero()
    {
        var p = new Points(0);
        Assert.Equal(0, p.Value);
        Assert.Equal(Points.Zero, p);
    }

    [Fact]
    public void Points_ShouldSupportAdditionOperator()
    {
        var a = new Points(100);
        var b = new Points(250);

        var sum = a + b;

        Assert.Equal(350, sum.Value);
    }

    [Fact]
    public void Points_Subtraction_ShouldThrow_WhenResultNegative()
    {
        var a = new Points(50);
        var b = new Points(100);

        Assert.Throws<ArgumentOutOfRangeException>(() => a - b);
    }

    [Fact]
    public void Points_ShouldSupportComparisonOperators()
    {
        var low = new Points(50);
        var high = new Points(500);

        Assert.True(low < high);
        Assert.True(high > low);
        Assert.True(low <= new Points(50));
        Assert.True(high >= new Points(500));
        Assert.False(low > high);
    }

    [Fact]
    public void Points_ShouldImplicitlyConvertFromInt()
    {
        Points p = 250;
        Assert.Equal(250, p.Value);
    }

    [Fact]
    public void Points_ShouldImplicitlyConvertToInt()
    {
        var p = new Points(800);
        int value = p;
        Assert.Equal(800, value);
    }
}
