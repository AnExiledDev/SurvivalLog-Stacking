using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

public class SplittingTests
{
    private const int Limit = 20;
    private const int NoWeightLimit = int.MaxValue;
    private const long Source = 1;

    /// <summary>A 6x4 bag; the dragged stack is a 1x1 item unless a test says otherwise.</summary>
    private static BagGrid Bag(params GridItem[] items) => new(6, 4, items);

    private static GridItem Stack(long id, int x, int y, int count = 5, StackMatch match = StackMatch.Same, int width = 1, int height = 1) =>
        new(id, new Footprint(x, y, width, height), count, match);

    private static StackDrag Drag(int count, int x, int y, Cell? cursor = null, int width = 1, int height = 1) =>
        new(Source, count, width, height, new Cell(x, y), cursor ?? new Cell(x, y));

    private static SplitPlan Plan(SplitKind kind, StackDrag drag, BagGrid? bag, int unitsWeightAllows = NoWeightLimit, int? chosen = null) =>
        Splitting.Plan(kind, drag, bag, Limit, unitsWeightAllows, chosen);

    [Fact]
    public void A_drag_without_a_modifier_is_left_to_the_game()
    {
        Assert.IsType<SplitPlan.Vanilla>(Plan(SplitKind.None, Drag(10, 3, 3), Bag()));
    }

    [Fact]
    public void A_single_item_has_nothing_to_split_so_it_moves_as_the_game_moves_it()
    {
        Assert.IsType<SplitPlan.Vanilla>(Plan(SplitKind.Half, Drag(1, 3, 3), Bag()));
    }

    [Fact]
    public void Dropping_outside_any_bag_is_left_to_the_game()
    {
        Assert.IsType<SplitPlan.Vanilla>(Plan(SplitKind.Half, Drag(10, 3, 3), bag: null));
    }

    [Fact]
    public void Shift_drag_to_an_empty_spot_starts_a_new_stack_with_half()
    {
        Assert.Equal(new SplitPlan.NewStack(new Cell(3, 3), 5), Plan(SplitKind.Half, Drag(10, 3, 3), Bag()));
    }

    [Fact]
    public void Half_of_an_odd_stack_leaves_the_larger_part_where_it_was()
    {
        Assert.Equal(new SplitPlan.NewStack(new Cell(3, 3), 3), Plan(SplitKind.Half, Drag(7, 3, 3), Bag()));
    }

    [Fact]
    public void Half_of_two_moves_one()
    {
        Assert.Equal(new SplitPlan.NewStack(new Cell(3, 3), 1), Plan(SplitKind.Half, Drag(2, 3, 3), Bag()));
    }

    [Fact]
    public void Ctrl_drag_takes_just_one_from_the_stack()
    {
        Assert.Equal(new SplitPlan.NewStack(new Cell(3, 3), 1), Plan(SplitKind.One, Drag(10, 3, 3), Bag()));
    }

    [Fact]
    public void Ctrl_shift_drag_asks_how_many_and_never_offers_to_move_the_whole_stack_to_a_new_spot()
    {
        Assert.Equal(new SplitPlan.Choose(9, 5), Plan(SplitKind.Choose, Drag(10, 3, 3), Bag()));
    }

    [Fact]
    public void The_amount_chosen_in_the_picker_is_what_moves()
    {
        Assert.Equal(new SplitPlan.NewStack(new Cell(3, 3), 4), Plan(SplitKind.Choose, Drag(10, 3, 3), Bag(), chosen: 4));
    }

    [Fact]
    public void A_chosen_amount_beyond_what_can_move_is_cut_to_what_can_because_the_bag_may_have_changed_while_the_picker_was_open()
    {
        Assert.Equal(new SplitPlan.NewStack(new Cell(3, 3), 9), Plan(SplitKind.Choose, Drag(10, 3, 3), Bag(), chosen: 50));
    }

