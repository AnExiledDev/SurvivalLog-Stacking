namespace Stacking.Core;

/// <summary>
/// A screen refresh held back while a run of moves is made. The game redraws an open station screen once per item
/// added or moved, and the crafting table's redraw checks every recipe, so a fill that moves eight units redrew the
/// recipe list eight times in one frame. While held, each refresh is only noted, and one goes out when the run ends.
/// Holds nest; only the outermost end sends. Main thread only, and nothing is made.
/// </summary>
public sealed class HeldRefresh
{
    private int _depth;
    private bool _isAsked;

    /// <summary>True while a run of moves is being made, so a refresh asked now waits for its end.</summary>
    public bool IsHeld => _depth > 0;

    /// <summary>True when a refresh was asked during the hold that is open.</summary>
    public bool IsAsked => _isAsked;

    public void Hold() => _depth++;

    /// <summary>The game asks for a refresh: true lets it go ahead now, false means it was noted for the end of the hold.</summary>
    public bool Pass()
    {
        if (_depth == 0) return true;

        _isAsked = true;

        return false;
    }

    /// <summary>
    /// Ends one hold. True when the outermost hold ends with a refresh asked, so the caller sends one now. With
    /// <paramref name="isSentNext"/> the game sends the same refresh itself right after, so none is sent here.
    /// </summary>
    public bool Release(bool isSentNext = false)
    {
        if (_depth == 0 || --_depth > 0) return false;

        var isAsked = _isAsked;

        _isAsked = false;

        return isAsked && !isSentNext;
    }
}
