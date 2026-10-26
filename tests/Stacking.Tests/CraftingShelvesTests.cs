using System.Linq;
using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

/// <summary>
/// The crafting table's recipe list asks, for every recipe, whether the bench and its storages fill it once stacks
/// give single units. The shelves are read once per list rebuild and answer every recipe the way the fill's plan
/// would, so the list shows exactly the recipes it showed before, without reading the game once per recipe.
/// </summary>
public class CraftingShelvesTests
{
    private const long Bench = 1;
    private const long Bag = 2;
    private const long Shelf = 3;
    private const int Glass = 501;
    private const int Metal = 502;
    private const int Wood = 503;

    private static readonly nint Table = 100;
    private static readonly nint Items = 200;

    private sealed record Held(long Owner, int ConfigId, int Count, bool IsCut);

    private static List<KeyValuePair<int, int>> Needs(params (int ConfigId, int Units)[] needs) =>
        needs.Select(need => new KeyValuePair<int, int>(need.ConfigId, need.Units)).ToList();

    /// <summary>Read as CraftingFill reads the game: containers in fill order, each item in the cache's own order.</summary>
    private static CraftingShelves Shelve(IEnumerable<Held> held)
    {
        var shelves = new CraftingShelves();

        shelves.Start(Table, Items);

        foreach (var owner in held.GroupBy(item => item.Owner))
        {
            foreach (var item in owner) shelves.Add(item.ConfigId, item.Count, item.IsCut);

            shelves.EndOwner();
        }

        return shelves;
    }

    /// <summary>What the list said before the shelves: the fill's plan over the candidates, largest first in each container.</summary>
    private static bool PlanSays(IReadOnlyList<Held> held, IReadOnlyList<KeyValuePair<int, int>> needs)
    {
        var wanted = needs.ToDictionary(need => need.Key, need => need.Value);
        var candidates = held
            .Select((item, index) => (Item: item, Index: index))
            .GroupBy(entry => entry.Item.Owner)
            .SelectMany(owner => owner.Where(entry => wanted.ContainsKey(entry.Item.ConfigId)).OrderByDescending(entry => entry.Item.Count))
            .Select(entry => new PickCandidate(entry.Index, entry.Item.Owner, entry.Item.ConfigId, entry.Item.Count, entry.Item.IsCut))
            .ToList();

        return candidates.Any(candidate => candidate.IsCut) && CraftingPick.Plan(wanted, candidates) is PickPlan.Filled;
    }

    [Fact]
    public void Two_glass_and_one_metal_off_stacks_in_the_bag_fill_the_recipe()
    {
        var shelves = Shelve(new[] { new Held(Bag, Glass, 8, true), new Held(Bag, Metal, 4, true) });

        Assert.True(shelves.CanFill(Needs((Glass, 2), (Metal, 1))));
    }

    /// <summary>The game's own check already says yes then; this check only turns a no into a yes the fill makes good.</summary>
    [Fact]
    public void A_recipe_whole_items_fill_on_their_own_is_left_to_the_game()
    {
        var shelves = Shelve(new[] { new Held(Bag, Glass, 1, false), new Held(Bag, Glass, 1, false), new Held(Bag, Metal, 6, true) });

        Assert.False(shelves.CanFill(Needs((Glass, 2))));
    }

    [Fact]
    public void Stacks_holding_fewer_units_than_needed_leave_the_recipe_short()
    {
        var shelves = Shelve(new[] { new Held(Bag, Glass, 2, true), new Held(Shelf, Glass, 2, true) });

        Assert.True(shelves.CanFill(Needs((Glass, 4))));
        Assert.False(shelves.CanFill(Needs((Glass, 5))));
    }

    [Fact]
    public void An_item_nothing_holds_leaves_the_recipe_short()
    {
        var shelves = Shelve(new[] { new Held(Bag, Glass, 8, true) });

        Assert.False(shelves.CanFill(Needs((Glass, 2), (Wood, 1))));
    }

    /// <summary>
    /// The game fills with the largest whole item that fits first, so a container holding whole items of 1 and 3 puts
    /// in the 3 for a need of 3. Read in the cache's order (1 first), the 3 would no longer fit and the single cut unit
    /// would not cover the 2 left, so the recipe would wrongly read as short.
    /// </summary>
    [Fact]
    public void Whole_items_in_a_container_are_taken_largest_first_as_the_game_takes_them()
    {
        var held = new[] { new Held(Bag, Glass, 1, false), new Held(Bag, Glass, 3, false), new Held(Shelf, Glass, 1, true) };
        var shelves = Shelve(held);

        Assert.True(PlanSays(held, Needs((Glass, 3))));
        Assert.True(shelves.CanFill(Needs((Glass, 3))));
    }

