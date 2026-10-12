using System;
using System.Collections.Concurrent;
using GameCore.HotUpdate.Battle.Show;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using Stacking.Core;
using UnityEngine;
using Vuplex.WebView.Internal;

namespace Stacking.Adapters;

/// <summary>
/// The game-side half of the split picker. Takes the picker's answers before the game's web layer sees them (it would
/// log them as unknown) and, while the picker is open, keeps the game's hotkeys quiet, so typing "5" does not also
/// pick a hotbar slot, and sends every click to the web UI, which the game only does where its own pages report a
/// button. Static because Harmony patches are.
/// </summary>
internal static class PickerHooks
{
    private static readonly ConcurrentQueue<PickerAnswer> Answers = new();

    private static volatile bool _isPickerOpen;
    private static int _blockedThroughFrame = -1;

    /// <summary>False when the picker's answers cannot be heard, so it must not be opened.</summary>
    public static bool Apply(Harmony harmony, Action<string> log)
    {
        var canHear = Hooks.TryPatch(harmony, typeof(PickerHooks), "the web UI's messages", () => AccessTools.Method(typeof(BaseWebView), "HandleMessageEmitted"), log,
            prefix: nameof(TakeOurs));

        if (!canHear) return false;

        if (!Hooks.TryPatch(harmony, typeof(PickerHooks), "the game's hotkeys", () => AccessTools.Method(typeof(HotKeyManager), "Update"), log, prefix: nameof(SkipWhileOpen)))
        {
            log("numbers typed in the split picker will also reach the hotbar");
        }

        if (!Hooks.TryPatch(harmony, typeof(PickerHooks), "where clicks go", () => AccessTools.Method(typeof(WebUILayer), "ValidateRaycast"), log, postfix: nameof(RouteClicksToPicker)))
        {
            log("clicks on the split picker may fall through to the game");
        }

        return true;
    }

    public static bool TryTakeAnswer(out PickerAnswer? answer) => Answers.TryDequeue(out answer);

    public static void Opened() => _isPickerOpen = true;

    /// <summary>Keeps hotkeys quiet through the next frame, so the Esc that closed the picker does not also open the game's menu.</summary>
    public static void Closed()
    {
        _isPickerOpen = false;
        _blockedThroughFrame = Time.frameCount + 1;
    }

    private static bool TakeOurs(string serializedMessage)
    {
        if (!PickerAnswer.IsOurs(serializedMessage)) return true;

        Answers.Enqueue(PickerAnswer.Parse(serializedMessage));

        return false;
    }

    private static bool SkipWhileOpen() => !(_isPickerOpen || Time.frameCount <= _blockedThroughFrame);

    private static void RouteClicksToPicker(ref bool __result)
    {
        if (_isPickerOpen) __result = true;
    }
}
