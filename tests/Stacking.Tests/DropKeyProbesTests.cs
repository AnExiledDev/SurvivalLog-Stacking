using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

/// <summary>
/// With one bag open a right-click split asks the bag page with a drop key, and the page answers the player's own drop
/// key the same way. The rules: an answer to this mod's press never drops anything, and a drop the player asked for
/// never turns into a split. A press the page never answers stops counting after a bounded number of frames, so it
/// cannot decide a later click or key, and even an answer that comes after that never drops.
/// </summary>
public class DropKeyProbesTests
{
    private static (DropKeyProbes Next, DropKeyReply Reply) Answer(DropKeyProbes probes) => probes.Replied(isBagOpen: true);

    [Fact]
    public void The_answer_to_this_mod_s_press_splits()
    {
        var (_, reply) = Answer(DropKeyProbes.None.Probed(0).KeySeen());

        Assert.Equal(DropKeyReply.Split, reply);
    }

    [Fact]
    public void The_player_s_drop_key_with_no_press_of_this_mod_drops_as_the_game_does()
    {
        var (_, reply) = Answer(DropKeyProbes.None.KeySeen());

        Assert.Equal(DropKeyReply.Drop, reply);
    }

    /// <summary>A slow page, up to the last frame of the bound, still answers the press; frames are counted, so a slow frame never expires it.</summary>
    [Fact]
    public void An_answer_that_comes_late_is_still_a_split_and_never_a_drop()
    {
        var waiting = DropKeyProbes.None.Probed(0).KeySeen().Expired(DropKeyProbes.AnswerWithinFrames);

        var (next, reply) = Answer(waiting);

        Assert.Equal(DropKeyReply.Split, reply);
        Assert.Equal(DropKeyProbes.None, next);
    }

    [Fact]
    public void Once_answered_the_next_drop_key_of_the_player_drops_again()
    {
        var (afterSplit, _) = Answer(DropKeyProbes.None.Probed(0).KeySeen());

        var (_, reply) = Answer(afterSplit.KeySeen());

        Assert.Equal(DropKeyReply.Drop, reply);
    }

    /// <summary>
    /// The player pressed while this mod's press waited, and the first answer may be either press's, so it neither
    /// splits nor drops. An answer after it can only be the player's, and drops.
    /// </summary>
    [Fact]
    public void A_player_key_while_a_press_waits_swallows_the_answer_owed_to_the_press()
    {
        var mixed = DropKeyProbes.None.Probed(0).KeySeen().KeySeen();

        var (afterFirst, first) = Answer(mixed);
        var (_, second) = Answer(afterFirst);

        Assert.Equal(DropKeyReply.Swallow, first);
        Assert.Equal(DropKeyReply.Drop, second);
    }

    /// <summary>The page may fold two presses in one frame into one answer; the press left waiting must not turn the next player key into a split.</summary>
    [Fact]
    public void Two_presses_folded_into_one_answer_never_split_the_player_s_next_drop()
    {
        var folded = DropKeyProbes.None.Probed(0).KeySeen().KeySeen();

        var (afterAnswer, reply) = Answer(folded);
        var (_, players) = Answer(afterAnswer.KeySeen());

        Assert.Equal(DropKeyReply.Swallow, reply);
        Assert.NotEqual(DropKeyReply.Split, players);
    }

    /// <summary>A press with no tooltip showing gets no answer; the cost is one player drop key that does nothing, never a split.</summary>
    [Fact]
    public void A_press_never_answered_costs_the_player_one_drop_key_and_never_splits_it()
    {
        var unanswered = DropKeyProbes.None.Probed(0).KeySeen();

        var (afterFirst, first) = Answer(unanswered.KeySeen());
        var (_, second) = Answer(afterFirst.KeySeen());

        Assert.Equal(DropKeyReply.Swallow, first);
        Assert.Equal(DropKeyReply.Drop, second);
    }

    [Fact]
    public void An_answer_after_the_bag_closed_answers_the_press_and_drops_nothing()
    {
        var closed = DropKeyProbes.None.Probed(0).KeySeen().Closed();

        var (next, reply) = closed.Replied(isBagOpen: false);

        Assert.Equal(DropKeyReply.Swallow, reply);
        Assert.Equal(DropKeyReply.Drop, next.Replied(isBagOpen: false).Reply);
    }

    /// <summary>The close is noticed a frame late; an answer that lands in between still finds the bag shut and is swallowed.</summary>
    [Fact]
    public void An_answer_while_the_bag_is_shut_but_the_close_not_yet_noticed_drops_nothing()
    {
        var waiting = DropKeyProbes.None.Probed(0).KeySeen();

        var (next, reply) = waiting.Replied(isBagOpen: false);

        Assert.Equal(DropKeyReply.Swallow, reply);
        Assert.Equal(DropKeyReply.Drop, next.Closed().Replied(isBagOpen: false).Reply);
    }

