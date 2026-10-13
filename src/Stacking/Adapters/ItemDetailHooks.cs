using System;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using Stacking.Core;

namespace Stacking.Adapters;

/// <summary>
/// Keeps a Ctrl-click on an item from opening the item details popup. Every window that shows the popup for a click
/// (the bag on its own or beside a storage, the merchant's trade window, the shop, the stations, the codex) opens it
/// through the one action, so the click is caught there rather than in each window. The bag's own Ctrl-click quick use
/// is decided before that and stays the game's. With splitting off nothing samples the keys each frame, so only a Ctrl
/// still down as the click arrives counts. Static because Harmony patches are.
/// </summary>
internal static class ItemDetailHooks
{
    private static Action<string> _log = _ => { };
    private static bool _hasReportedFailure;

    public static bool Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        return Hooks.TryPatch(harmony, typeof(ItemDetailHooks), "opening the item details popup",
            () => AccessTools.Method(typeof(Ac_ItemDetailPopup_OpenUI), nameof(Ac_ItemDetailPopup_OpenUI.SendAction)), log, prefix: nameof(OpenOnlyWithoutCtrl));
    }

    private static bool OpenOnlyWithoutCtrl()
    {
        try
        {
            return ItemClicks.OpensDetails(SplitKeys.Current());
        }
        catch (Exception ex)
        {
            if (!_hasReportedFailure) _log($"reading the keys for an item click failed, so the item details open as in the base game: {ex}");

            _hasReportedFailure = true;

            return true;
        }
    }
}
