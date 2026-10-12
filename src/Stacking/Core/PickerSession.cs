namespace Stacking.Core;

/// <summary>What the split picker needs from the game on a frame.</summary>
public abstract record PickerStep
{
    private PickerStep()
    {
    }

    public sealed record Wait : PickerStep;

    /// <summary>Ask the page whether the picker with this token is still on screen; it answers Shown or Gone.</summary>
    public sealed record Check(int Token) : PickerStep;

    /// <summary>The picker closed on the game's side: the game gets its keys and clicks back.</summary>
    public sealed record GaveUp(PickerGiveUp Reason) : PickerStep;
}

public enum PickerGiveUp
{
    /// <summary>The page never said the picker appeared: its script failed, or the page was reloading.</summary>
    NeverAppeared,

    /// <summary>The page stopped answering the checks while the picker was up.</summary>
    StoppedAnswering,

    /// <summary>Nobody answered the question for too long.</summary>
    LeftOpen,
}

/// <summary>
/// One split question at a time, as the game sees it. The picker counts as on screen only once the page says so, and
/// while it is up the page is checked about once a second, so a page that failed, reloaded or hung cannot keep the
/// game's keys and clicks for a panel nobody can see. Time comes in as an argument; nothing here reads a clock.
/// </summary>
public sealed class PickerSession<TRequest> where TRequest : class
{
    /// <summary>A reply is overdue after this long, but never before ReplyFrames frames, so one long frame is not a silent page.</summary>
    public const float ReplySeconds = 1f;
    public const int ReplyFrames = 3;
    public const float CheckEverySeconds = 1f;
    public const float GiveUpSeconds = 120f;

    private static readonly PickerStep Waiting = new PickerStep.Wait();

    private State _state = new State.Closed();
    private int _lastToken;

    public bool IsOpen => _state is not State.Closed;

    /// <summary>Opens a question in place of any open one and returns the token the page must echo.</summary>
    public int Ask(TRequest request, float now)
    {
        var question = new Question(++_lastToken, request, now);
        _state = new State.Opening(question, new Reply(now, 0));

        return question.Token;
    }

    /// <summary>The page says the picker with this token is on screen. True the first time, when the game's input goes to it.</summary>
    public bool Shown(int token, float now)
    {
        switch (_state)
        {
            case State.Opening opening when opening.Question.Token == token:
                _state = new State.Showing(opening.Question, now + CheckEverySeconds, Due: null);

                return true;
            case State.Showing showing when showing.Question.Token == token:
                _state = showing with { NextCheckAt = now + CheckEverySeconds, Due = null };

                return false;
            default:
                return false;
        }
    }

    /// <summary>The page says the picker with this token is not on screen. True when that closed the open question.</summary>
    public bool Gone(int token) => CloseIfAsking(token);

    /// <summary>
    /// The page's script installed itself. While the picker opens, that is the open call's own install; once it is up,
    /// the page reloaded and took the picker with it. True when that closed the open question.
    /// </summary>
    public bool Reloaded()
    {
        if (_state is not State.Showing) return false;

        _state = new State.Closed();

        return true;
    }

    /// <summary>The drop the answer is for, closing the question; null when it answers a question no longer open.</summary>
    public TRequest? Answered(int token)
    {
        if (OpenQuestion() is not { } question || question.Token != token) return null;

        _state = new State.Closed();

        return question.Request;
    }

    public bool Cancelled(int token) => CloseIfAsking(token);

    /// <summary>True when a question was open.</summary>
    public bool Close()
    {
        if (!IsOpen) return false;

        _state = new State.Closed();

        return true;
    }

    /// <summary>Once a frame: waits for a reply, checks on a picker that is up, and gives up on one that is gone or forgotten.</summary>
    public PickerStep Tick(float now)
    {
        if (OpenQuestion() is not { } question) return Waiting;

        if (now - question.AskedAt >= GiveUpSeconds) return GiveUp(PickerGiveUp.LeftOpen);

        switch (_state)
        {
            case State.Opening opening:
                if (!IsStillDue(opening.Due, now, out var waitedToOpen)) return GiveUp(PickerGiveUp.NeverAppeared);

                _state = opening with { Due = waitedToOpen };

                return Waiting;
            case State.Showing { Due: { } due } showing:
                if (!IsStillDue(due, now, out var waitedToCheck)) return GiveUp(PickerGiveUp.StoppedAnswering);

                _state = showing with { Due = waitedToCheck };

                return Waiting;
            case State.Showing showing when now >= showing.NextCheckAt:
                _state = showing with { Due = new Reply(now, 0) };

                return new PickerStep.Check(question.Token);
            default:
                return Waiting;
        }
    }

    private bool CloseIfAsking(int token)
    {
        if (OpenQuestion()?.Token != token) return false;

        _state = new State.Closed();

        return true;
    }

    private PickerStep GiveUp(PickerGiveUp reason)
    {
        _state = new State.Closed();

        return new PickerStep.GaveUp(reason);
    }

    private Question? OpenQuestion() => _state switch
    {
        State.Opening opening => opening.Question,
        State.Showing showing => showing.Question,
        _ => null,
    };

    /// <summary>Counts this frame against a reply; false once it has been both too long and too many frames.</summary>
    private static bool IsStillDue(Reply due, float now, out Reply waited)
    {
        waited = due with { Frames = due.Frames + 1 };

        return now - waited.SentAt < ReplySeconds || waited.Frames < ReplyFrames;
    }

    private sealed record Question(int Token, TRequest Request, float AskedAt);

    /// <summary>A call the page has not answered yet: when it went out and how many frames have passed since.</summary>
    private sealed record Reply(float SentAt, int Frames);

    private abstract record State
    {
        private State()
        {
        }

        public sealed record Closed : State;

        /// <summary>The open call went out; the page has not yet said the picker is on screen.</summary>
        public sealed record Opening(Question Question, Reply Due) : State;

        /// <summary>On screen. Due is set while a check waits for its reply.</summary>
        public sealed record Showing(Question Question, float NextCheckAt, Reply? Due) : State;
    }
}