    [Fact]
    public void A_bag_opened_again_starts_with_nothing_owed()
    {
        var reopened = DropKeyProbes.None.Probed(0).KeySeen().Closed();

        var (_, reply) = Answer(reopened.KeySeen());

        Assert.Equal(DropKeyReply.Drop, reply);
    }

    private const int TooLong = DropKeyProbes.AnswerWithinFrames + 1;

    /// <summary>A right-click over no item gets no answer; the next right-click, on an item, is split as asked and nothing of the first is left over.</summary>
    [Fact]
    public void A_press_never_answered_expires_and_the_next_press_is_answered_alone()
    {
        var expired = DropKeyProbes.None.Probed(0).KeySeen().Expired(TooLong);

        var (afterAnswer, reply) = Answer(expired.Probed(TooLong + 5).KeySeen());

        Assert.Equal(DropKeyReply.Split, reply);
        Assert.Equal(0, afterAnswer.Outstanding);
        Assert.Equal(DropKeyReply.Drop, Answer(afterAnswer.KeySeen()).Reply);
    }

    /// <summary>The stale press must not swallow the player's own drop key either.</summary>
    [Fact]
    public void A_press_never_answered_expires_and_the_player_s_next_drop_key_drops()
    {
        var expired = DropKeyProbes.None.Probed(0).KeySeen().Expired(TooLong);

        var (_, reply) = Answer(expired.KeySeen());

        Assert.Equal(DropKeyReply.Drop, reply);
    }

    [Fact]
    public void An_answer_that_comes_after_the_press_expired_is_swallowed_and_never_drops()
    {
        var expired = DropKeyProbes.None.Probed(0).KeySeen().Expired(TooLong);

        var (afterLate, late) = Answer(expired);

        Assert.Equal(DropKeyReply.Swallow, late);
        Assert.Equal(DropKeyReply.Drop, Answer(afterLate.KeySeen()).Reply);
    }

    /// <summary>Answers owed to a mix of the player's key and a press that never got one stop being swallowed too.</summary>
    [Fact]
    public void Answers_left_to_swallow_expire_with_the_press_they_were_owed_to()
    {
        var mixedThenQuiet = DropKeyProbes.None.Probed(0).KeySeen().KeySeen().Expired(TooLong);

        var (_, reply) = Answer(mixedThenQuiet.KeySeen());

        Assert.Equal(DropKeyReply.Drop, reply);
    }

    [Fact]
    public void A_press_is_kept_for_the_whole_bound_counted_from_the_newest_press()
    {
        var twoPresses = DropKeyProbes.None.Probed(0).KeySeen().Probed(20).KeySeen();

        var kept = twoPresses.Expired(20 + DropKeyProbes.AnswerWithinFrames);

        Assert.Equal(2, kept.Outstanding);
        Assert.Equal(0, kept.Expired(20 + TooLong).Outstanding);
    }

    /// <summary>
    /// Whatever order presses, keys, answers and expiries (E: the bound passes with no answer) come in, every answer
    /// to a press of this mod is a split or nothing, as long as no drop key was seen after the expiry.
    /// </summary>
    [Theory]
    [InlineData("PKR")]
    [InlineData("PPKKRR")]
    [InlineData("PKKRR")]
    [InlineData("PKPKRR")]
    [InlineData("PKRPKR")]
    [InlineData("PPKKR")]
    [InlineData("PKCR")]
    [InlineData("PKXR")]
    [InlineData("PKKCRR")]
    [InlineData("PKER")]
    [InlineData("PPKKERR")]
    [InlineData("PKKERR")]
    [InlineData("PKECR")]
    public void No_answer_ever_drops_while_a_press_of_this_mod_is_unanswered(string events)
    {
        var probes = DropKeyProbes.None;
        var isOpen = true;
        var pressesOwed = 0;
        var frame = 0L;

        foreach (var step in events)
        {
            switch (step)
            {
                case 'P':
                    probes = probes.Probed(frame);
                    pressesOwed++;
                    break;
                case 'E':
                    frame += TooLong;
                    probes = probes.Expired(frame);
                    break;
                case 'K':
                    probes = probes.KeySeen();
                    break;
                case 'C':
                    probes = probes.Closed();
                    isOpen = false;
                    break;
                case 'X':
                    isOpen = false;
                    break;
                case 'R':
                    (probes, var reply) = probes.Replied(isOpen);

                    if (pressesOwed > 0) Assert.NotEqual(DropKeyReply.Drop, reply);

                    pressesOwed = Math.Max(0, pressesOwed - 1);
                    break;
            }
        }
    }
}
