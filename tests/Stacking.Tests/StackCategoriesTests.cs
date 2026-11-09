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
}
