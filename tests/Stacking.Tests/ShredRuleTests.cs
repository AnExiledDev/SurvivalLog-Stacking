using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

/// <summary>
/// The player's report: a stack of wires in the shredder was shredded all at once and gave back one wire's worth.
/// Every unit of a stack gets shredded, and each one gives its own yield.
/// </summary>
public class ShredRuleTests
{
    private const long Shredded = 41;

    private static ShredRemoval Removal(bool isSettlingShred = true, long removedItemId = Shredded, int count = 12) =>
        ShredRule.Removal(isSettlingShred, removedItemId, Shredded, count);

    [Fact]
    public void A_finished_shred_takes_one_unit_off_a_stack_and_leaves_the_rest_to_shred()
    {
        Assert.Equal(ShredRemoval.OneUnit, Removal());
    }

    [Fact]
    public void A_single_item_is_removed_as_the_game_removes_it()
    {
        Assert.Equal(ShredRemoval.Vanilla, Removal(count: 1));
    }

    [Fact]
    public void Removing_a_stack_outside_a_shred_is_the_game_s_own_removal()
    {
        Assert.Equal(ShredRemoval.Vanilla, Removal(isSettlingShred: false));
    }

    [Fact]
    public void A_different_item_removed_during_a_shred_is_the_game_s_own_removal()
    {
        Assert.Equal(ShredRemoval.Vanilla, Removal(removedItemId: Shredded + 1));
    }
}
