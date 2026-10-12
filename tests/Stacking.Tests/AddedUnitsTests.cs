using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

/// <summary>
/// An add of units as a new stack is judged from the container's counts before and after it, never from what the
/// game returned or threw, so units it placed are never added twice and units it spilled are never left over. The
/// split and the station steps both decide this way.
/// </summary>
public class AddedUnitsTests
{
    private const long Mine = 8;
    private const long New = 100;
    private const long Other = 101;

    private static readonly Dictionary<long, int> Before = new() { [Mine] = 3 };

    /// <summary>A split dropped onto a free cell, where the game made the stack and then threw: the split goes on with that stack.</summary>
    [Fact]
    public void One_new_stack_of_exactly_the_units_is_the_add_s_own_even_when_the_add_threw()
    {
        var result = AddedUnits.Judge(Before, new Dictionary<long, int> { [Mine] = 3, [New] = 4 }, units: 4);

        Assert.Equal(new AddResult.Started(New), result);
    }

    [Fact]
    public void A_container_that_did_not_change_means_the_add_did_nothing()
    {
        var result = AddedUnits.Judge(Before, new Dictionary<long, int>(Before), units: 4);

        Assert.True(Assert.IsType<AddResult.Undo>(result).DidNothing);
    }

    [Fact]
    public void A_stack_already_there_that_grew_is_put_back_to_its_count_and_the_spill_is_taken_away()
    {
        var result = AddedUnits.Judge(Before, new Dictionary<long, int> { [Mine] = 4, [New] = 3 }, units: 4);

        var undo = Assert.IsType<AddResult.Undo>(result);
        Assert.Equal(new[] { New }, undo.NewStacks);
        Assert.Equal(3, undo.Grown[Mine]);
    }

    [Fact]
    public void A_new_stack_with_the_wrong_count_is_taken_away()
    {
        var result = AddedUnits.Judge(Before, new Dictionary<long, int> { [Mine] = 3, [New] = 5 }, units: 4);

        var undo = Assert.IsType<AddResult.Undo>(result);
        Assert.Equal(new[] { New }, undo.NewStacks);
        Assert.Empty(undo.Grown);
    }

    [Fact]
    public void Two_new_stacks_are_both_taken_away()
    {
        var result = AddedUnits.Judge(Before, new Dictionary<long, int> { [Mine] = 3, [New] = 2, [Other] = 2 }, units: 4);

        Assert.Equal(new[] { New, Other }, Assert.IsType<AddResult.Undo>(result).NewStacks);
    }

    /// <summary>A stack that shrank, as when the game merged the units elsewhere, is never counted as grown and never topped back down.</summary>
    [Fact]
    public void A_stack_that_shrank_is_not_counted_as_grown()
    {
        var result = AddedUnits.Judge(Before, new Dictionary<long, int> { [Mine] = 2 }, units: 4);

        Assert.True(Assert.IsType<AddResult.Undo>(result).DidNothing);
    }

    [Fact]
    public void Units_too_many_counts_new_stacks_and_growth_only()
    {
        var tooMany = AddedUnits.UnitsTooMany(new Dictionary<long, int> { [Mine] = 3, [Other] = 5 }, new Dictionary<long, int> { [Mine] = 4, [Other] = 2, [New] = 3 });

        Assert.Equal(4, tooMany);
    }

    /// <summary>An add that threw after the game dropped its units on the ground: the one new drop of exactly the units is the add's.</summary>
    [Fact]
    public void A_new_stack_of_exactly_the_units_on_the_ground_is_found()
    {
        var dropped = AddedUnits.NewStacksOf(Before, new Dictionary<long, int> { [Mine] = 3, [New] = 4 }, units: 4);

        Assert.Equal(new[] { New }, dropped);
    }

    /// <summary>A stack already on the ground, or a new one of another count, is never taken for the add's.</summary>
    [Fact]
    public void Stacks_already_on_the_ground_or_of_another_count_are_never_the_add_s()
    {
        var dropped = AddedUnits.NewStacksOf(new Dictionary<long, int> { [Mine] = 4 }, new Dictionary<long, int> { [Mine] = 4, [New] = 5 }, units: 4);

        Assert.Empty(dropped);
    }

    [Fact]
    public void Several_new_stacks_of_the_units_on_the_ground_are_all_found_so_none_is_guessed()
    {
        var dropped = AddedUnits.NewStacksOf(Before, new Dictionary<long, int> { [Mine] = 3, [Other] = 4, [New] = 4 }, units: 4);

        Assert.Equal(new[] { New, Other }, dropped);
    }
}
