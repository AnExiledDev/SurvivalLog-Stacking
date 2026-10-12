using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

/// <summary>
/// The player's rule: a stack never sits in a cooking or crafting station. Two eggs put in to cook take two slots,
/// because a station counts one stacked slot as one ingredient.
/// </summary>
public class StationRuleTests
{
    private static StationEntry Entry(bool isIntoStation = true, bool isWithinOneBag = false, int count = 5, bool isNewlyStackable = true, bool hasOwnWeight = false) =>
        StationRule.Entry(isIntoStation, isWithinOneBag, count, isNewlyStackable, hasOwnWeight);

    [Fact]
    public void A_stack_moved_into_a_station_puts_in_one_unit()
    {
        Assert.Equal(StationEntry.OneUnit, Entry());
    }

    [Fact]
    public void A_single_item_moves_into_a_station_as_the_game_moves_it()
    {
        Assert.Equal(StationEntry.Vanilla, Entry(count: 1));
    }

    [Fact]
    public void Rearranging_inside_a_station_is_the_game_s_own_move()
    {
        Assert.Equal(StationEntry.Vanilla, Entry(isWithinOneBag: true));
    }

    [Fact]
    public void An_item_the_game_already_stacks_goes_in_whole_as_the_game_s_stations_expect()
    {
        Assert.Equal(StationEntry.Vanilla, Entry(isNewlyStackable: false));
    }

    [Fact]
    public void An_item_carrying_its_own_weight_is_never_a_stack_so_it_moves_as_the_game_moves_it()
    {
        Assert.Equal(StationEntry.Vanilla, Entry(hasOwnWeight: true));
    }

    [Fact]
    public void A_stack_moved_anywhere_but_a_station_moves_whole()
    {
        Assert.Equal(StationEntry.Vanilla, Entry(isIntoStation: false));
    }

    [Fact]
    public void A_stack_a_whole_bag_move_left_in_a_station_sends_all_but_one_back()
    {
        Assert.Equal(4, StationRule.UnitsToSendBack(5, isNewlyStackable: true, hasOwnWeight: false));
    }

    [Theory]
    [InlineData(1, true, false)]
    [InlineData(5, false, false)]
    [InlineData(5, true, true)]
    public void A_whole_bag_move_leaves_singles_and_the_game_s_own_stacks_where_they_landed(int count, bool isNewlyStackable, bool hasOwnWeight)
    {
        Assert.Equal(0, StationRule.UnitsToSendBack(count, isNewlyStackable, hasOwnWeight));
    }
}
