using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

public class TradeOffersTests
{
    private static readonly PendingOffer Split = new(StackId: 41, SourceId: 40, new OfferTarget(new Cell(2, 1)), SplitAt: 10f);
    private static readonly PendingOffer Sent = Split with { SentAt = 10.2f };
    private static readonly PendingTakeBack TakenBack = new(StackId: 42, OfferedId: 40, OfferedAt: new Cell(3, 2), SplitAt: 10f);

    /// <summary>The bug report: Shift, Ctrl and Ctrl+Shift did nothing on the trade screen.</summary>
    [Theory]
    [InlineData(SplitKind.Half)]
    [InlineData(SplitKind.One)]
    [InlineData(SplitKind.Choose)]
    public void A_drag_from_the_bag_to_the_drone_with_shift_or_ctrl_offers_part_of_the_stack(SplitKind heldKeys)
    {
        Assert.Equal(TradeDrag.Offer, TradeOffers.DragOf("bag", "drone", heldKeys));
    }

    [Theory]
    [InlineData(SplitKind.Half)]
    [InlineData(SplitKind.One)]
    [InlineData(SplitKind.Choose)]
    public void A_right_click_on_the_bag_with_shift_or_ctrl_offers_part_of_the_stack(SplitKind heldKeys)
    {
        Assert.Equal(TradeClick.Split, TradeOffers.ClickOf("bag", isRightClick: true, heldKeys, isHighlightHeld: false));
    }

    [Theory]
    [InlineData(SplitKind.None)]
    [InlineData(SplitKind.Join)]
    public void A_plain_drag_is_the_game_s_own_wherever_it_goes(SplitKind heldKeys)
    {
        Assert.Equal(TradeDrag.Game, TradeOffers.DragOf("bag", "drone", heldKeys));
        Assert.Equal(TradeDrag.Game, TradeOffers.DragOf("drone", "bag", heldKeys));
        Assert.Equal(TradeDrag.Game, TradeOffers.DragOf("drone", "drone", heldKeys));
        Assert.Equal(TradeDrag.Game, TradeOffers.DragOf("bag", "bag", heldKeys));
    }

    /// <summary>The bug report: a click on a Material Rack stack offered all of it; a transfer click offers one unit, as at a station.</summary>
    [Fact]
    public void A_plain_right_click_on_the_bag_offers_one_unit()
    {
        Assert.Equal(TradeClick.OfferOne, TradeOffers.ClickOf("bag", isRightClick: true, SplitKind.None, isHighlightHeld: false));
    }

    /// <summary>The window's hint: Shift with a left click is a transfer, the same as a right-click.</summary>
    [Theory]
    [InlineData(SplitKind.Half)]
    [InlineData(SplitKind.Choose)]
    public void A_shift_left_click_on_the_bag_offers_one_unit(SplitKind heldKeys)
    {
        Assert.Equal(TradeClick.OfferOne, TradeOffers.ClickOf("bag", isRightClick: false, heldKeys, isHighlightHeld: false));
    }

    [Theory]
    [InlineData(SplitKind.None)]
    [InlineData(SplitKind.One)]
    public void A_left_click_without_shift_shows_the_item_as_the_game_does(SplitKind heldKeys)
    {
        Assert.Equal(TradeClick.Game, TradeOffers.ClickOf("bag", isRightClick: false, heldKeys, isHighlightHeld: false));
    }

    /// <summary>The highlight key's click offers every stack of the kind; that is the game's and stays whole.</summary>
    [Theory]
    [InlineData(true, SplitKind.None)]
    [InlineData(true, SplitKind.Half)]
    [InlineData(false, SplitKind.None)]
    public void A_click_with_the_highlight_key_held_is_the_game_s_own(bool isRightClick, SplitKind heldKeys)
    {
        Assert.Equal(TradeClick.Game, TradeOffers.ClickOf("bag", isRightClick, heldKeys, isHighlightHeld: true));
    }

