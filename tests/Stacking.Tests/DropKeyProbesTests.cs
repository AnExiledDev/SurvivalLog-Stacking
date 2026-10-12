using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

/// <summary>
/// With one bag open a right-click split asks the bag page with a drop key, and the page answers the player's own drop
/// key the same way. The rules: an answer to this mod's press never drops anything, and a drop the player asked for
/// never turns into a split. A press the page never answers stops counting once both a number of frames and a time
/// have passed, so it cannot decide a later key, and even an answer that comes after that never drops.
/// </summary>
public class DropKeyProbesTests
{
    private const int TooLong = DropKeyProbes.AnswerWithinFrames + 1;
    private const int TooLongMs = DropKeyProbes.AnswerWithinMs;

    /// <summary>The wall-clock time of a frame at 240 frames a second, where 31 frames take 129 ms.</summary>
    private static long MsAt240Fps(long frame) => frame * 1000 / 240;

    private static (DropKeyProbes Next, DropKeyReply Reply) Answer(DropKeyProbes probes) => probes.Replied(isBagOpen: true);

    [Fact]
    public void The_answer_to_this_mod_s_press_splits()
    {
        var (_, reply) = Answer(DropKeyProbes.None.Probed(0, 0).KeySeen());

        Assert.Equal(DropKeyReply.Split, reply);
    }

    [Fact]
    public void The_player_s_drop_key_with_no_press_of_this_mod_drops_as_the_game_does()
    {
        var (_, reply) = Answer(DropKeyProbes.None.KeySeen());

        Assert.Equal(DropKeyReply.Drop, reply);
    }

    /// <summary>A slow page, up to the last frame of the bound, still answers the press; frames are counted, so one slow frame never expires it.</summary>
    [Fact]
    public void An_answer_that_comes_late_is_still_a_split_and_never_a_drop()
    {
        var waiting = DropKeyProbes.None.Probed(0, 0).KeySeen().Expired(DropKeyProbes.AnswerWithinFrames, 10 * TooLongMs);

        var (next, reply) = Answer(waiting);

        Assert.Equal(DropKeyReply.Split, reply);
        Assert.Equal(DropKeyProbes.None, next);
    }

    /// <summary>At a high frame rate the bound's frames pass before a slow page answers; the time has not, so the press still waits.</summary>
    [Fact]
    public void An_answer_slower_than_the_frames_but_within_the_time_is_still_a_split()
    {
        var waiting = DropKeyProbes.None.Probed(0, 0).KeySeen().Expired(TooLong, MsAt240Fps(TooLong));

        var (_, reply) = Answer(waiting);

        Assert.Equal(DropKeyReply.Split, reply);
    }

    /// <summary>
    /// The review's case at 240 frames a second: a press whose page takes 150 ms, then another press before it is
    /// answered. The first answer used to be taken as the second press's split, and the second press's own answer
    /// then found nothing owed and dropped the item.
    /// </summary>
    [Fact]
    public void A_slow_answer_and_a_second_press_never_drop_either_item()
    {
        var probes = DropKeyProbes.None.Probed(0, MsAt240Fps(0));

        probes = probes.Expired(31, MsAt240Fps(31)).Probed(35, MsAt240Fps(35));

        var (afterFirst, first) = Answer(probes);
        var (_, second) = Answer(afterFirst);

        Assert.NotEqual(DropKeyReply.Drop, first);
        Assert.NotEqual(DropKeyReply.Drop, second);
    }

    /// <summary>
    /// The first press really did go unanswered too long, and then a second press came. An answer that still comes
    /// may be the first press's, so it is swallowed rather than split; the answer after it is the second press's.
    /// </summary>
    [Fact]
    public void A_late_answer_after_a_new_press_is_swallowed_and_the_new_press_is_still_answered()
    {
        var expired = DropKeyProbes.None.Probed(0, 0).KeySeen().Expired(TooLong, TooLongMs);
        var pressedAgain = expired.Probed(TooLong + 5, TooLongMs + 40).KeySeen();

        var (afterLate, late) = Answer(pressedAgain);
        var (afterOwn, own) = Answer(afterLate);

        Assert.Equal(DropKeyReply.Swallow, late);
        Assert.Equal(DropKeyReply.Split, own);
        Assert.Equal(DropKeyReply.Drop, Answer(afterOwn.KeySeen()).Reply);
    }

