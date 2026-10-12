using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

public class StackIdentityTests
{
    private const float Now = 120f;

    private static Freshness Meal(int startTime = 100, int timeLeft = 48, float frac = 0f, float scale = 1f, int uses = 1, int maxUses = 1) =>
        new(startTime, timeLeft, frac, scale, uses, maxUses);

    [Fact]
    public void Two_meals_from_one_batch_share_a_stack()
    {
        Assert.True(StackIdentity.CanShareStack(Meal(), Meal(), Now));
    }

    [Fact]
    public void A_fresher_meal_does_not_join_an_older_stack_so_neither_freshness_is_lost()
    {
        Assert.False(StackIdentity.CanShareStack(Meal(startTime: 105), Meal(), Now));
    }

    [Fact]
    public void The_fraction_of_an_hour_lost_on_the_way_into_a_bag_does_not_split_a_batch()
    {
        Assert.True(StackIdentity.CanShareStack(Meal(), Meal(frac: 0.6f), Now));
    }

    [Fact]
    public void Items_that_never_spoil_stack_whenever_they_were_picked_up()
    {
        Assert.True(StackIdentity.CanShareStack(Meal(startTime: 10, timeLeft: 0), Meal(startTime: 90, timeLeft: 0), Now));
    }

    [Fact]
    public void A_spoiling_item_never_joins_a_stack_that_does_not_spoil()
    {
        Assert.False(StackIdentity.CanShareStack(Meal(), Meal(timeLeft: 0), Now));
    }

    [Fact]
    public void Same_freshness_matches_across_spoil_rates_so_a_bag_item_can_join_a_fridge_stack()
    {
        // 28 hours left in the bag; the fridge spoils at half rate, so 56 clock hours there is the same 28 hours of shelf life.
        var inBag = Meal(startTime: 100, timeLeft: 48);
        var inFridge = Meal(startTime: 100, timeLeft: 76, scale: 0.5f);

        Assert.True(StackIdentity.CanShareStack(inBag, inFridge, Now));
    }

    [Fact]
    public void Stopped_spoiling_still_keeps_different_expiry_apart()
    {
        Assert.False(StackIdentity.CanShareStack(Meal(scale: 0f), Meal(startTime: 110, scale: 0f), Now));
    }

    [Fact]
    public void Items_holding_different_uses_do_not_merge()
    {
        Assert.False(StackIdentity.CanShareStack(Meal(uses: 2, maxUses: 4), Meal(uses: 4, maxUses: 4), Now));
    }

    [Fact]
    public void A_single_use_item_stacks_whether_the_game_wrote_zero_or_one_use()
    {
        Assert.True(StackIdentity.CanShareStack(Meal(uses: 0, maxUses: 0), Meal(uses: 1, maxUses: 1), Now));
    }

    /// <summary>The bug report: one kind of seed sat in a stack of 3 and a stack of 1 because their clocks differed.</summary>
    [Fact]
    public void Seeds_that_never_spoil_join_one_stack_whatever_their_clocks_say()
    {
        Assert.True(StackIdentity.CanJoin(Meal(startTime: 10, timeLeft: 48), Meal(startTime: 90, timeLeft: 0), Now, neverSpoils: true));
    }

    [Fact]
    public void A_seed_whose_add_gave_no_values_still_joins_the_stack()
    {
        Assert.True(StackIdentity.CanJoin(incoming: null, Meal(), nowHours: null, neverSpoils: true));
    }

    [Fact]
    public void A_never_spoiling_item_holding_other_uses_stays_apart()
    {
        Assert.False(StackIdentity.CanJoin(Meal(uses: 2, maxUses: 4), Meal(uses: 4, maxUses: 4), Now, neverSpoils: true));
    }

    [Fact]
    public void Food_keeps_the_freshness_rule_so_a_fresher_meal_starts_its_own_stack()
    {
        Assert.False(StackIdentity.CanJoin(Meal(startTime: 105), Meal(), Now, neverSpoils: false));
    }

