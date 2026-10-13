using System;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using Stacking.Core;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>
/// Moves what the game does with Ctrl held in the bag, using an item at once on a click and lighting up the items
/// that can be used that way, to the QuickUseKey, so Ctrl in the bag is only ever this mod's split. The game
/// asks Unity whether Ctrl is held in three places: the bag's click handler, the hotkey manager's per-frame check that
/// drives the highlight, and the bag opening. While one of those runs, a Ctrl read answers for the QuickUseKey; every
/// other Ctrl read, this mod's own included, is left alone. Static because Harmony patches are.
/// </summary>
internal static class QuickUseKeyHooks
{
    private static QuickUseKey _key = new QuickUseKey.GameCtrl();
    private static Action<string> _log = _ => { };
    private static int _openScopes;
    private static int _openedFrame = -1;
    private static bool _hasReportedFailure;

    public static void Apply(Harmony harmony, string setting, Action<string> log)
    {
        _log = log;

        var parsed = QuickUseKeys.Parse(setting, UnityKeyCode);

        if (parsed.Problem != null) log($"Keys/QuickUseKey: {parsed.Problem}");

        if (parsed.Key is QuickUseKey.GameCtrl)
        {
            log("quick use in the bag stays on the game's Ctrl: Keys/QuickUseKey is Ctrl");

            return;
        }

        if (!Hooks.TryPatch(harmony, typeof(QuickUseKeyHooks), "reading a held key",
                () => AccessTools.Method(typeof(Input), nameof(Input.GetKey), new[] { typeof(KeyCode) }), log, prefix: nameof(AnswerCtrlRead)))
        {
            log("quick use in the bag stays on Ctrl this session, so a Ctrl click in the bag can still use an item");

            return;
        }

        _key = parsed.Key;

        if (!TryScope(harmony, log, "clicking an item in the bag", typeof(Reducer_Web_BackpackUI), nameof(Reducer_Web_BackpackUI.RA_ClickItem)))
        {
            log("a Ctrl click in the bag can still use an item this session");
        }

        if (!TryScope(harmony, log, "the bag's quick-use highlight", typeof(GameCore.HotUpdate.Battle.Show.HotKeyManager), "Update"))
        {
            log("the bag's quick-use highlight still follows Ctrl this session");
        }

        if (!TryScope(harmony, log, "opening the bag", typeof(Reducer_Web_BackpackUI), nameof(Reducer_Web_BackpackUI.RA_OpenUI)))
        {
            log("a bag opened with Ctrl held may show the quick-use highlight until Ctrl is let go");
        }

        log(Describe(_key));
    }

    private static bool TryScope(Harmony harmony, Action<string> log, string what, Type type, string method) =>
        Hooks.TryPatchScope(harmony, typeof(QuickUseKeyHooks), what, () => AccessTools.Method(type, method), log,
            open: nameof(OpenScope), close: nameof(CloseScope));

    private static string Describe(QuickUseKey key) => key switch
    {
        QuickUseKey.Off => "quick use in the bag is off: Keys/QuickUseKey is None or empty, so Ctrl in the bag is only for splitting",
        QuickUseKey.Key chosen => $"quick use in the bag is on {(KeyCode)chosen.Code} instead of Ctrl, so Ctrl in the bag is only for splitting",
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "only a key moved off Ctrl is hooked"),
    };

    private static int? UnityKeyCode(string name) =>
        Enum.TryParse<KeyCode>(name, ignoreCase: true, out var key) && Enum.IsDefined(key) ? (int)key : null;

    /// <summary>A scope left open by a method that threw, where the game took no finalizer, closes with its frame.</summary>
    private static void OpenScope()
    {
        var frame = Time.frameCount;

        if (frame != _openedFrame) _openScopes = 0;

        _openedFrame = frame;
        _openScopes++;
    }

    private static void CloseScope()
    {
        if (_openScopes > 0) _openScopes--;
    }

    private static bool AnswerCtrlRead(KeyCode key, ref bool __result)
    {
        if (_openScopes == 0 || !QuickUseKeys.IsCtrl((int)key)) return true;

        try
        {
            if (Time.frameCount != _openedFrame)
            {
                _openScopes = 0;

                return true;
            }

            switch (QuickUseKeys.ReadFor(_key, (int)key))
            {
                case KeyRead.NotHeld:
                    __result = false;

                    return false;
                case KeyRead.Instead instead:
                    __result = Input.GetKey((KeyCode)instead.Code);

                    return false;
                default:
                    return true;
            }
        }
        catch (Exception ex)
        {
            if (!_hasReportedFailure) _log("could not answer a Ctrl read for the quick-use key, so the game read Ctrl (reported once): " + ex.Message);

            _hasReportedFailure = true;

            return true;
        }
    }
}
