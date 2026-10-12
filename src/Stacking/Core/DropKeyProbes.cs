using System;

namespace Stacking.Core;

/// <summary>What to do with the bag page's answer to a drop key.</summary>
public enum DropKeyReply
{
    /// <summary>The player's own drop key: the game drops the item, as it would without this mod.</summary>
    Drop,

    /// <summary>The answer to this mod's own press: the item named is the one right-clicked, and it is split.</summary>
    Split,

    /// <summary>Nobody can tell whose press it answers, so nothing drops and nothing splits.</summary>
    Swallow,
}

/// <summary>
/// With one bag open, the bag page sends nothing for a right-click, so this mod presses the drop key itself: the page
/// answers a drop key by naming the item under the tooltip. The page answers this mod's presses and the player's
/// alike, may fold two presses in one frame into one answer, and does not answer at all when no tooltip is showing.
/// So each press is counted as it reaches the game, and an answer is only a split while every press since the last
/// answer was this mod's. Once the player presses too, the answers owed are swallowed: a press of this mod never
/// drops anything, and a drop the player asked for never turns into a split. The cost is a player's drop key now and
/// then doing nothing.
/// A press with no answer within <see cref="AnswerWithinFrames"/> frames and <see cref="AnswerWithinMs"/> ms is taken
/// to have none coming (the cursor was over no item), so it can no longer decide a later drop key. Both must pass: the
/// page answers on its own clock, so frames alone expire a press too soon at a high frame rate, and time alone
/// expires it during one slow frame. An answer that still comes after that, before the player's next drop key, is
/// swallowed, even after this mod pressed again: a late answer never drops on this mod's account, and never splits
/// the wrong item. The cost is that the next right-click after one on an empty cell does nothing, and only that one:
/// the press whose answer was swallowed in its place is taken to have had it once its own bound passes.
/// </summary>
/// <param name="Outstanding">This mod's presses still owed an answer.</param>
/// <param name="OursUnseen">This mod's presses sent but not yet seen reaching the game.</param>
/// <param name="ToSwallow">Answers to swallow, owed to presses of this mod that the player's press got mixed up with.</param>
/// <param name="StaleAfterClose">Presses of this mod still owed an answer when the bag closed; an answer that comes while it is closed answers one of them.</param>
/// <param name="Late">Presses of this mod that went unanswered too long; an answer before the player's next drop key is one of them.</param>
/// <param name="MaybeAnswered">Of the outstanding presses, those whose answer may be one already swallowed as late; never more than <paramref name="Outstanding"/>.</param>
/// <param name="LastPressFrame">The frame of this mod's newest press.</param>
/// <param name="LastPressMs">The wall-clock time of this mod's newest press, in ms.</param>
public sealed record DropKeyProbes(
    int Outstanding,
    int OursUnseen,
    int ToSwallow,
    int StaleAfterClose,
    int Late,
    int MaybeAnswered,
    long LastPressFrame,
    long LastPressMs)
{
    /// <summary>The page answers a press in a frame or two; this many frames without one, and <see cref="AnswerWithinMs"/>, means none is coming.</summary>
    public const int AnswerWithinFrames = 30;

    /// <summary>The page answers on its own clock; this long without an answer, and <see cref="AnswerWithinFrames"/>, means none is coming.</summary>
    public const int AnswerWithinMs = 500;

    public static readonly DropKeyProbes None = new(0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>
    /// This mod pressed the drop key. A late answer still owed is kept, so it cannot be taken as this press's answer;
    /// at most one, so presses that never got an answer cost one click between them, never one each.
    /// </summary>
    public DropKeyProbes Probed(long frame, long ms) =>
        this with { Outstanding = Outstanding + 1, OursUnseen = OursUnseen + 1, Late = Math.Min(Late, 1), LastPressFrame = frame, LastPressMs = ms };

    /// <summary>A drop key reached the game. The first ones seen after this mod's presses are taken to be those presses.</summary>
    public DropKeyProbes KeySeen() =>
        OursUnseen > 0
            ? this with { OursUnseen = OursUnseen - 1 }
            : this with { Outstanding = 0, ToSwallow = ToSwallow + Outstanding, Late = 0, MaybeAnswered = 0 };

    /// <summary>
    /// Once a frame. When this mod's newest press has gone unanswered for more than <see cref="AnswerWithinFrames"/>
    /// frames and at least <see cref="AnswerWithinMs"/> ms, nothing is owed to any press any more; an answer still on
    /// its way is swallowed until the player's next drop key. A press that may have been answered already, by the
    /// answer swallowed as late, is taken to have been, so it never costs another click.
    /// </summary>
    public DropKeyProbes Expired(long frame, long ms)
    {
        var isOwed = Outstanding > 0 || OursUnseen > 0 || ToSwallow > 0;
        var isWithinBound = frame - LastPressFrame <= AnswerWithinFrames || ms - LastPressMs < AnswerWithinMs;

        if (!isOwed || isWithinBound) return this;

        return this with { Outstanding = 0, OursUnseen = 0, ToSwallow = 0, Late = Late + Outstanding - MaybeAnswered + ToSwallow, MaybeAnswered = 0 };
    }

    /// <summary>The page answered a drop key.</summary>
    public (DropKeyProbes Next, DropKeyReply Reply) Replied(bool isBagOpen)
    {
        if (!isBagOpen)
        {
            var closed = Closed();

            return closed.StaleAfterClose > 0 ? (closed with { StaleAfterClose = closed.StaleAfterClose - 1 }, DropKeyReply.Swallow) : (closed, DropKeyReply.Drop);
        }

        var open = this with { StaleAfterClose = 0 };

        if (open.Late > 0) return (open with { Late = open.Late - 1, MaybeAnswered = Math.Min(open.MaybeAnswered + 1, open.Outstanding) }, DropKeyReply.Swallow);

        if (open.ToSwallow > 0) return (open with { ToSwallow = open.ToSwallow - 1 }, DropKeyReply.Swallow);

        if (open.Outstanding > 0) return (open with { Outstanding = open.Outstanding - 1, MaybeAnswered = Math.Min(open.MaybeAnswered, open.Outstanding - 1) }, DropKeyReply.Split);

        return (open, DropKeyReply.Drop);
    }

    /// <summary>
    /// The bag closed: no press is owed an answer any more, but an answer already on its way is still swallowed while
    /// the bag stays closed. An answer that comes before the close is noticed counts the bag closed by itself.
    /// </summary>
    public DropKeyProbes Closed() => None with { StaleAfterClose = StaleAfterClose + Outstanding + ToSwallow + Late, LastPressFrame = LastPressFrame, LastPressMs = LastPressMs };
}
