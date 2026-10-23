using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

/// <summary>
/// A harvest adds crops at the field's speed of decay (1). In a backpack with the refrigerated upgrade the stack decays
/// slower, and the game topping up that stack writes the harvest's time left onto it unchanged, which shortens it.
/// </summary>
public class TopUpFreshnessTests
{
    private const int Hour = 500;
    private const int Life = 72;
    private const float Field = 1f;
    private const float CoolBag = 0.5f;

    [Fact]
    public void A_harvest_written_onto_a_cooler_bag_stack_is_converted()
    {
        Assert.True(TopUpFreshness.NeedsRescale(Hour, Life, CoolBag, Hour, Life, Field));
    }

    [Fact]
    public void A_stack_at_the_same_speed_as_the_add_is_left_alone()
    {
        Assert.False(TopUpFreshness.NeedsRescale(Hour, Life, Field, Hour, Life, Field));
    }

    [Fact]
    public void A_stack_still_holding_its_own_values_is_left_alone()
    {
        // The bag already converted this stack: 72 hours at the field speed is 144 at half speed.
        Assert.False(TopUpFreshness.NeedsRescale(Hour, Life * 2, CoolBag, Hour, Life, Field));
        Assert.False(TopUpFreshness.NeedsRescale(Hour - 3, Life, CoolBag, Hour, Life, Field));
    }

    [Fact]
    public void Items_that_never_spoil_are_left_alone()
    {
        Assert.False(TopUpFreshness.NeedsRescale(Hour, 0, CoolBag, Hour, 0, Field));
    }

    [Fact]
    public void A_speed_of_decay_off_by_rounding_counts_as_the_same()
    {
        Assert.False(TopUpFreshness.NeedsRescale(Hour, Life, 0.9995f, Hour, Life, Field));
    }

    [Fact]
    public void An_unknown_speed_of_decay_is_left_to_the_game()
    {
        Assert.False(TopUpFreshness.NeedsRescale(Hour, Life, CoolBag, Hour, Life, 0f));
        Assert.False(TopUpFreshness.NeedsRescale(Hour, Life, 0f, Hour, Life, Field));
    }
}
