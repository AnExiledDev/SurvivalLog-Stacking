using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

public class ItemClicksTests
{
    /// <summary>The bug report: a Ctrl-click on a stack in the merchant's trade window opened the item info popup.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_ctrl_click_never_opens_the_item_details_whatever_else_is_held(bool isShiftHeld)
    {
        Assert.False(ItemClicks.OpensDetails(Splitting.KindFor(isShiftHeld, isCtrlHeld: true)));
    }

    [Fact]
    public void A_plain_click_still_opens_the_item_details()
    {
        Assert.True(ItemClicks.OpensDetails(Splitting.KindFor(isShiftHeld: false, isCtrlHeld: false)));
    }

    [Fact]
    public void A_shift_click_the_window_does_not_take_still_opens_the_item_details_as_in_the_base_game()
    {
        Assert.True(ItemClicks.OpensDetails(Splitting.KindFor(isShiftHeld: true, isCtrlHeld: false)));
    }

    /// <summary>The bug report: Shift with a right-click on a stack in the bag split nothing in the crafting views.</summary>
    [Theory]
    [InlineData(SplitKind.Half)]
    [InlineData(SplitKind.One)]
    [InlineData(SplitKind.Choose)]
    public void A_right_click_with_shift_or_ctrl_on_a_stack_in_the_players_bag_splits_it_in_a_station_view(SplitKind heldKeys)
    {
        Assert.True(ItemClicks.SplitsInView(new ViewClick(7, IsRightClick: true), heldKeys, isInPlayersBag: true));
    }

    [Fact]
    public void A_plain_right_click_in_a_station_view_does_what_the_game_does()
    {
        Assert.False(ItemClicks.SplitsInView(new ViewClick(7, IsRightClick: true), SplitKind.None, isInPlayersBag: true));
    }

    [Fact]
    public void A_shift_left_click_stays_the_crafting_tables_quick_move()
    {
        Assert.False(ItemClicks.SplitsInView(new ViewClick(7, IsRightClick: false), SplitKind.Half, isInPlayersBag: true));
    }

    [Fact]
    public void A_right_click_with_shift_on_what_the_station_or_a_storage_tab_holds_stays_the_games()
    {
        Assert.False(ItemClicks.SplitsInView(new ViewClick(7, IsRightClick: true), SplitKind.Half, isInPlayersBag: false));
    }

    [Fact]
    public void A_station_page_s_right_click_names_the_item_and_the_button()
    {
        Assert.Equal(new ViewClick(12, IsRightClick: true), ItemClicks.ParseViewClick("{\"itemId\":12,\"rightClick\":true}"));
    }

    [Fact]
    public void A_page_that_sends_the_item_id_as_text_still_names_the_item()
    {
        Assert.Equal(new ViewClick(12, IsRightClick: true), ItemClicks.ParseViewClick("{\"itemId\":\"12\",\"rightClick\":true}"));
    }

    [Theory]
    [InlineData("{\"itemId\":12,\"rightClick\":false}")]
    [InlineData("{\"itemId\":12}")]
    [InlineData("{\"itemId\":12,\"rightClick\":false,\"fromRack\":true}")]
    public void A_left_click_from_any_station_page_reads_as_a_left_click(string json)
    {
        Assert.Equal(new ViewClick(12, IsRightClick: false), ItemClicks.ParseViewClick(json));
    }

    [Fact]
    public void Extra_fields_a_page_adds_do_not_hide_the_click()
    {
        Assert.Equal(new ViewClick(12, IsRightClick: true), ItemClicks.ParseViewClick("{\"itemId\":12,\"rightClick\":true,\"grid\":\"bag\"}"));
    }

    [Fact]
    public void No_click_data_at_all_splits_nothing()
    {
        Assert.Null(ItemClicks.ParseViewClick(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[12]")]
    [InlineData("{\"sourceId\":12}")]
    [InlineData("{\"itemId\":0,\"rightClick\":true}")]
    [InlineData("{\"itemId\":-4,\"rightClick\":true}")]
    [InlineData("{\"itemId\":1.5,\"rightClick\":true}")]
    [InlineData("{\"itemId\":\"x\",\"rightClick\":true}")]
    [InlineData("{\"itemId\":null,\"rightClick\":true}")]
    public void A_click_that_names_no_item_splits_nothing(string json)
    {
        Assert.Null(ItemClicks.ParseViewClick(json));
    }
}