    [Fact]
    public void When_only_one_unit_can_move_the_picker_is_skipped()
    {
        Assert.Equal(new SplitPlan.NewStack(new Cell(3, 3), 1), Plan(SplitKind.Choose, Drag(2, 3, 3), Bag()));
    }

    [Fact]
    public void Splitting_within_one_bag_cannot_put_the_new_stack_on_top_of_the_one_it_came_from()
    {
        var bag = Bag(Stack(Source, 2, 2, count: 10, width: 2));

        Assert.Equal(new SplitPlan.Refused(Refusal.NoRoom), Plan(SplitKind.Half, Drag(10, 3, 2, width: 2), bag));
    }

    [Fact]
    public void Splitting_within_one_bag_next_to_the_source_stack_works()
    {
        var bag = Bag(Stack(Source, 2, 2, count: 10));

        Assert.Equal(new SplitPlan.NewStack(new Cell(3, 2), 5), Plan(SplitKind.Half, Drag(10, 3, 2), bag));
    }

    [Fact]
    public void A_new_stack_that_would_hang_off_the_edge_of_the_bag_is_refused()
    {
        Assert.Equal(new SplitPlan.Refused(Refusal.NoRoom), Plan(SplitKind.Half, Drag(10, 5, 3, width: 2), Bag()));
    }

    [Fact]
    public void Dropping_onto_a_different_item_is_refused()
    {
        var bag = Bag(Stack(7, 3, 3, match: StackMatch.OtherItem));

        Assert.Equal(new SplitPlan.Refused(Refusal.NoRoom), Plan(SplitKind.Half, Drag(10, 3, 3), bag));
    }

    [Fact]
    public void Dropping_onto_a_matching_stack_tops_it_up()
    {
        var bag = Bag(Stack(7, 3, 3, count: 4));

        Assert.Equal(new SplitPlan.JoinStack(7, 5), Plan(SplitKind.Half, Drag(10, 3, 3), bag));
    }

    [Fact]
    public void Topping_up_stops_at_the_stack_limit()
    {
        var bag = Bag(Stack(7, 3, 3, count: Limit - 3));

        Assert.Equal(new SplitPlan.JoinStack(7, 3), Plan(SplitKind.Half, Drag(10, 3, 3), bag));
    }

    [Fact]
    public void A_full_stack_takes_nothing()
    {
        var bag = Bag(Stack(7, 3, 3, count: Limit));

        Assert.Equal(new SplitPlan.Refused(Refusal.StackFull), Plan(SplitKind.One, Drag(10, 3, 3), bag));
    }

    [Fact]
    public void A_stack_in_other_condition_takes_nothing_so_neither_loses_its_shelf_life_or_quality()
    {
        var bag = Bag(Stack(7, 3, 3, match: StackMatch.OtherCondition));

        Assert.Equal(new SplitPlan.Refused(Refusal.OtherCondition), Plan(SplitKind.One, Drag(10, 3, 3), bag));
    }

    [Fact]
    public void A_split_into_or_out_of_a_container_with_its_own_rules_is_refused_rather_than_bending_them()
    {
        var bench = new BagGrid(6, 4, [], HasOwnRules: true);

        Assert.Equal(new SplitPlan.Refused(Refusal.OwnRules), Plan(SplitKind.Half, Drag(10, 3, 3), bench));
    }

    [Fact]
    public void A_plain_drag_into_a_container_with_its_own_rules_is_still_the_game_s_to_handle()
    {
        var bench = new BagGrid(6, 4, [], HasOwnRules: true);

        Assert.IsType<SplitPlan.Vanilla>(Plan(SplitKind.None, Drag(10, 3, 3), bench));
    }

    /// <summary>The game's move into a station is cut to one unit by the station rule, so a split there needs no plan of its own.</summary>
    [Theory]
    [InlineData(SplitKind.Half)]
    [InlineData(SplitKind.One)]
    [InlineData(SplitKind.Choose)]
    public void Every_split_kind_into_a_station_is_left_to_the_move_that_takes_one_unit(SplitKind kind)
    {
        var station = new BagGrid(6, 4, [], HasOwnRules: true, IsStation: true, TakesOneUnit: true);

        Assert.IsType<SplitPlan.Vanilla>(Plan(kind, Drag(10, 3, 3), station));
    }

