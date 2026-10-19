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
        Assert.True(TradeOffers.SplitsDrag("bag", "drone", heldKeys));
    }

    [Theory]
    [InlineData(SplitKind.Half)]
    [InlineData(SplitKind.One)]
    [InlineData(SplitKind.Choose)]
    public void A_right_click_on_the_bag_with_shift_or_ctrl_offers_part_of_the_stack(SplitKind heldKeys)
    {
        Assert.True(TradeOffers.SplitsClick("bag", isRightClick: true, heldKeys));
    }

    [Theory]
    [InlineData(SplitKind.None)]
    [InlineData(SplitKind.Join)]
    public void A_plain_drag_to_the_drone_offers_the_whole_stack_as_the_game_does(SplitKind heldKeys)
    {
        Assert.False(TradeOffers.SplitsDrag("bag", "drone", heldKeys));
    }

    [Fact]
    public void A_plain_right_click_offers_the_whole_stack_as_the_game_does()
    {
        Assert.False(TradeOffers.SplitsClick("bag", isRightClick: true, SplitKind.None));
    }

    /// <summary>Shift with a left click is the game's own quick offer of the whole stack.</summary>
    [Fact]
    public void A_shift_left_click_offers_the_whole_stack_as_the_game_does()
    {
        Assert.False(TradeOffers.SplitsClick("bag", isRightClick: false, SplitKind.Half));
    }

    [Theory]
    [InlineData("drone", "drone")]
    [InlineData("bag", "bag")]
    public void A_drag_within_one_side_of_the_window_splits_nothing(string fromGrid, string toGrid)
    {
        Assert.False(TradeOffers.SplitsDrag(fromGrid, toGrid, SplitKind.Half));
    }

    /// <summary>The bug report: splitting what is on the drone back into the bag moved the whole stack.</summary>
    [Theory]
    [InlineData(SplitKind.Half)]
    [InlineData(SplitKind.One)]
    [InlineData(SplitKind.Choose)]
    public void A_drag_from_the_drone_to_the_bag_with_shift_or_ctrl_takes_part_of_the_offer_back(SplitKind heldKeys)
    {
        Assert.True(TradeOffers.SplitsDrag("drone", "bag", heldKeys));
        Assert.True(TradeOffers.TakesBack("drone"));
    }

    [Theory]
    [InlineData(SplitKind.Half)]
    [InlineData(SplitKind.One)]
    [InlineData(SplitKind.Choose)]
    public void A_right_click_on_the_drone_with_shift_or_ctrl_takes_part_of_the_offer_back(SplitKind heldKeys)
    {
        Assert.True(TradeOffers.SplitsClick("drone", isRightClick: true, heldKeys));
    }

    [Fact]
    public void A_plain_drag_or_right_click_off_the_drone_takes_the_whole_offer_back_as_the_game_does()
    {
        Assert.False(TradeOffers.SplitsDrag("drone", "bag", SplitKind.None));
        Assert.False(TradeOffers.SplitsClick("drone", isRightClick: true, SplitKind.None));
    }

    [Fact]
    public void A_split_from_the_bag_offers_rather_than_takes_back()
    {
        Assert.False(TradeOffers.TakesBack("bag"));
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
