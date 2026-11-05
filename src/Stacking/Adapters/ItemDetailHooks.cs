using System;
using ExiledToolkit.Game;
using GameCore.HotUpdate.ReduxUI;
using Stacking.Core;

namespace Stacking.Adapters;

/// <summary>
/// Keeps a Ctrl-click on an item from opening the item details popup. Every window that shows the popup for a click
/// (the bag on its own or beside a storage, the merchant's trade window, the shop, the stations, the codex) opens it
/// through the one action, so the click is caught there rather than in each window. The bag's own Ctrl-click quick use
/// is decided before that and stays the game's. Static because Harmony patches are.
/// </summary>
internal static class ItemDetailHooks
{
    private static PatchKit? _hooks;

    public static bool Apply(PatchKit hooks)
    {
        _hooks = hooks;

        return hooks.Patch(typeof(ItemDetailHooks), typeof(Ac_ItemDetailPopup_OpenUI), nameof(Ac_ItemDetailPopup_OpenUI.SendAction), prefix: nameof(OpenOnlyWithoutCtrl),
            consequence: "opening the item details popup");
    }

    private static bool OpenOnlyWithoutCtrl()
    {
        try
        {
            return ItemClicks.OpensDetails(SplitKeys.ForClick());
        }
        catch (Exception ex)
        {
            _hooks?.Fail("reading the keys for an item click, so the item details open as in the base game", ex);

            return true;
        }
    }
}