    /// <summary>Without the station rule a stack would go in whole, so a station with its own rules still refuses a split, as in 0.2.0.</summary>
    [Theory]
    [InlineData(SplitKind.Half)]
    [InlineData(SplitKind.One)]
    [InlineData(SplitKind.Choose)]
    public void A_split_into_a_station_that_does_not_take_one_unit_is_refused(SplitKind kind)
    {
        var station = new BagGrid(6, 4, [], HasOwnRules: true, IsStation: true);

        Assert.Equal(new SplitPlan.Refused(Refusal.OwnRules), Plan(kind, Drag(10, 3, 3), station));
    }

    /// <summary>A station is never split into, whether or not the game gives it rules of its own, as with a right-click split.</summary>
    [Theory]
    [InlineData(SplitKind.Half)]
    [InlineData(SplitKind.One)]
    [InlineData(SplitKind.Choose)]
    public void A_split_into_any_station_that_does_not_take_one_unit_is_refused(SplitKind kind)
    {
        var station = new BagGrid(6, 4, [], IsStation: true);

        Assert.Equal(new SplitPlan.Refused(Refusal.OwnRules), Plan(kind, Drag(10, 3, 3), station));
    }

    [Theory]
    [InlineData(false, false, SplitKind.None)]
    [InlineData(true, false, SplitKind.Half)]
    [InlineData(false, true, SplitKind.One)]
    [InlineData(true, true, SplitKind.Choose)]
    public void Shift_takes_half_ctrl_takes_one_and_both_ask_how_many(bool isShiftHeld, bool isCtrlHeld, SplitKind expected)
    {
        Assert.Equal(expected, Splitting.KindFor(isShiftHeld, isCtrlHeld));
    }

    [Theory]
    [MemberData(nameof(EveryRefusal))]
    public void Every_refusal_tells_the_player_why(Refusal reason)
    {
        Assert.False(string.IsNullOrWhiteSpace(Splitting.Explain(reason)));
    }

    public static TheoryData<Refusal> EveryRefusal() => new(Enum.GetValues<Refusal>());

    [Theory]
    [InlineData(Refusal.NoRoom, "No room to split the stack there")]
    [InlineData(Refusal.StackFull, "That stack is already full")]
    [InlineData(Refusal.TooHeavy, "That would be too heavy for the container")]
    [InlineData(Refusal.OtherCondition, "Those don't stack: freshness or condition differs")]
    [InlineData(Refusal.OwnRules, "Stacks can't be split into or out of this container; split them in your bag first")]
    [InlineData(Refusal.Unreadable, "Couldn't read this container's layout, so nothing split: see BepInEx/stacking.log")]
    public void Every_refusal_tells_the_player_the_reason_decided_for_it(Refusal reason, string words)
    {
        Assert.Equal(words, Splitting.Explain(reason));
    }

    [Fact]
    public void A_new_stack_that_landed_where_it_was_dropped_needs_no_word()
    {
        var landing = Splitting.LandingOf(5, new Cell(2, 1), 5, new Cell(2, 1));

        Assert.Equal(Landing.WhereDropped, landing);
        Assert.Null(Splitting.Explain(landing));
    }

    [Fact]
    public void A_new_stack_the_game_put_in_another_cell_tells_the_player_it_went_elsewhere_in_the_container()
    {
        var landing = Splitting.LandingOf(5, new Cell(4, 0), 5, new Cell(2, 1));

        Assert.Equal("That spot was taken, so the split-off stack went elsewhere in the container", Splitting.Explain(landing));
    }