    /// <summary>Several presses that went unanswered cost one click between them, never one each.</summary>
    [Fact]
    public void Many_presses_never_answered_cost_one_click_at_most()
    {
        var expired = DropKeyProbes.None.Probed(0, 0).KeySeen().Probed(1, 1).KeySeen().Expired(TooLong + 1, TooLongMs + 1);
        var pressedAgain = expired.Probed(TooLong + 5, TooLongMs + 40).KeySeen().Probed(TooLong + 6, TooLongMs + 41).KeySeen();

        var (afterFirst, first) = Answer(pressedAgain);
        var (afterSecond, second) = Answer(afterFirst);
        var quiet = afterSecond.Expired(2 * TooLong + 6, 2 * TooLongMs + 41);
        var (_, third) = Answer(quiet.Probed(2 * TooLong + 10, 2 * TooLongMs + 80).KeySeen());

        Assert.Equal(DropKeyReply.Swallow, first);
        Assert.Equal(DropKeyReply.Split, second);
        Assert.Equal(DropKeyReply.Split, third);
    }

    [Fact]
    public void Once_answered_the_next_drop_key_of_the_player_drops_again()
    {
        var (afterSplit, _) = Answer(DropKeyProbes.None.Probed(0, 0).KeySeen());

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
        var mixed = DropKeyProbes.None.Probed(0, 0).KeySeen().KeySeen();

        var (afterFirst, first) = Answer(mixed);
        var (_, second) = Answer(afterFirst);

        Assert.Equal(DropKeyReply.Swallow, first);
        Assert.Equal(DropKeyReply.Drop, second);
    }

    /// <summary>The page may fold two presses in one frame into one answer; the press left waiting must not turn the next player key into a split.</summary>
    [Fact]
    public void Two_presses_folded_into_one_answer_never_split_the_player_s_next_drop()
    {
        var folded = DropKeyProbes.None.Probed(0, 0).KeySeen().KeySeen();

        var (afterAnswer, reply) = Answer(folded);
        var (_, players) = Answer(afterAnswer.KeySeen());

        Assert.Equal(DropKeyReply.Swallow, reply);
        Assert.NotEqual(DropKeyReply.Split, players);
    }

    /// <summary>A press with no tooltip showing gets no answer; the cost is one player drop key that does nothing, never a split.</summary>
    [Fact]
    public void A_press_never_answered_costs_the_player_one_drop_key_and_never_splits_it()
    {
        var unanswered = DropKeyProbes.None.Probed(0, 0).KeySeen();

        var (afterFirst, first) = Answer(unanswered.KeySeen());
        var (_, second) = Answer(afterFirst.KeySeen());

        Assert.Equal(DropKeyReply.Swallow, first);
        Assert.Equal(DropKeyReply.Drop, second);
    }

    [Fact]
    public void An_answer_after_the_bag_closed_answers_the_press_and_drops_nothing()
    {
        var closed = DropKeyProbes.None.Probed(0, 0).KeySeen().Closed();

        var (next, reply) = closed.Replied(isBagOpen: false);

        Assert.Equal(DropKeyReply.Swallow, reply);
        Assert.Equal(DropKeyReply.Drop, next.Replied(isBagOpen: false).Reply);
    }

    /// <summary>The close is noticed a frame late; an answer that lands in between still finds the bag shut and is swallowed.</summary>
    [Fact]
    public void An_answer_while_the_bag_is_shut_but_the_close_not_yet_noticed_drops_nothing()
    {
        var waiting = DropKeyProbes.None.Probed(0, 0).KeySeen();

        var (next, reply) = waiting.Replied(isBagOpen: false);

        Assert.Equal(DropKeyReply.Swallow, reply);
        Assert.Equal(DropKeyReply.Drop, next.Closed().Replied(isBagOpen: false).Reply);
    }

    [Fact]
    public void A_bag_opened_again_starts_with_nothing_owed()
    {
        var reopened = DropKeyProbes.None.Probed(0, 0).KeySeen().Closed();

        var (_, reply) = Answer(reopened.KeySeen());

        Assert.Equal(DropKeyReply.Drop, reply);
    }

