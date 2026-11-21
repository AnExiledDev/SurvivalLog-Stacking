using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

public class StackCategoriesTests
{
    private const int Material = 9;
    private const int Renovation = 21;

    private static StackCategory Category(int id) => Assert.Single(StackCategories.All, category => category.Id == id);

    [Fact]
    public void Every_item_category_of_the_game_has_its_own_config_entry()
    {
        Assert.Equal(Enumerable.Range(1, 22), StackCategories.All.Select(category => category.Id).Order());
    }

    /// <summary>Furniture, story items, robot modules, tools, books, records and other one-offs get no suggested raise.</summary>
    [Theory]
    [InlineData(3)] // Books
    [InlineData(4)] // ElectricalAppliances
    [InlineData(6)] // Tool
    [InlineData(7)] // Pet
    [InlineData(8)] // Pack
    [InlineData(12)] // Trap
    [InlineData(14)] // BagFurniture
    [InlineData(17)] // RobotModule
    [InlineData(18)] // StoryItem
    [InlineData(19)] // Collectible
    [InlineData(20)] // Record
    [InlineData(22)] // TaskItem
    public void Categories_of_one_off_items_get_no_suggested_raise(int id)
    {
        Assert.Equal(0, Category(id).Suggested);
    }

    [Fact]
    public void Materials_get_a_suggestion_above_the_games_limit_because_players_hoard_them()
    {
        var material = Category(Material);

        Assert.True(material.Game is { } game && material.Suggested > game.Highest);
    }

    [Theory]
    [InlineData(1)] // Food
    [InlineData(2)] // Drug
    [InlineData(5)] // DailyNecessities
    [InlineData(Material)]
    [InlineData(10)] // Seed
    [InlineData(11)] // Fertilizer
    [InlineData(13)] // Fuel
    [InlineData(15)] // Flower
    [InlineData(16)] // Throwable
    [InlineData(Renovation)]
    public void Everyday_consumables_get_a_suggestion_above_the_games_limit(int id)
    {
        var category = Category(id);

        Assert.True(category.Game is { } game && category.Suggested > game.Highest);
    }

    /// <summary>A suggestion at or below the game's limit would look like a raise in the config and change nothing.</summary>
    [Fact]
    public void Every_suggested_raise_is_above_the_games_own_limit()
    {
        var raised = StackCategories.All.Where(category => category.Suggested > 0);

        Assert.All(raised, category => Assert.True(category.Game is not { } game || category.Suggested > game.Highest));
    }

    [Fact]
    public void Every_suggestion_is_a_value_the_config_accepts()
    {
        Assert.All(StackCategories.All, category => Assert.Equal(StackLimits.Clamp(category.Suggested), category.Suggested));
    }

    /// <summary>Installing the mod changes no stack: every category's default keeps the game's limit.</summary>
    [Fact]
    public void The_default_limit_raises_no_item_above_the_games_own()
    {
        var rows = StackCategories.All.Select(category => new ItemRow(category.Id, category.Id, StackLimit: 1, Uses: 1)).ToArray();
        var wanted = StackCategories.All.ToDictionary(category => category.Id, _ => StackLimits.KeepGame);

        Assert.Empty(StackLimits.Plan(rows, wanted, canGuardFreshness: true).Limits);
    }

    [Fact]
    public void The_config_shows_the_games_limit_and_the_default_in_english_and_chinese()
    {
        var lines = StackCategories.Describe(Category(Renovation)).Split('\n');

        Assert.Equal(2, lines.Length);
        Assert.Contains("Game limit 1 to 10, default 0, suggested 30, highest 999.", lines[0]);
        Assert.Contains("游戏上限 1 至 10，默认 0，建议 30，最高 999。", lines[1]);
    }

    /// <summary>The limits live in the config itself, not in a separate file.</summary>
    [Fact]
    public void No_category_description_sends_the_player_to_another_file()
    {
        Assert.All(StackCategories.All, category => Assert.DoesNotContain(".log", StackCategories.Describe(category)));
    }

    [Fact]
    public void A_category_the_game_adds_later_gets_an_entry_that_keeps_the_games_limit()
    {
        var added = StackCategories.For(99, "Gadget");

        Assert.Equal(0, added.Suggested);
        Assert.StartsWith("Stack size for Gadget items.", StackCategories.Describe(added));
    }

    private const int Food = 1;
    private const int Tool = 6;

    private static readonly Dictionary<int, int> NothingSet = StackCategories.All.ToDictionary(category => category.Id, _ => StackLimits.KeepGame);

    /// <summary>Global is for the everyday items players hoard; one-off items stay one per slot unless set by name.</summary>
    [Fact]
    public void Global_raises_every_everyday_category_and_no_one_off_category()
    {
        var wanted = StackCategories.Wanted(NothingSet, 50);

        Assert.Equal(new[] { 1, 2, 5, 9, 10, 11, 13, 15, 16, 21 }, wanted.Keys.Order());
        Assert.All(wanted.Values, limit => Assert.Equal(50, limit));
    }

    [Theory]
    [InlineData(20)]
    [InlineData(500)]
    public void A_categorys_own_number_wins_over_global_whether_lower_or_higher(int food)
    {
        var set = new Dictionary<int, int>(NothingSet) { [Food] = food };

        Assert.Equal(food, StackCategories.Wanted(set, 99)[Food]);
    }

    [Fact]
    public void A_one_off_category_set_by_name_still_rises_next_to_global()
    {
        var set = new Dictionary<int, int>(NothingSet) { [Tool] = 5 };
        var wanted = StackCategories.Wanted(set, 99);

        Assert.Equal(5, wanted[Tool]);
        Assert.Equal(99, wanted[Material]);
    }

    /// <summary>An update adds Global at 0, so a player's limits stay exactly as they were.</summary>
    [Fact]
    public void Global_at_0_leaves_only_the_categories_set_by_name()
    {
        var set = new Dictionary<int, int>(NothingSet) { [Material] = 99 };

        Assert.Equal(new Dictionary<int, int> { [Material] = 99 }, StackCategories.Wanted(set, StackLimits.KeepGame));
    }

    [Theory]
    [InlineData(5000, 999)]
    [InlineData(-3, 0)]
    public void Global_is_held_to_the_range_the_config_accepts(int global, int expected)
    {
        var wanted = StackCategories.Wanted(NothingSet, global);

        Assert.Equal(expected, wanted.GetValueOrDefault(Food));
    }

    /// <summary>A player reading Food's setting has to know 0 there now means Global, while a tool's 0 still means the game's limit.</summary>
    [Fact]
    public void Only_categories_global_raises_say_their_0_uses_global()
    {
        Assert.Contains("0 uses the Global setting", StackCategories.Describe(Category(Food)));
        Assert.DoesNotContain("Global", StackCategories.Describe(Category(Tool)));
        Assert.Contains("0 keeps the game's limit.", StackCategories.Describe(Category(Tool)));
    }

    [Fact]
    public void The_global_description_is_english_then_chinese_and_names_no_other_file()
    {
        var lines = StackCategories.GlobalDescription.Split('\n');

        Assert.Equal(2, lines.Length);
        Assert.DoesNotContain(".log", StackCategories.GlobalDescription);
    }
}