    /// <summary>The game's add drops a stack it cannot place, which gives it another owner; its cell says nothing then.</summary>
    [Fact]
    public void A_new_stack_that_left_the_container_tells_the_player_it_was_dropped_on_the_ground()
    {
        var landing = Splitting.LandingOf(9, new Cell(2, 1), 5, new Cell(2, 1));

        Assert.Equal("No room in the container, so the split-off stack was dropped on the ground", Splitting.Explain(landing));
    }

    [Fact]
    public void The_picker_can_offer_every_unit_when_topping_up_because_nothing_is_left_behind_in_an_empty_cell()
    {
        var bag = Bag(Stack(7, 3, 3, count: 2));

        Assert.Equal(new SplitPlan.Choose(10, 5), Plan(SplitKind.Choose, Drag(10, 3, 3), bag));
    }

    [Fact]
    public void When_a_wide_drop_touches_two_stacks_the_one_under_the_cursor_is_topped_up()
    {
        var bag = Bag(Stack(7, 2, 1), Stack(8, 3, 1));

        Assert.Equal(new SplitPlan.JoinStack(8, 1), Plan(SplitKind.One, Drag(10, 2, 1, cursor: new Cell(3, 1), width: 2), bag));
    }

    [Fact]
    public void When_a_wide_drop_touches_two_stacks_and_the_cursor_is_over_neither_it_is_refused()
    {
        var bag = Bag(Stack(7, 2, 1), Stack(8, 3, 1));

        Assert.Equal(new SplitPlan.Refused(Refusal.NoRoom), Plan(SplitKind.One, Drag(10, 2, 1, cursor: new Cell(0, 0), width: 2), bag));
    }

    [Fact]
    public void Topping_up_the_stack_being_dragged_is_not_a_split()
    {
        var bag = Bag(Stack(Source, 3, 3, count: 10));

        Assert.Equal(new SplitPlan.Refused(Refusal.NoRoom), Plan(SplitKind.One, Drag(10, 3, 3), bag));
    }

    [Fact]
    public void A_new_stack_in_another_bag_holds_no_more_than_that_bag_can_carry()
    {
        Assert.Equal(new SplitPlan.NewStack(new Cell(3, 3), 2), Plan(SplitKind.Half, Drag(10, 3, 3), Bag(), unitsWeightAllows: 2));
    }

    [Fact]
    public void A_bag_that_cannot_carry_one_more_unit_refuses_the_split()
    {
        Assert.Equal(new SplitPlan.Refused(Refusal.TooHeavy), Plan(SplitKind.One, Drag(10, 3, 3), Bag(), unitsWeightAllows: 0));
    }

    [Fact]
    public void Items_side_by_side_do_not_block_each_other()
    {
        var bag = Bag(Stack(7, 2, 3, match: StackMatch.OtherItem), Stack(8, 4, 3, match: StackMatch.OtherItem), Stack(9, 3, 2, match: StackMatch.OtherItem));

        Assert.Equal(new SplitPlan.NewStack(new Cell(3, 3), 1), Plan(SplitKind.One, Drag(10, 3, 3), bag));
    }

    [Fact]
    public void Moving_units_within_one_bag_changes_no_weight()
    {
        Assert.Equal(int.MaxValue, Splitting.UnitsWeightAllows(isSameBag: true, checksWeight: true, maxWeight: 1f, currentWeight: 1f, unitWeight: 0.5f));
    }

    [Fact]
    public void A_bag_without_a_weight_limit_takes_any_number()
    {
        Assert.Equal(int.MaxValue, Splitting.UnitsWeightAllows(isSameBag: false, checksWeight: false, maxWeight: 0f, currentWeight: 9f, unitWeight: 0.5f));
    }

    [Fact]
    public void A_weightless_item_is_never_held_back_by_weight()
    {
        Assert.Equal(int.MaxValue, Splitting.UnitsWeightAllows(isSameBag: false, checksWeight: true, maxWeight: 1f, currentWeight: 1f, unitWeight: 0f));
    }

    [Fact]
    public void A_bag_takes_as_many_whole_units_as_its_spare_weight_holds()
    {
        Assert.Equal(2, Splitting.UnitsWeightAllows(isSameBag: false, checksWeight: true, maxWeight: 10f, currentWeight: 5f, unitWeight: 2f));
    }

