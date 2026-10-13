using System;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;
using Stacking.Core;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>
/// Makes the shredder shred every unit of a stack. A finished shred (Furniture.SettleShred) hands out one item's yield
/// and then force-removes the item it shredded; while it settles, that removal takes one unit off a stack instead, and
/// the game's own restart picks the rest of the stack as the next item to shred.
/// </summary>
internal static class ShredderStacks
{
    private const int MaxLoggedFailures = 3;

    [ThreadStatic] private static Settle? _settle;

    private static Action<string> _log = _ => { };
    private static Action<string> _logFileOnly = _ => { };
    private static int _failures;

    public static bool Apply(Harmony harmony, Action<string> log, Action<string> logFileOnly)
    {
        _log = log;
        _logFileOnly = logFileOnly;

        var isHooked = Hooks.TryPatchScope(harmony, typeof(ShredderStacks), "a finished shred",
                           () => AccessTools.Method(typeof(Furniture), nameof(Furniture.SettleShred)), log, nameof(OpenSettle), nameof(CloseSettle))
                       && Hooks.TryPatch(harmony, typeof(ShredderStacks), "the shredder removing what it shredded",
                           () => AccessTools.Method(typeof(ItemManager), nameof(ItemManager.ForceRemoveItem), new[] { typeof(long) }), log, prefix: nameof(TakeOneUnit));

        log(isHooked
            ? "the shredder shreds every item of a stack, one at a time, each with its own yield"
            : "the shredder still shreds a stack as one item this session");

        return isHooked;
    }

    private static void OpenSettle(Furniture __instance)
    {
        _settle = null;

        try
        {
            if (__instance._shredderComp is { } shredder) _settle = new Settle(Time.frameCount, shredder.HeadItemId);
        }
        catch (Exception ex)
        {
            Fail("reading what the shredder is shredding", ex);
        }
    }

    private static void CloseSettle() => _settle = null;

    /// <summary>Runs before every forced removal in the game, so anything outside a shred returns at the first check.</summary>
    private static bool TakeOneUnit(ItemManager __instance, long __0)
    {
        var settle = _settle;

        if (settle == null) return true;

        // Without a finalizer a shred that throws leaves its settle open; a frame later it is known to be stale.
        if (settle.Frame != Time.frameCount)
        {
            _settle = null;

            return true;
        }

        try
        {
            var itemId = __0;
            var item = __instance.GetItemData(itemId);

            if (item == null || ShredRule.Removal(isSettlingShred: true, itemId, settle.ShreddedItemId, item.ItemCount) != ShredRemoval.OneUnit) return true;

            var countBefore = item.ItemCount;

            if (!__instance.DecreaseItemCount(item, 1)) throw new InvalidOperationException($"the game would not take one unit of item {item.ItemConfigId}");

            _logFileOnly($"shredded one of {countBefore} (item {item.ItemConfigId}); {countBefore - 1} left to shred");

            return false;
        }
        catch (Exception ex)
        {
            Fail("taking one shredded unit off a stack", ex);

            return true;
        }
    }

    private static void Fail(string what, Exception ex)
    {
        _failures++;

        if (_failures <= MaxLoggedFailures) _log($"{what} failed, the game's own removal went ahead (logged {_failures} of {MaxLoggedFailures}): {ex}");
    }

    private sealed record Settle(int Frame, long ShreddedItemId);
}