    /// <summary>The bug report: a Shift or Ctrl drag inside the drone did not split.</summary>
    [Theory]
    [InlineData(SplitKind.Half)]
    [InlineData(SplitKind.One)]
    [InlineData(SplitKind.Choose)]
    public void A_drag_with_shift_or_ctrl_from_one_drone_cell_to_another_splits_the_offer(SplitKind heldKeys)
    {
        Assert.Equal(TradeDrag.SplitOffer, TradeOffers.DragOf("drone", "drone", heldKeys));
    }

    /// <summary>A split works the same in every grid: inside the bag too.</summary>
    [Theory]
    [InlineData(SplitKind.Half)]
    [InlineData(SplitKind.One)]
    [InlineData(SplitKind.Choose)]
    public void A_drag_with_shift_or_ctrl_inside_the_bag_splits_the_stack(SplitKind heldKeys)
    {
        Assert.Equal(TradeDrag.InBag, TradeOffers.DragOf("bag", "bag", heldKeys));
    }

    /// <summary>The bug report: splitting what is on the drone back into the bag moved the whole stack.</summary>
    [Theory]
    [InlineData(SplitKind.Half)]
    [InlineData(SplitKind.One)]
    [InlineData(SplitKind.Choose)]
    public void A_drag_from_the_drone_to_the_bag_with_shift_or_ctrl_takes_part_of_the_offer_back(SplitKind heldKeys)
    {
        Assert.Equal(TradeDrag.TakeBack, TradeOffers.DragOf("drone", "bag", heldKeys));
        Assert.True(TradeOffers.TakesBack("drone"));
    }

    [Theory]
    [InlineData(SplitKind.Half)]
    [InlineData(SplitKind.One)]
    [InlineData(SplitKind.Choose)]
    public void A_right_click_on_the_drone_with_shift_or_ctrl_takes_part_of_the_offer_back(SplitKind heldKeys)
    {
        Assert.Equal(TradeClick.Split, TradeOffers.ClickOf("drone", isRightClick: true, heldKeys, isHighlightHeld: false));
    }

    [Fact]
    public void A_plain_right_click_or_shift_click_on_the_drone_takes_the_whole_offer_back_as_the_game_does()
    {
        Assert.Equal(TradeClick.Game, TradeOffers.ClickOf("drone", isRightClick: true, SplitKind.None, isHighlightHeld: false));
        Assert.Equal(TradeClick.Game, TradeOffers.ClickOf("drone", isRightClick: false, SplitKind.Half, isHighlightHeld: false));
    }

    [Theory]
    [InlineData("ground", "bag")]
    [InlineData(null, "drone")]
    public void A_drag_from_outside_the_bag_and_the_drone_is_the_game_s_own(string? fromGrid, string toGrid)
    {
        Assert.Equal(TradeDrag.Game, TradeOffers.DragOf(fromGrid, toGrid, SplitKind.Half));
    }

    [Fact]
    public void A_split_from_the_bag_offers_rather_than_takes_back()
    {
        Assert.False(TradeOffers.TakesBack("bag"));
    }

