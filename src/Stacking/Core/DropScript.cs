namespace Stacking.Core;

/// <summary>What the drop watcher sends to the game's web UI this frame.</summary>
public enum DropScriptSend
{
    Nothing,

    /// <summary>The short check, which asks for the script only when the page lost it.</summary>
    Check,

    /// <summary>The whole drop watcher script.</summary>
    Script,
}

/// <summary>
/// When drops.js goes to the game's web UI. The web UI can reload and lose it at any time, so every few seconds a
/// short check runs on the page; it says "slmod|stacking|drops" only when the script is missing, and only then is the
/// whole script sent again. The script goes once at the start, and again after the page asked for it or after a
/// send that did not reach the page.
/// </summary>
public sealed class DropScriptSchedule
{
    public const float CheckSeconds = 3f;

    /// <summary>
    /// Run on the page every few seconds. drops.js sets window.__slstackingDrops as it installs, so a page that has it
    /// says nothing.
    /// </summary>
    public const string Check = "window.__slstackingDrops||window.vuplex&&window.vuplex.postMessage('" + PickerAnswer.Prefix + "drops')";

    private float _lastAt = float.NegativeInfinity;
    private bool _isScriptOnPage;
    private bool _isAskedFor;

    /// <summary>Each frame: what to send now, if anything.</summary>
    public DropScriptSend Next(float now)
    {
        if (_isAskedFor)
        {
            _isAskedFor = false;
            _lastAt = now;
            _isScriptOnPage = true;

            return DropScriptSend.Script;
        }

        if (now - _lastAt < CheckSeconds) return DropScriptSend.Nothing;

        _lastAt = now;

        if (_isScriptOnPage) return DropScriptSend.Check;

        _isScriptOnPage = true;

        return DropScriptSend.Script;
    }

    /// <summary>The page said the script is missing, so it goes again at the next frame.</summary>
    public void Missing() => _isAskedFor = true;

    /// <summary>The last send never reached a page (no web UI yet, or the call failed), so the next one is the whole script again.</summary>
    public void NotSent() => _isScriptOnPage = false;
}
