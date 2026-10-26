using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

/// <summary>
/// The station screens' refresh held while a run of moves is made. Each refresh the game sends while the crafting table
/// is open redraws its whole recipe list, so a refresh let through mid-run brings the stutter back, and one lost at
/// the end leaves the screen showing items that moved.
/// </summary>
public class HeldRefreshTests
{
    [Fact]
    public void with_no_hold_the_game_refreshes_at_once()
    {
        var refresh = new HeldRefresh();

        Assert.True(refresh.Pass());
        Assert.False(refresh.IsAsked);
    }

    [Fact]
    public void every_refresh_asked_during_a_hold_waits_and_one_goes_out_at_the_end()
    {
        var refresh = new HeldRefresh();

        refresh.Hold();

        Assert.False(refresh.Pass());
        Assert.False(refresh.Pass());
        Assert.False(refresh.Pass());
        Assert.True(refresh.IsAsked);
        Assert.True(refresh.Release());
        Assert.False(refresh.IsHeld);
    }

    [Fact]
    public void only_the_outermost_hold_sends()
    {
        var refresh = new HeldRefresh();

        refresh.Hold();
        refresh.Hold();

        Assert.False(refresh.Pass());
        Assert.False(refresh.Release());
        Assert.True(refresh.IsHeld);
        Assert.False(refresh.Pass());
        Assert.True(refresh.Release());
    }

    [Fact]
    public void a_refresh_the_game_sends_right_after_is_not_sent_twice()
    {
        var refresh = new HeldRefresh();

        refresh.Hold();
        refresh.Pass();

        Assert.False(refresh.Release(isSentNext: true));
        Assert.False(refresh.IsAsked);
    }

    [Fact]
    public void a_hold_with_nothing_asked_sends_nothing()
    {
        var refresh = new HeldRefresh();

        refresh.Hold();

        Assert.False(refresh.Release());
    }

    [Fact]
    public void an_ask_from_one_hold_does_not_carry_into_the_next()
    {
        var refresh = new HeldRefresh();

        refresh.Hold();
        refresh.Pass();
        refresh.Release();
        refresh.Hold();

        Assert.False(refresh.Release());
        Assert.True(refresh.Pass());
    }

    [Fact]
    public void a_release_without_a_hold_changes_nothing()
    {
        var refresh = new HeldRefresh();

        Assert.False(refresh.Release());
        Assert.False(refresh.IsHeld);
        Assert.True(refresh.Pass());
    }
}