    /// <summary>A transfer click tops up a stack already on offer, so it needs every offered stack and its cell.</summary>
    [Fact]
    public void Every_offered_stack_is_read_with_its_drone_cell()
    {
        Assert.Equal(new[] { new OfferedStack(7, new Cell(0, 0)), new OfferedStack(41, new Cell(3, 2)) },
            TradeOffers.Offered("[{\"id\":7,\"x\":0,\"y\":0,\"o\":5},{\"id\":\"41\",\"x\":3,\"y\":2,\"o\":5}]"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("{\"id\":41}")]
    public void An_unreadable_offer_list_offers_nothing(string? offeredJson)
    {
        Assert.Empty(TradeOffers.Offered(offeredJson));
    }

    [Fact]
    public void The_smaller_offer_is_put_down_again_once_the_bag_shows_the_units_taken_back()
    {
        Assert.Equal(OfferStep.Send, TradeOffers.Next(TakenBack, now: 10.1f, isInTradeBag: true));
    }

    [Fact]
    public void The_offer_is_left_alone_while_the_bag_has_not_caught_up()
    {
        Assert.Equal(OfferStep.Wait, TradeOffers.Next(TakenBack, now: 10.5f, isInTradeBag: false));
    }

    [Fact]
    public void Units_taken_back_the_bag_never_shows_are_given_up_on()
    {
        Assert.Equal(OfferStep.GiveUp, TradeOffers.Next(TakenBack, now: 10f + TradeOffers.ShowWaitSeconds + 0.1f, isInTradeBag: false));
    }

    /// <summary>The smaller offer goes back down on the drone cell it already holds.</summary>
    [Fact]
    public void An_offered_stack_is_found_where_it_lies_on_the_drone()
    {
        Assert.Equal(new Cell(3, 2), TradeOffers.OfferedAt("[{\"id\":7,\"x\":0,\"y\":0,\"o\":5},{\"id\":41,\"x\":3,\"y\":2,\"o\":5}]", 41));
    }

    [Fact]
    public void The_split_off_stack_is_offered_once_the_bag_shows_it()
    {
        Assert.Equal(OfferStep.Send, TradeOffers.Next(Split, now: 10.1f, isInTradeBag: true, isOffered: false));
    }

    [Fact]
    public void Nothing_is_offered_while_the_bag_has_not_caught_up()
    {
        Assert.Equal(OfferStep.Wait, TradeOffers.Next(Split, now: 10.5f, isInTradeBag: false, isOffered: false));
    }

    [Fact]
    public void A_stack_the_bag_never_shows_stays_split_and_unoffered()
    {
        Assert.Equal(OfferStep.GiveUp, TradeOffers.Next(Split, now: 10f + TradeOffers.ShowWaitSeconds + 0.1f, isInTradeBag: false, isOffered: false));
    }

    [Fact]
    public void An_offer_the_game_took_is_done()
    {
        Assert.Equal(OfferStep.Done, TradeOffers.Next(Sent, now: 10.3f, isInTradeBag: true, isOffered: true));
    }

    [Fact]
    public void An_offer_gets_time_to_be_answered()
    {
        Assert.Equal(OfferStep.Wait, TradeOffers.Next(Sent, now: 10.4f, isInTradeBag: true, isOffered: false));
    }

    /// <summary>A refused offer leaves no stray stack behind: the units go back where they came from.</summary>
    [Fact]
    public void A_refused_offer_goes_back_on_its_stack()
    {
        Assert.Equal(OfferStep.Rejoin, TradeOffers.Next(Sent, now: 10.2f + TradeOffers.AnswerWaitSeconds + 0.1f, isInTradeBag: true, isOffered: false));
    }

    [Fact]
    public void A_sent_offer_is_never_sent_twice()
    {
        Assert.NotEqual(OfferStep.Send, TradeOffers.Next(Sent, now: 10.3f, isInTradeBag: true, isOffered: false));
    }

    [Theory]
    [InlineData("[{\"id\":41,\"x\":2,\"y\":1,\"o\":5}]")]
    [InlineData("[{\"id\":7,\"x\":0,\"y\":0,\"o\":5},{\"id\":\"41\",\"x\":2,\"y\":1,\"o\":5}]")]
    public void A_stack_in_the_offer_list_is_offered(string offeredJson)
    {
        Assert.True(TradeOffers.IsOffered(offeredJson, 41));
    }

    [Theory]
    [InlineData("[{\"id\":7,\"x\":0,\"y\":0,\"o\":41}]")]
    [InlineData("[]")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("{\"id\":41}")]
    public void A_stack_missing_from_the_offer_list_is_not_offered(string? offeredJson)
    {
        Assert.False(TradeOffers.IsOffered(offeredJson, 41));
    }
}
