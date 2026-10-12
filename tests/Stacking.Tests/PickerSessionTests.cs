using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

/// <summary>
/// The game's keys and clicks go to the split picker only while the page shows it. Times are seconds, one Tick a frame;
/// they are exact binary fractions so no test leans on float rounding.
/// </summary>
public class PickerSessionTests
{
    private const string Drop = "the dropped stack";

    private static readonly PickerStep Waiting = new PickerStep.Wait();

    [Fact]
    public void The_first_word_from_the_page_hands_over_the_game_s_input_and_later_ones_do_not_again()
    {
        var session = new PickerSession<string>();
        var token = session.Ask(Drop, 0f);

        Assert.True(session.Shown(token, 0.125f));
        Assert.False(session.Shown(token, 1.25f));
        Assert.True(session.IsOpen);
    }

    [Fact]
    public void A_picker_the_page_never_shows_gives_the_game_its_input_back_after_a_second()
    {
        var session = new PickerSession<string>();

        session.Ask(Drop, 0f);

        Assert.Equal(Waiting, session.Tick(0.5f));
        Assert.Equal(Waiting, session.Tick(0.75f));
        Assert.Equal(new PickerStep.GaveUp(PickerGiveUp.NeverAppeared), session.Tick(1f));
        Assert.False(session.IsOpen);
    }

    [Fact]
    public void One_long_frame_is_not_a_page_that_never_answered()
    {
        var session = new PickerSession<string>();
        var token = session.Ask(Drop, 0f);

        Assert.Equal(Waiting, session.Tick(5f));
        Assert.True(session.Shown(token, 5.125f));
        Assert.True(session.IsOpen);
    }

    [Fact]
    public void A_late_word_about_an_older_picker_neither_shows_nor_saves_the_current_one()
    {
        var session = new PickerSession<string>();
        var old = session.Ask("an older drop", 0f);

        session.Ask(Drop, 0.25f);

        Assert.False(session.Shown(old, 0.375f));
        Assert.Equal(Waiting, session.Tick(0.5f));
        Assert.Equal(Waiting, session.Tick(1f));
        Assert.Equal(new PickerStep.GaveUp(PickerGiveUp.NeverAppeared), session.Tick(1.5f));
    }

    [Fact]
    public void A_page_reload_while_the_picker_is_up_closes_it_and_moves_nothing()
    {
        var session = new PickerSession<string>();
        var token = session.Ask(Drop, 0f);

        session.Shown(token, 0.125f);

        Assert.True(session.Reloaded());
        Assert.False(session.IsOpen);
        Assert.Null(session.Answered(token));
    }

    /// <summary>The open call carries the script, so a page that had lost it installs it and says ready just before it shows the picker.</summary>
    [Fact]
    public void The_script_installing_itself_for_the_picker_being_opened_is_not_a_reload()
    {
        var session = new PickerSession<string>();
        var token = session.Ask(Drop, 0f);

        Assert.False(session.Reloaded());
        Assert.True(session.Shown(token, 0.125f));
    }

    [Fact]
    public void A_picker_that_is_up_is_checked_every_second_and_closes_when_the_page_stops_answering()
    {
        var session = new PickerSession<string>();
        var token = session.Ask(Drop, 0f);

        session.Shown(token, 0f);

        Assert.Equal(Waiting, session.Tick(0.5f));
        Assert.Equal(new PickerStep.Check(token), session.Tick(1f));
        Assert.Equal(Waiting, session.Tick(1.5f));
        Assert.Equal(Waiting, session.Tick(1.75f));
        Assert.Equal(new PickerStep.GaveUp(PickerGiveUp.StoppedAnswering), session.Tick(2f));
        Assert.False(session.IsOpen);
    }

    [Fact]
    public void A_page_that_answers_its_check_keeps_the_picker_up_until_the_next_one()
    {
        var session = new PickerSession<string>();
        var token = session.Ask(Drop, 0f);

        session.Shown(token, 0f);

        Assert.Equal(new PickerStep.Check(token), session.Tick(1f));
        Assert.False(session.Shown(token, 1.25f));
        Assert.Equal(Waiting, session.Tick(1.5f));
        Assert.Equal(Waiting, session.Tick(2f));
        Assert.Equal(Waiting, session.Tick(2.125f));
        Assert.Equal(new PickerStep.Check(token), session.Tick(2.25f));
        Assert.True(session.IsOpen);
    }

    [Fact]
    public void The_page_saying_the_picker_is_gone_closes_it_but_not_when_it_means_an_older_one()
    {
        var session = new PickerSession<string>();
        var old = session.Ask("an older drop", 0f);
        var token = session.Ask(Drop, 0f);

        session.Shown(token, 0f);

        Assert.False(session.Gone(old));
        Assert.True(session.IsOpen);
        Assert.True(session.Gone(token));
        Assert.False(session.IsOpen);
        Assert.Equal(Waiting, session.Tick(5f));
    }

    [Fact]
    public void An_answer_carries_out_its_drop_once()
    {
        var session = new PickerSession<string>();
        var token = session.Ask(Drop, 0f);

        session.Shown(token, 0f);

        Assert.Equal(Drop, session.Answered(token));
        Assert.Null(session.Answered(token));
        Assert.False(session.IsOpen);
    }

    [Fact]
    public void A_late_answer_to_an_older_picker_moves_nothing_and_leaves_the_current_one_open()
    {
        var session = new PickerSession<string>();
        var old = session.Ask("an older drop", 0f);
        var token = session.Ask(Drop, 0f);

        Assert.Null(session.Answered(old));
        Assert.False(session.Cancelled(old));
        Assert.True(session.IsOpen);
        Assert.Equal(Drop, session.Answered(token));
    }

    [Fact]
    public void A_picker_nobody_answers_closes_after_two_minutes_even_while_the_page_keeps_it_up()
    {
        var session = new PickerSession<string>();
        var token = session.Ask(Drop, 0f);

        session.Shown(token, 0f);

        for (var second = 1f; second < PickerSession<string>.GiveUpSeconds; second++)
        {
            session.Tick(second);
            session.Shown(token, second);
        }

        Assert.Equal(new PickerStep.GaveUp(PickerGiveUp.LeftOpen), session.Tick(PickerSession<string>.GiveUpSeconds));
        Assert.False(session.IsOpen);
    }
}