    [Fact]
    public void A_unit_that_exactly_fills_the_bag_still_fits_despite_float_rounding()
    {
        Assert.Equal(3, Splitting.UnitsWeightAllows(isSameBag: false, checksWeight: true, maxWeight: 0.9f, currentWeight: 0.6f, unitWeight: 0.1f));
    }

    [Fact]
    public void An_overloaded_bag_takes_nothing()
    {
        Assert.Equal(0, Splitting.UnitsWeightAllows(isSameBag: false, checksWeight: true, maxWeight: 10f, currentWeight: 12f, unitWeight: 1f));
    }
}

public class StackMatchTests
{
    private const float Now = 120f;

    private static StackFacts Facts(int configId = 42, bool polluted = false, bool mapPreset = false, bool ownWeight = false, int startTime = 100) =>
        new(configId, polluted, mapPreset, ownWeight, new Freshness(startTime, 48, 0f, 1f, 1, 1));

    private static StackMatch Match(StackFacts other, bool isSameQuality = true, bool isNewlyStackable = false, bool neverSpoils = false, float? now = Now) =>
        Splitting.MatchOf(Facts(), other, isSameQuality, isNewlyStackable, neverSpoils, now);

    [Fact]
    public void Another_item_is_never_a_stack_to_top_up()
    {
        Assert.Equal(StackMatch.OtherItem, Match(Facts(configId: 43)));
    }

    [Fact]
    public void The_same_item_in_the_same_condition_tops_up()
    {
        Assert.Equal(StackMatch.Same, Match(Facts()));
    }

    [Fact]
    public void Polluted_and_clean_units_stay_apart_as_the_game_keeps_them()
    {
        Assert.Equal(StackMatch.OtherCondition, Match(Facts(polluted: true)));
    }

    [Fact]
    public void A_map_placed_unit_stays_apart_from_a_found_one_as_the_game_keeps_them()
    {
        Assert.Equal(StackMatch.OtherCondition, Match(Facts(mapPreset: true)));
    }

    [Fact]
    public void Units_of_different_quality_stay_apart_as_the_game_keeps_them()
    {
        Assert.Equal(StackMatch.OtherCondition, Match(Facts(), isSameQuality: false));
    }

    [Fact]
    public void A_stack_carrying_its_own_weight_never_takes_more_units()
    {
        Assert.Equal(StackMatch.OtherCondition, Match(Facts(ownWeight: true)));
    }

    [Fact]
    public void An_item_the_game_already_stacks_keeps_the_game_s_rule_that_ignores_freshness()
    {
        Assert.Equal(StackMatch.Same, Match(Facts(startTime: 110)));
    }

    [Fact]
    public void An_item_this_mod_made_stackable_tops_up_only_a_stack_of_the_same_freshness()
    {
        Assert.Equal(StackMatch.OtherCondition, Match(Facts(startTime: 110), isNewlyStackable: true));
    }

    [Fact]
    public void An_item_this_mod_made_stackable_tops_up_a_stack_from_the_same_batch()
    {
        Assert.Equal(StackMatch.Same, Match(Facts(), isNewlyStackable: true));
    }

    [Fact]
    public void Without_the_game_clock_freshness_cannot_be_compared_so_nothing_is_mixed()
    {
        Assert.Equal(StackMatch.OtherCondition, Match(Facts(), isNewlyStackable: true, now: null));
    }

    [Fact]
    public void A_seed_that_never_spoils_tops_up_a_stack_picked_up_at_another_time()
    {
        Assert.Equal(StackMatch.Same, Match(Facts(startTime: 10), isNewlyStackable: true, neverSpoils: true));
    }

    [Fact]
    public void A_seed_that_never_spoils_still_keeps_apart_from_a_polluted_stack()
    {
        Assert.Equal(StackMatch.OtherCondition, Match(Facts(polluted: true, startTime: 10), isNewlyStackable: true, neverSpoils: true));
    }
}

