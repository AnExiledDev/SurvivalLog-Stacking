using System;
using System.IO;
using GameCore.HotUpdate.ReduxUI;
using Stacking.Core;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>
/// Asks the player how many units to split, in a small panel drawn by picker.js in the game's web UI, and shows the
/// mod's short messages there as toasts. The web view can reload and lose the script at any time, so the script,
/// which installs itself once, is sent ahead of every call. One question at a time: a new one replaces the old,
/// and each carries a token so a late answer to the old one is ignored.
/// </summary>
internal sealed class SplitPicker
{
    private const string ScriptResource = "Stacking.picker.js";
    private const float GiveUpSeconds = 120f;

    private readonly Action<string> _log;
    private string? _script;
    private int _lastToken;
    private Question? _open;
    private bool? _wasKeyboardEnabled;
    private bool _hasReportedRunFailure;

    public SplitPicker(Action<string> log)
    {
        _log = log;
    }

    public void Ask(SplitRequest request, int max, int suggested, float now)
    {
        Close();

        var question = new Question(++_lastToken, request, now);

        if (!Run(PickerCalls.Open(question.Token, max, suggested)))
        {
            _log("could not open the split picker, so nothing moved");

            return;
        }

        _open = question;
        PickerHooks.Opened();
        ClaimKeyboard();
    }

    /// <summary>The drop an answer is for, or null when it answers a question no longer open.</summary>
    public SplitRequest? Answered(int token)
    {
        if (_open is not { } question || question.Token != token) return null;

        Close();

        return question.Request;
    }

    public void Cancelled(int token)
    {
        if (_open?.Token == token) Close();
    }

    /// <summary>Each frame: Esc closes the picker even when the page has not got the keyboard, and a forgotten one gives up.</summary>
    public void Tick(float now)
    {
        if (_open is not { } question) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
        else if (now - question.AskedAt > GiveUpSeconds)
        {
            _log($"the split picker was left open {GiveUpSeconds:0}s, so it closed and nothing moved");
            Close();
        }
    }

    public void Close()
    {
        if (_open == null) return;

        _open = null;
        PickerHooks.Closed();
        ReleaseKeyboard();
        Run(PickerCalls.Close(), withScript: false);
    }

    public void Toast(string text) => Run(PickerCalls.Toast(text));

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

    private sealed record Question(int Token, SplitRequest Request, float AskedAt);
}