    /// <summary>
    /// A right-click over no item gets no answer. The next right-click's answer cannot be told from a late one to the
    /// first, so it does nothing. That costs one click and no more: every right-click after it splits, whether it comes
    /// at once or after a pause.
    /// </summary>
    [Theory]
    [InlineData(10, 80)]
    [InlineData(2 * TooLong + 10, 2 * TooLongMs + 80)]
    public void A_press_never_answered_costs_the_next_right_click_and_no_other(int laterFrame, int laterMs)
    {
        const long againFrame = TooLong + 5;
        const long againMs = TooLongMs + 40;
        var expired = DropKeyProbes.None.Probed(0, 0).KeySeen().Expired(TooLong, TooLongMs);

        var (afterSwallowed, swallowed) = Answer(expired.Probed(againFrame, againMs).KeySeen());
        var waited = afterSwallowed.Expired(againFrame + laterFrame, againMs + laterMs);
        var (afterLater, later) = Answer(waited.Probed(againFrame + laterFrame, againMs + laterMs).KeySeen());
        var quiet = afterLater.Expired(againFrame + laterFrame + TooLong, againMs + laterMs + TooLongMs);
        var (_, last) = Answer(quiet.Probed(againFrame + laterFrame + TooLong + 5, againMs + laterMs + TooLongMs + 40).KeySeen());

        Assert.Equal(DropKeyReply.Swallow, swallowed);
        Assert.Equal(DropKeyReply.Split, later);
        Assert.Equal(DropKeyReply.Split, last);
    }

    /// <summary>After the one click it cost, nothing of the press never answered is left to swallow the player's own drop key.</summary>
    [Fact]
    public void A_press_never_answered_leaves_the_player_s_drop_key_dropping_once_its_cost_is_paid()
    {
        var expired = DropKeyProbes.None.Probed(0, 0).KeySeen().Expired(TooLong, TooLongMs);
        var (afterSwallowed, _) = Answer(expired.Probed(TooLong + 5, TooLongMs + 40).KeySeen());

        var quiet = afterSwallowed.Expired(2 * TooLong + 5, 2 * TooLongMs + 40);

        Assert.Equal(DropKeyReply.Drop, Answer(quiet.KeySeen()).Reply);
    }

    /// <summary>The stale press must not swallow the player's own drop key either.</summary>
    [Fact]
    public void A_press_never_answered_expires_and_the_player_s_next_drop_key_drops()
    {
        var expired = DropKeyProbes.None.Probed(0, 0).KeySeen().Expired(TooLong, TooLongMs);

        var (_, reply) = Answer(expired.KeySeen());

        Assert.Equal(DropKeyReply.Drop, reply);
    }

    [Fact]
    public void An_answer_that_comes_after_the_press_expired_is_swallowed_and_never_drops()
    {
        var expired = DropKeyProbes.None.Probed(0, 0).KeySeen().Expired(TooLong, TooLongMs);

        var (afterLate, late) = Answer(expired);

        Assert.Equal(DropKeyReply.Swallow, late);
        Assert.Equal(DropKeyReply.Drop, Answer(afterLate.KeySeen()).Reply);
    }

    /// <summary>Answers owed to a mix of the player's key and a press that never got one stop being swallowed too.</summary>
    [Fact]
    public void Answers_left_to_swallow_expire_with_the_press_they_were_owed_to()
    {
        var mixedThenQuiet = DropKeyProbes.None.Probed(0, 0).KeySeen().KeySeen().Expired(TooLong, TooLongMs);

        var (_, reply) = Answer(mixedThenQuiet.KeySeen());

        Assert.Equal(DropKeyReply.Drop, reply);
    }

    [Fact]
    public void A_press_is_kept_for_the_whole_bound_counted_from_the_newest_press()
    {
        var twoPresses = DropKeyProbes.None.Probed(0, 0).KeySeen().Probed(20, 20).KeySeen();

        var keptByFrames = twoPresses.Expired(20 + DropKeyProbes.AnswerWithinFrames, 20 + 10 * TooLongMs);
        var keptByTime = twoPresses.Expired(20 + TooLong, 20 + TooLongMs - 1);

        Assert.Equal(2, keptByFrames.Outstanding);
        Assert.Equal(2, keptByTime.Outstanding);
        Assert.Equal(0, twoPresses.Expired(20 + TooLong, 20 + TooLongMs).Outstanding);
    }

    /// <summary>
    /// Whatever order presses, keys, answers and expiries (E: the bound passes with no answer) come in, every answer
    /// to a press of this mod is a split or nothing, as long as no drop key was seen after the expiry and no two
    /// presses in a row are answered only after their bound.
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
    [InlineData("PKEPKRR")]
    [InlineData("PKEPKREPKR")]
    public void No_answer_ever_drops_while_a_press_of_this_mod_is_unanswered(string events)
    {
        var probes = DropKeyProbes.None;
        var isOpen = true;
        var pressesOwed = 0;
        var frame = 0L;
        var ms = 0L;

        foreach (var step in events)
        {
            switch (step)
            {
                case 'P':
                    probes = probes.Probed(frame, ms);
                    pressesOwed++;
                    break;
                case 'E':
                    frame += TooLong;
                    ms += TooLongMs;
                    probes = probes.Expired(frame, ms);
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