public class RightClickSplitTests
{
    private const long Clicked = 1;

    /// <summary>A 4x3 bag holding the clicked stack at its cell.</summary>
    private static BagGrid Bag(Cell at, int count, params GridItem[] others) =>
        new(4, 3, [new GridItem(Clicked, new Footprint(at.X, at.Y, 1, 1), count, StackMatch.Same), .. others]);

    private static GridItem Other(long id, int x, int y, int width = 1, int height = 1) =>
        new(id, new Footprint(x, y, width, height), 1, StackMatch.OtherItem);

    private static StackDrag Clicking(Cell at, int count) => new(Clicked, count, 1, 1, at, null);

    private static SplitPlan Plan(SplitKind kind, Cell at, int count, BagGrid bag, int? chosen = null) =>
        Splitting.PlanRightClick(kind, Clicking(at, count), bag, chosen);

    [Fact]
    public void A_plain_right_click_is_left_to_the_game()
    {
        var at = new Cell(1, 1);

        Assert.IsType<SplitPlan.Vanilla>(Plan(SplitKind.None, at, 10, Bag(at, 10)));
    }

    [Fact]
    public void A_single_item_has_nothing_to_split_off()
    {
        var at = new Cell(1, 1);

        Assert.IsType<SplitPlan.Vanilla>(Plan(SplitKind.Half, at, 1, Bag(at, 1)));
    }

    [Fact]
    public void Shift_right_click_puts_half_in_the_free_spot_next_to_the_stack()
    {
        var at = new Cell(1, 1);

        Assert.Equal(new SplitPlan.NewStack(new Cell(1, 0), 5), Plan(SplitKind.Half, at, 10, Bag(at, 10)));
    }

    [Fact]
    public void Ctrl_right_click_splits_off_one()
    {
        var at = new Cell(1, 1);

        Assert.Equal(new SplitPlan.NewStack(new Cell(1, 0), 1), Plan(SplitKind.One, at, 10, Bag(at, 10)));
    }

    [Fact]
    public void Ctrl_shift_right_click_asks_how_many_and_always_leaves_one_behind()
    {
        var at = new Cell(1, 1);

        Assert.Equal(new SplitPlan.Choose(9, 5), Plan(SplitKind.Choose, at, 10, Bag(at, 10)));
    }

    [Fact]
    public void The_amount_chosen_after_a_right_click_is_what_splits_off()
    {
        var at = new Cell(1, 1);

        Assert.Equal(new SplitPlan.NewStack(new Cell(1, 0), 3), Plan(SplitKind.Choose, at, 10, Bag(at, 10), chosen: 3));
    }

    [Fact]
    public void A_full_container_moves_nothing()
    {
        var at = new Cell(0, 0);
        var full = Bag(at, 10, Other(2, 1, 0, width: 3), Other(3, 0, 1, width: 4, height: 2));

        Assert.Equal(new SplitPlan.Refused(Refusal.NoRoom), Plan(SplitKind.Half, at, 10, full));
    }

    [Fact]
    public void A_container_with_its_own_rules_is_never_split_in()
    {
        var at = new Cell(1, 1);
        var compost = Bag(at, 10) with { HasOwnRules = true };

        Assert.Equal(new SplitPlan.Refused(Refusal.OwnRules), Plan(SplitKind.Half, at, 10, compost));
    }

    /// <summary>A split in a station would leave two slots holding stacks; what a station holds is never split, whatever was asked.</summary>
    [Theory]
    [InlineData(SplitKind.Half)]
    [InlineData(SplitKind.One)]
    [InlineData(SplitKind.Choose)]
    public void What_a_station_holds_is_never_split(SplitKind kind)
    {
        var at = new Cell(1, 1);
        var hotPot = Bag(at, 10) with { HasOwnRules = true, IsStation = true };
        var crafting = Bag(at, 10) with { IsStation = true };

        Assert.Equal(new SplitPlan.Refused(Refusal.OwnRules), Plan(kind, at, 10, hotPot));
        Assert.Equal(new SplitPlan.Refused(Refusal.OwnRules), Plan(kind, at, 10, crafting));
    }

