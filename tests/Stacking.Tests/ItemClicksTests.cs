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
}