    [Fact]
    public void Food_of_unknown_freshness_never_joins_a_stack()
    {
        Assert.False(StackIdentity.CanJoin(incoming: null, Meal(), Now, neverSpoils: false));
    }
}

public class StackLimitsTests
{
    private const int Food = 1;
    private const int Seed = 10;

    private static readonly Dictionary<int, int> FoodTo20 = new() { [Food] = 20 };

    [Fact]
    public void A_category_limit_lets_an_unstackable_item_stack_and_marks_it_for_the_freshness_guard()
    {
        var plan = StackLimits.Plan(new[] { new ItemRow(7, Food, 1, 1) }, FoodTo20, canGuardFreshness: true);

        Assert.Equal(20, plan.Limits[7]);
        Assert.Contains(7, plan.NewlyStackable);
    }

    [Fact]
    public void An_item_the_game_already_stacks_is_raised_but_keeps_the_games_own_merge_rules()
    {
        var plan = StackLimits.Plan(new[] { new ItemRow(7, Food, 10, 0) }, FoodTo20, canGuardFreshness: true);

        Assert.Equal(20, plan.Limits[7]);
        Assert.DoesNotContain(7, plan.NewlyStackable);
    }

    [Fact]
    public void A_limit_at_or_below_the_games_never_lowers_it()
    {
        var plan = StackLimits.Plan(new[] { new ItemRow(7, Food, 30, 0), new ItemRow(8, Food, 20, 0) }, FoodTo20, canGuardFreshness: true);

        Assert.Empty(plan.Limits);
    }

    [Fact]
    public void Dishes_with_portions_keep_their_limit_because_the_game_only_merges_portions_at_limit_one()
    {
        var plan = StackLimits.Plan(new[] { new ItemRow(7, Food, 1, 4) }, FoodTo20, canGuardFreshness: true);

        Assert.Empty(plan.Limits);
    }

    [Fact]
    public void Categories_left_unset_are_untouched()
    {
        var plan = StackLimits.Plan(new[] { new ItemRow(3, Seed, 1, 0) }, FoodTo20, canGuardFreshness: true);

        Assert.Empty(plan.Limits);
    }

    [Fact]
    public void Without_the_freshness_guard_nothing_new_becomes_stackable()
    {
        var plan = StackLimits.Plan(new[] { new ItemRow(7, Food, 1, 1), new ItemRow(8, Food, 10, 0) }, FoodTo20, canGuardFreshness: false);

        Assert.False(plan.Limits.ContainsKey(7));
        Assert.Equal(20, plan.Limits[8]);
        Assert.Empty(plan.NewlyStackable);
    }

    [Theory]
    [InlineData(5000, StackLimits.Highest)]
    [InlineData(-3, 0)]
    [InlineData(50, 50)]
    public void Configured_limits_are_kept_within_range(int wanted, int expected)
    {
        Assert.Equal(expected, StackLimits.Clamp(wanted));
    }

    [Fact]
    public void The_log_tells_the_player_each_categorys_game_limit_so_they_can_tune_it()
    {
        var rows = new[] { new ItemRow(7, Food, 1, 1), new ItemRow(8, Food, 10, 0), new ItemRow(9, Food, 1, 4) };
        var plan = StackLimits.Plan(rows, FoodTo20, canGuardFreshness: true);

        var line = Assert.Single(StackLimits.Describe(rows, plan, id => id == Food ? "Food" : "?"));

        Assert.Equal("Food: 3 items, game limit 1-10, 2 raised (1 newly stackable), 1 kept for having uses", line);
    }

    [Fact]
    public void Only_seeds_without_a_shelf_life_ignore_freshness_when_stacking()
    {
        var rows = new[]
        {
            new ItemRow(15001, Seed, 1, 0, Life: -1),
            new ItemRow(15002, Seed, 1, 0, Life: 0),
            new ItemRow(15003, Seed, 1, 0, Life: 72),
            new ItemRow(7, Food, 1, 1, Life: -1),
        };

        Assert.Equal(new[] { 15001, 15002 }, StackLimits.NeverSpoiling(rows).Order());
    }
}