    [Fact]
    public void Shelves_with_no_cut_stack_say_so_and_fill_nothing()
    {
        var shelves = Shelve(new[] { new Held(Bag, Glass, 1, false), new Held(Bag, Metal, 1, false) });

        Assert.False(shelves.HasAnyCut);
        Assert.Empty(shelves.CutConfigs);
        Assert.False(shelves.CanFill(Needs((Glass, 1))));
    }

    [Fact]
    public void Each_item_with_a_cut_stack_is_named_once()
    {
        var shelves = Shelve(new[] { new Held(Bag, Glass, 5, true), new Held(Shelf, Glass, 4, true), new Held(Bag, Metal, 1, false) });

        Assert.True(shelves.HasAnyCut);
        Assert.Equal(new[] { Glass }, shelves.CutConfigs);
        Assert.True(shelves.IsCut(Glass));
        Assert.False(shelves.IsCut(Metal));
    }

    /// <summary>The shelves are reused from rebuild to rebuild; a second read must hold only what the containers hold now.</summary>
    [Fact]
    public void A_new_read_forgets_what_the_last_one_held()
    {
        var shelves = Shelve(new[] { new Held(Bag, Glass, 8, true) });

        shelves.Start(Table, Items);
        shelves.Add(Metal, 1, isCut: false);
        shelves.EndOwner();

        Assert.False(shelves.HasAnyCut);
        Assert.False(shelves.IsCut(Glass));
        Assert.False(shelves.CanFill(Needs((Glass, 2))));
    }

    [Fact]
    public void Reading_the_same_stacks_again_does_not_count_their_units_twice()
    {
        var shelves = Shelve(new[] { new Held(Bag, Glass, 3, true) });

        shelves.Start(Table, Items);
        shelves.Add(Glass, 3, isCut: true);
        shelves.EndOwner();

        Assert.True(shelves.CanFill(Needs((Glass, 3))));
        Assert.False(shelves.CanFill(Needs((Glass, 4))));
    }

    [Fact]
    public void Every_recipe_in_one_rebuild_is_answered_from_one_read()
    {
        var shelves = Shelve(new[] { new Held(Bag, Glass, 8, true) });

        Assert.True(shelves.IsBuiltFor(Table, Items));
        Assert.Equal(1, shelves.Builds);
    }

    /// <summary>A loaded save brings a new item state, so the storages are read again even mid-rebuild.</summary>
    [Fact]
    public void A_loaded_save_s_items_are_read_again()
    {
        var shelves = Shelve(new[] { new Held(Bag, Glass, 8, true) });

        Assert.False(shelves.IsBuiltFor(Table, Items + 1));
        Assert.False(shelves.IsBuiltFor(Table + 1, Items));
    }

    /// <summary>Items move between rebuilds, so a rebuild that ends or starts drops the read.</summary>
    [Fact]
    public void A_rebuild_ending_drops_the_read()
    {
        var shelves = Shelve(new[] { new Held(Bag, Glass, 8, true) });

        shelves.Forget();

        Assert.False(shelves.IsBuiltFor(Table, Items));
    }

    [Fact]
    public void Nothing_read_yet_is_never_taken_as_read()
    {
        Assert.False(new CraftingShelves().IsBuiltFor(0, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void An_item_with_no_count_counts_as_one_unit(int count)
    {
        var shelves = Shelve(new[] { new Held(Bag, Glass, count, true), new Held(Bag, Glass, count, true) });

        Assert.True(shelves.CanFill(Needs((Glass, 2))));
        Assert.False(shelves.CanFill(Needs((Glass, 3))));
    }

    /// <summary>Random benches and storages, each answered both ways: the shelves must say what the fill's plan says.</summary>
    [Fact]
    public void Every_answer_matches_the_fill_s_plan()
    {
        var random = new Random(7781);
        var configs = new[] { Glass, Metal, Wood };
        var owners = new[] { Bench, Bag, Shelf };

        for (var round = 0; round < 3000; round++)
        {
            var held = Enumerable.Range(0, random.Next(0, 9))
                .Select(_ => new Held(owners[random.Next(owners.Length)], configs[random.Next(configs.Length)], random.Next(-1, 7), random.Next(3) == 0))
                .OrderBy(item => Array.IndexOf(owners, item.Owner))
                .ToList();
            var needs = configs.Where(_ => random.Next(2) == 0).Select(configId => new KeyValuePair<int, int>(configId, random.Next(0, 9))).ToList();
            var shelves = Shelve(held);

            Assert.True(PlanSays(held, needs) == shelves.CanFill(needs), $"round {round}: {string.Join(", ", held)} needing {string.Join(", ", needs)}");
        }
    }
}
