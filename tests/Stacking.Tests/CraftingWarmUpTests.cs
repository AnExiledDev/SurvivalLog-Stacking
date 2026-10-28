using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

/// <summary>
/// The first recipe pick at the crafting table stuttered while the fill compiled on the game's frame. The warm-up runs
/// the fill once at load on made-up items, so it only helps if that run really goes the whole way: units off a stack,
/// onto the bench and back.
/// </summary>
public class CraftingWarmUpTests
{
    [Fact]
    public void The_made_up_fill_takes_three_units_off_a_stack_onto_the_bench()
    {
        var landed = Assert.IsType<UnitsTaken.Landed>(CraftingWarmUp.Run());

        Assert.Equal(3, landed.OnBench.Count);
        Assert.All(landed.OnBench, unit => Assert.Equal(2, unit.FromOwnerId));
    }

    [Fact]
    public void The_warm_up_can_run_again()
    {
        CraftingWarmUp.Run();

        Assert.IsType<UnitsTaken.Landed>(CraftingWarmUp.Run());
    }
}
