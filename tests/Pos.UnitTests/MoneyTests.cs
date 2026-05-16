using FluentAssertions;
using Pos.BuildingBlocks;
using Xunit;

namespace Pos.UnitTests;

public class MoneyTests
{
    [Fact]
    public void Add_should_sum_amounts_when_same_currency()
    {
        var a = new Money(10m, "USD");
        var b = new Money(2.50m, "USD");
        (a + b).Amount.Should().Be(12.50m);
    }

    [Fact]
    public void Add_should_throw_on_currency_mismatch()
    {
        var a = new Money(10m, "USD");
        var b = new Money(10m, "EUR");
        Action act = () => _ = a + b;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Multiply_should_round_to_4dp()
    {
        var a = new Money(1m, "USD");
        a.Multiply(1.123456789m).Amount.Should().Be(1.1235m);
    }
}
