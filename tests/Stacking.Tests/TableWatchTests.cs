using System.Linq;
using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

public class TableWatchTests
{
    private static readonly TableStamp Loaded = new(Items: 1000, ItemCount: 900, Recipes: 2000, RecipeCount: 120);

    [Fact]
    public void Nothing_written_yet_means_a_full_write()
    {
        Assert.False(TableWatch.IsUnchanged(null, Loaded, []));
    }

    [Fact]
    public void The_same_tables_with_every_watched_limit_in_place_skip_the_write()
    {
        Assert.True(TableWatch.IsUnchanged(Loaded, Loaded, [new Canary(99, 99), new Canary(50, 50)]));
    }

    [Fact]
    public void A_replaced_item_table_is_written_again()
    {
        Assert.False(TableWatch.IsUnchanged(Loaded, Loaded with { Items = 3000 }, [new Canary(99, 99)]));
    }

    [Fact]
    public void A_replaced_recipe_table_is_written_again_so_new_dishes_get_their_limit()
    {
        Assert.False(TableWatch.IsUnchanged(Loaded, Loaded with { Recipes = 4000 }, []));
    }

    [Fact]
    public void Items_added_to_the_table_are_written()
    {
        Assert.False(TableWatch.IsUnchanged(Loaded, Loaded with { ItemCount = 901 }, []));
    }

    [Fact]
    public void A_watched_item_back_at_the_game_limit_is_written_again()
    {
        Assert.False(TableWatch.IsUnchanged(Loaded, Loaded, [new Canary(99, 99), new Canary(99, 10)]));
    }

    [Fact]
    public void Few_raised_items_are_all_watched()
    {
        Assert.Equal([3, 5, 9], TableWatch.PickCanaries([9, 3, 5]));
    }

    [Fact]
    public void Many_raised_items_are_watched_from_first_to_last()
    {
        var picked = TableWatch.PickCanaries(Enumerable.Range(1, 100));

        Assert.Equal(TableWatch.CanaryCount, picked.Count);
        Assert.Equal(1, picked[0]);
        Assert.Equal(100, picked[^1]);
        Assert.Equal(picked.Count, picked.Distinct().Count());
    }
}
