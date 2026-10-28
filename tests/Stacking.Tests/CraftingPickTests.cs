using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

/// <summary>
/// The player's report: a recipe needing two glass and one metal said "not enough materials" with stacks of glass and
/// metal in the bag, because the game fills a recipe only with whole items that fit what is needed.
/// </summary>
public class CraftingPickTests
{
    private const long Bench = 1;
    private const long Bag = 2;
    private const long Shelf = 3;
    private const int Glass = 501;
    private const int Metal = 502;

    private static PickCandidate Stack(long id, long owner, int configId, int count) => new(id, owner, configId, count, IsCut: true);

    private static PickCandidate Whole(long id, long owner, int configId, int count = 1) => new(id, owner, configId, count, IsCut: false);

    private static PickPlan.Filled Filled(PickPlan plan) => Assert.IsType<PickPlan.Filled>(plan);

    [Fact]
    public void Two_glass_and_one_metal_come_off_the_stacks_in_the_bag()
    {
        var candidates = new[]
        {
            Stack(10, Bag, Glass, 8), Stack(11, Bag, Glass, 5), Stack(12, Bag, Glass, 4),
            Stack(20, Bag, Metal, 4), Stack(21, Bag, Metal, 4),
        };

        var plan = Filled(CraftingPick.Plan(new Dictionary<int, int> { [Glass] = 2, [Metal] = 1 }, candidates));

        Assert.Empty(plan.Whole);
        Assert.Equal(new[] { new UnitTake(10, Bag, 2), new UnitTake(20, Bag, 1) }, plan.Units);
    }

    [Fact]
    public void A_stack_holding_exactly_what_is_needed_still_gives_single_units()
    {
        var plan = Filled(CraftingPick.Plan(new Dictionary<int, int> { [Glass] = 2 }, new[] { Stack(10, Bag, Glass, 2) }));

        Assert.Empty(plan.Whole);
        Assert.Equal(new[] { new UnitTake(10, Bag, 2) }, plan.Units);
    }

    [Fact]
    public void Single_items_go_in_whole_before_any_unit_comes_off_a_stack()
    {
        var candidates = new[] { Stack(10, Bag, Glass, 5), Whole(11, Bag, Glass) };

        var plan = Filled(CraftingPick.Plan(new Dictionary<int, int> { [Glass] = 2 }, candidates));

        Assert.Equal(new[] { new WholePick(11, Bag) }, plan.Whole);
        Assert.Equal(new[] { new UnitTake(10, Bag, 1) }, plan.Units);
    }

    [Fact]
    public void A_stack_the_rule_does_not_cut_goes_in_whole_when_it_fits()
    {
        var candidates = new[] { Stack(10, Bag, Glass, 5), Whole(11, Bag, Glass, count: 2) };

        var plan = Filled(CraftingPick.Plan(new Dictionary<int, int> { [Glass] = 2 }, candidates));

        Assert.Equal(new[] { new WholePick(11, Bag) }, plan.Whole);
        Assert.Empty(plan.Units);
    }

    [Fact]
    public void A_stack_the_rule_does_not_cut_is_never_split_to_fit()
    {
        var plan = CraftingPick.Plan(new Dictionary<int, int> { [Glass] = 2 }, new[] { Whole(11, Bag, Glass, count: 5) });

        Assert.IsType<PickPlan.Short>(plan);
    }

    [Fact]
    public void Units_come_from_the_containers_in_fill_order()
    {
        var candidates = new[] { Stack(10, Bag, Glass, 3), Stack(11, Shelf, Glass, 5) };

        var plan = Filled(CraftingPick.Plan(new Dictionary<int, int> { [Glass] = 4 }, candidates));

        Assert.Equal(new[] { new UnitTake(10, Bag, 3), new UnitTake(11, Shelf, 1) }, plan.Units);
    }

    [Fact]
    public void Too_few_units_plan_nothing()
    {
        var candidates = new[] { Whole(11, Bag, Glass), Stack(10, Bag, Glass, 2), Stack(20, Bag, Metal, 4) };

        var plan = CraftingPick.Plan(new Dictionary<int, int> { [Metal] = 1, [Glass] = 4 }, candidates);

        Assert.IsType<PickPlan.Short>(plan);
    }

    [Fact]
    public void Items_on_the_bench_in_the_bag_and_on_a_shelf_add_up_to_one_need()
    {
        var candidates = new[] { Whole(30, Bench, Glass), Whole(11, Bag, Glass), Stack(12, Shelf, Glass, 3) };

        var plan = Filled(CraftingPick.Plan(new Dictionary<int, int> { [Glass] = 4 }, candidates));

        Assert.Equal(new[] { new WholePick(30, Bench), new WholePick(11, Bag) }, plan.Whole);
        Assert.Equal(new[] { new UnitTake(12, Shelf, 2) }, plan.Units);
    }

    [Fact]
    public void An_item_with_no_count_counts_as_one_unit()
    {
        var plan = Filled(CraftingPick.Plan(new Dictionary<int, int> { [Glass] = 1 }, new[] { Whole(11, Bag, Glass, count: 0) }));

        Assert.Equal(new[] { new WholePick(11, Bag) }, plan.Whole);
    }

    [Fact]
    public void Candidates_keep_the_fill_order_and_put_the_largest_first_within_a_container()
    {
        var held = new List<List<PickCandidate>>
        {
            new() { Whole(30, Bench, Glass), Stack(31, Bench, Glass, 2) },
            new() { Stack(10, Bag, Glass, 3), Whole(99, Bag, 999), Stack(11, Bag, Metal, 6), Stack(12, Bag, Glass, 3) },
        };

        var candidates = CraftingPick.Candidates(held, new Dictionary<int, int> { [Glass] = 4, [Metal] = 1 });

        // The bench comes first though the bag holds more; a tie keeps the cache's order; an item no recipe needs drops out.
        Assert.Equal(new long[] { 31, 30, 11, 10, 12 }, candidates.Select(candidate => candidate.ItemId));
    }

    [Fact]
    public void The_games_pick_stands_with_no_stack_the_rule_cuts()
    {
        var candidates = new[] { Whole(11, Bag, Glass), Whole(12, Bag, Glass) };

        Assert.False(CraftingPick.NeedsUnits(null, candidates));
    }

    [Fact]
    public void Units_are_taken_when_the_game_found_nothing_or_picked_a_stack_the_rule_cuts()
    {
        var candidates = new[] { Whole(11, Bag, Glass), Stack(10, Bag, Glass, 5) };

        Assert.True(CraftingPick.NeedsUnits(null, candidates));
        Assert.True(CraftingPick.NeedsUnits(new long[] { 11, 10 }, candidates));
        Assert.False(CraftingPick.NeedsUnits(new long[] { 11 }, candidates));
    }

    [Fact]
    public void Only_the_plans_items_already_on_the_bench_stay_while_it_is_cleared()
    {
        var kept = CraftingPick.KeptOnBench(new[] { new WholePick(30, Bench), new WholePick(11, Bag) }, Bench);

        Assert.Equal(new long[] { 30 }, kept);
    }

    [Fact]
    public void The_pick_made_again_lists_the_units_then_the_whole_items_with_where_each_came_from()
    {
        var picks = CraftingPick.Picks(new[] { new BenchUnit(40, Shelf) }, new[] { new WholePick(30, Bench), new WholePick(11, Bag) });

        Assert.Equal(new[] { (40L, Shelf), (30L, Bench), (11L, Bag) }, picks);
    }
}