    [Fact]
    public void A_right_click_split_of_food_never_lands_on_mold()
    {
        var at = new Cell(1, 1);
        var moldy = Bag(at, 10) with { Blocked = [new Cell(1, 0), new Cell(0, 1)] };

        Assert.Equal(new SplitPlan.NewStack(new Cell(2, 1), 5), Plan(SplitKind.Half, at, 10, moldy));
    }
}

public class NearestFreeCellTests
{
    private static GridItem Item(long id, int x, int y, int width = 1, int height = 1) =>
        new(id, new Footprint(x, y, width, height), 1, StackMatch.OtherItem);

    [Fact]
    public void The_closest_free_cell_wins_over_one_further_along_the_row()
    {
        var bag = new BagGrid(5, 3, [Item(1, 0, 0), Item(2, 1, 0), Item(3, 2, 0), Item(4, 3, 0)]);

        Assert.Equal(new Cell(2, 1), Splitting.NearestFreeCell(bag, 1, 1, new Cell(2, 0)));
    }

    [Fact]
    public void Between_equally_close_cells_the_higher_row_wins()
    {
        var bag = new BagGrid(3, 3, [Item(1, 1, 1)]);

        Assert.Equal(new Cell(1, 0), Splitting.NearestFreeCell(bag, 1, 1, new Cell(1, 1)));
    }

    [Fact]
    public void Between_equally_close_cells_in_one_row_the_left_one_wins()
    {
        var bag = new BagGrid(3, 3, [Item(1, 1, 1), Item(2, 1, 0)]);

        Assert.Equal(new Cell(0, 1), Splitting.NearestFreeCell(bag, 1, 1, new Cell(1, 1)));
    }

    [Fact]
    public void A_spot_too_small_for_the_item_is_skipped()
    {
        var bag = new BagGrid(4, 2, [Item(1, 0, 0), Item(2, 2, 0), Item(3, 0, 1, width: 2)]);

        Assert.Equal(new Cell(2, 1), Splitting.NearestFreeCell(bag, 2, 1, new Cell(0, 0)));
    }

    [Fact]
    public void A_spot_that_would_hang_off_the_edge_is_never_chosen()
    {
        var bag = new BagGrid(3, 1, [Item(1, 0, 0)]);

        Assert.Equal(new Cell(1, 0), Splitting.NearestFreeCell(bag, 2, 1, new Cell(2, 0)));
    }

    [Fact]
    public void A_full_bag_has_no_free_cell()
    {
        var bag = new BagGrid(2, 1, [Item(1, 0, 0), Item(2, 1, 0)]);

        Assert.Null(Splitting.NearestFreeCell(bag, 1, 1, new Cell(0, 0)));
    }

    [Fact]
    public void A_moldy_cell_is_taken_like_an_occupied_one()
    {
        var bag = new BagGrid(3, 1, [], Blocked: [new Cell(1, 0)]);

        Assert.Equal(new Cell(0, 0), Splitting.NearestFreeCell(bag, 1, 1, new Cell(1, 0)));
    }

    [Fact]
    public void An_item_wider_than_one_cell_never_covers_mold_with_any_part_of_it()
    {
        var bag = new BagGrid(4, 1, [], Blocked: [new Cell(1, 0)]);

        Assert.Equal(new Cell(2, 0), Splitting.NearestFreeCell(bag, 2, 1, new Cell(0, 0)));
    }

    [Fact]
    public void A_bag_whose_free_cells_are_all_moldy_has_no_free_cell()
    {
        var bag = new BagGrid(2, 1, [Item(1, 0, 0)], Blocked: [new Cell(1, 0)]);

        Assert.Null(Splitting.NearestFreeCell(bag, 1, 1, new Cell(0, 0)));
    }
}
