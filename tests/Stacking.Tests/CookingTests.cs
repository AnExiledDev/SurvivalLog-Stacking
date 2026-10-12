using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

public class CookingCountsTests
{
    private const int Strawberry = 501;
    private const int Fruit = 7;
    private const int Battery = 900;

    private static readonly ItemUse Stackable = new(StackLimit: 20, Category: 1);
    private static readonly ItemUse Single = new(StackLimit: 1, Category: 1);
    private static readonly ItemUse Appliance = new(StackLimit: 5, Category: 4);

    [Fact]
    public void A_stack_of_three_counts_as_three_ingredients_like_three_single_items_do()
    {
        var extras = CookingCounts.Extras(new[] { new BenchStack(Strawberry, Fruit, 3, Stackable) });

        Assert.Equal(2, extras.ByItem[Strawberry]);
        Assert.Equal(2, extras.ByTag[Fruit]);
    }

    [Fact]
    public void Stacks_of_one_item_add_up_across_the_bench()
    {
        var extras = CookingCounts.Extras(new[] { new BenchStack(Strawberry, Fruit, 3, Stackable), new BenchStack(Strawberry, Fruit, 4, Stackable) });

        Assert.Equal(5, extras.ByItem[Strawberry]);
    }

    [Fact]
    public void An_unstackable_item_holding_portions_still_counts_as_one_ingredient()
    {
        // The game splits portions off before cooking and removes such an item whole, so its count is not units.
        var extras = CookingCounts.Extras(new[] { new BenchStack(Strawberry, Fruit, 3, Single) });

        Assert.True(extras.IsEmpty);
    }

    [Fact]
    public void An_appliance_keeps_the_games_count_even_without_uses_because_the_game_may_leave_it_in_place()
    {
        var extras = CookingCounts.Extras(new[] { new BenchStack(Battery, 3, 4, Appliance) });

        Assert.True(extras.IsEmpty);
    }

    [Fact]
    public void A_stack_reading_zero_units_counts_as_the_one_item_the_game_sees()
    {
        var extras = CookingCounts.Extras(new[] { new BenchStack(Strawberry, Fruit, 0, Stackable) });

        Assert.True(extras.IsEmpty);
    }
}

public class SettleLedgerTests
{
    private const int Strawberry = 501;
    private const int Salt = 502;
    private const int Battery = 900;

    private static readonly ItemUse Stackable = new(StackLimit: 20, Category: 1);
    private static readonly ItemUse Single = new(StackLimit: 1, Category: 1);
    private static readonly ItemUse Appliance = new(StackLimit: 5, Category: 4);

    private static SettleLedger Ledger(Dictionary<int, int> need, params long[] benchIds) =>
        SettleLedger.Open(new[] { need }, benchIds);

    [Fact]
    public void Three_smoothies_from_a_stack_of_three_strawberries_use_up_all_three()
    {
        var ledger = Ledger(new() { [Strawberry] = 3 }, 1);

        // The game takes one unit itself; the ledger owes the other two.
        Assert.Equal(2, ledger.ExtraUnits(1, Strawberry, 3, Stackable));
    }

    [Fact]
    public void A_stack_larger_than_the_recipes_need_keeps_what_was_not_cooked()
    {
        var ledger = Ledger(new() { [Strawberry] = 3 }, 1);

        Assert.Equal(2, ledger.ExtraUnits(1, Strawberry, 5, Stackable));
    }

    [Fact]
    public void Two_stacks_covering_a_need_of_three_give_up_exactly_three_units()
    {
        var ledger = Ledger(new() { [Strawberry] = 3 }, 1, 2);

        var first = ledger.ExtraUnits(1, Strawberry, 2, Stackable);
        var second = ledger.ExtraUnits(2, Strawberry, 2, Stackable);

        // Plus the one unit the game takes from each stack: 2 + 1 = 3.
        Assert.Equal(1, first);
        Assert.Equal(0, second);
    }

    [Fact]
    public void Need_is_split_across_recipes_and_added_up()
    {
        var ledger = SettleLedger.Open(new[] { new Dictionary<int, int> { [Strawberry] = 1 }, new Dictionary<int, int> { [Strawberry] = 2 } }, new long[] { 1 });

        Assert.Equal(2, ledger.ExtraUnits(1, Strawberry, 3, Stackable));
    }

    [Fact]
    public void A_seasoning_stack_no_recipe_used_gives_up_only_the_unit_the_game_takes()
    {
        var ledger = Ledger(new() { [Strawberry] = 3 }, 1, 2);

        Assert.Equal(0, ledger.ExtraUnits(2, Salt, 6, Stackable));
    }

    [Fact]
    public void An_item_that_was_not_on_the_bench_is_never_touched()
    {
        var ledger = Ledger(new() { [Strawberry] = 3 }, 1);

        Assert.Equal(0, ledger.ExtraUnits(99, Strawberry, 3, Stackable));
    }

    [Fact]
    public void A_stack_is_settled_once_even_if_the_game_removes_from_it_again()
    {
        var ledger = Ledger(new() { [Strawberry] = 6 }, 1);

        ledger.ExtraUnits(1, Strawberry, 3, Stackable);

        Assert.Equal(0, ledger.ExtraUnits(1, Strawberry, 3, Stackable));
    }

    [Fact]
    public void An_unstackable_item_holding_portions_is_left_to_the_game_which_removes_it_whole()
    {
        var ledger = Ledger(new() { [Strawberry] = 3 }, 1);

        Assert.Equal(0, ledger.ExtraUnits(1, Strawberry, 3, Single));
    }

    [Fact]
    public void An_appliance_is_left_to_the_game_even_without_uses()
    {
        var ledger = Ledger(new() { [Battery] = 4 }, 1);

        Assert.Equal(0, ledger.ExtraUnits(1, Battery, 4, Appliance));
    }

    [Fact]
    public void Every_unit_the_planner_counted_is_used_up_when_the_recipes_use_them_all()
    {
        var bench = new[] { (Id: 1L, Stack: new BenchStack(Strawberry, 7, 3, Stackable)), (Id: 2L, Stack: new BenchStack(Strawberry, 7, 2, Stackable)) };
        var counted = bench.Length + CookingCounts.Extras(bench.Select(b => b.Stack)).ByItem[Strawberry];
        var ledger = Ledger(new() { [Strawberry] = counted }, 1, 2);

        // The game takes one unit per stack it cooks with; the ledger owes the rest.
        var removed = bench.Sum(b => 1 + ledger.ExtraUnits(b.Id, b.Stack.ConfigId, b.Stack.Count, b.Stack.Use));

        Assert.Equal(5, removed);
    }
}
