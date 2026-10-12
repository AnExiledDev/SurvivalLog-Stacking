using System;
using System.IO;
using GameCore.HotUpdate.ReduxUI;
using Stacking.Core;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>
/// Asks the player how many units to split, in a small panel drawn by picker.js in the game's web UI, and shows the
/// mod's short messages there as toasts. The web view can reload and lose the script at any time, so the script,
/// which installs itself once, is sent ahead of every call. The game's keys and clicks go to the picker only once the
/// page says it is on screen, and come back as soon as the page says it is gone, goes quiet, or reloads; the rules
/// for that are the core's PickerSession.
/// </summary>
internal sealed class SplitPicker
{
    private const string ScriptResource = "Stacking.picker.js";
    private const float RepeatedToastLogSeconds = 5f;

    private readonly Action<string> _log;
    private readonly PickerSession<SplitRequest> _session = new();
    private string? _script;
    private bool? _wasKeyboardEnabled;
    private bool _hasReportedRunFailure;
    private (string Text, float At)? _lastLoggedToast;

    public SplitPicker(Action<string> log)
    {
        _log = log;
    }

    public void Ask(SplitRequest request, int max, int suggested, float now)
    {
        Close();

        var token = _session.Ask(request, now);

        if (Run(PickerCalls.Open(token, max, suggested))) return;

        _session.Close();
        _log("could not open the split picker, so nothing moved");
    }

    /// <summary>The page says the picker is on screen: the first time, the game's keys and clicks go to it.</summary>
    public void Shown(int token, float now)
    {
        if (!_session.Shown(token, now)) return;

        PickerHooks.Opened();
        ClaimKeyboard();
    }

    public void Gone(int token)
    {
        if (!_session.Gone(token)) return;

        _log("the split picker was no longer on screen, so it closed and nothing moved");
        Disengage();
    }

    /// <summary>The page's script installed itself again: the page reloaded, taking any open picker with it.</summary>
    public void Reloaded()
    {
        if (!_session.Reloaded()) return;

        _log("the game's web UI reloaded under the split picker, so it closed and nothing moved");
        Disengage();
    }

    /// <summary>The drop an answer is for, or null when it answers a question no longer open.</summary>
    public SplitRequest? Answered(int token)
    {
        var request = _session.Answered(token);

        if (request != null) Disengage();

        return request;
    }

    public void Cancelled(int token)
    {
        if (_session.Cancelled(token)) Disengage();
    }

    /// <summary>Each frame: Esc closes the picker even when the page has not got the keyboard, and the page is checked on.</summary>
    public void Tick(float now)
    {
        if (!_session.IsOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();

            return;
        }

        var step = _session.Tick(now);

        switch (step)
        {
            case PickerStep.Wait:
                break;
            case PickerStep.Check check:
                Run(PickerCalls.Check(check.Token), withScript: false);
                break;
            case PickerStep.GaveUp gaveUp:
                _log(GaveUpText(gaveUp.Reason));
                Disengage();
                break;
            default:
                throw new InvalidOperationException("every picker step needs handling: " + step);
        }
    }

    public void Close()
    {
        if (_session.Close()) Disengage();
    }

    /// <summary>A toast the web UI cannot show goes to the log instead, once per text every few seconds.</summary>
    public void Toast(string text)
    {
        if (Run(PickerCalls.Toast(text))) return;

        var now = Time.unscaledTime;

        if (_lastLoggedToast is { } last && last.Text == text && now - last.At < RepeatedToastLogSeconds) return;

        _lastLoggedToast = (text, now);
        _log("could not show this message in the game: " + text);
    }

    private static string GaveUpText(PickerGiveUp reason) => reason switch
    {
        PickerGiveUp.NeverAppeared => "the split picker did not appear, so the game kept its keys and clicks and nothing moved",
        PickerGiveUp.StoppedAnswering => "the split picker stopped answering, so it closed and nothing moved",
        PickerGiveUp.LeftOpen => $"the split picker was left open {PickerSession<SplitRequest>.GiveUpSeconds:0}s, so it closed and nothing moved",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "every way the picker gives up needs a message"),
    };

    /// <summary>Gives the game back its keys and clicks, and hides the panel if the page still has it.</summary>
    private void Disengage()
    {
        PickerHooks.Closed();
        ReleaseKeyboard();
        Run(PickerCalls.Close(), withScript: false);
    }

    /// <summary>The page's number box needs the keyboard, which the game gives the web UI only while one of its own boxes has focus.</summary>
    private void ClaimKeyboard()
    {
        var layer = WebLayer();

        if (layer == null) return;

        _wasKeyboardEnabled ??= layer.IsKeyboardEnabled;
        layer.SetKeyboardEnabled(true);
    }

    private void ReleaseKeyboard()
    {
        if (_wasKeyboardEnabled is not { } wasEnabled) return;

        _wasKeyboardEnabled = null;
        WebLayer()?.SetKeyboardEnabled(wasEnabled);
    }

    private bool Run(string call, bool withScript = true)
    {
        var webView = WebLayer()?.canvasWebViewPrefab?.WebView;

        if (webView == null) return false;

        try
        {
            webView.ExecuteJavaScript(withScript ? Script() + ";" + call : call, null);

            return true;
        }
        catch (Exception ex)
        {
            if (!_hasReportedRunFailure) _log("could not run the split picker's script in the web UI (reported once): " + ex.Message);

            _hasReportedRunFailure = true;

            return false;
        }
    }

    private static WebUILayer? WebLayer() => GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer();

    private string Script()
    {
        if (_script != null) return _script;

        using var stream = typeof(SplitPicker).Assembly.GetManifestResourceStream(ScriptResource) ?? throw new InvalidOperationException(ScriptResource + " is not embedded");
        using var reader = new StreamReader(stream);

        return _script = reader.ReadToEnd();
    }
}
