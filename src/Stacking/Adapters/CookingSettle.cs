using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;
using Stacking.Core;
using UnityEngine;
using GameItems = Il2CppSystem.Collections.Generic.List<GameCore.HotUpdate.Battle.Logic.ItemData>;
using GameReturns = Il2CppSystem.Collections.Generic.List<Il2CppSystem.Collections.Generic.KeyValuePair<int, int>>;
using GameTally = Il2CppSystem.Collections.Generic.Dictionary<int, int>;
using MatchedRecipes = Il2CppSystem.Collections.Generic.List<GameCore.HotUpdate.Battle.Logic.Furniture.MatchedRecipeEntry>;

namespace Stacking.Adapters;

/// <summary>
/// Takes the units a finished cook used from each stack. The game counts a whole stack against what the dishes need
/// but removes one unit of it, so while a cook settles, each removal of a bench stack first takes the units the dishes
/// used beyond that one, and hands out what each of those units leaves behind (a can, a bottle) as the game does for
/// the one it removes.
/// </summary>
internal static class CookingSettle
{
    private const int MaxLoggedPopUpFailures = 3;

    [ThreadStatic] private static Settle? _settle;

    private static Action<string> _log = _ => { };
    private static int _popUpFailures;

    /// <summary>True when every hook the settle needs is in place; the left-behind pop-up is optional.</summary>
    public static bool Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        var isHooked = Hooks.TryPatchScope(harmony, typeof(CookingSettle), "a finished cook",
                           () => AccessTools.Method(typeof(Furniture), nameof(Furniture.SettleCookingResult)), log, nameof(OpenSettle), nameof(CloseSettle))
                       && Hooks.TryPatch(harmony, typeof(CookingSettle), "the recipes a cook used",
                           () => AccessTools.Method(typeof(Furniture), nameof(Furniture.BuildMatchedEntriesFromRecipeIds)), log, postfix: nameof(OpenLedger))
                       && Hooks.TryPatch(harmony, typeof(CookingSettle), "what a used item leaves behind",
                           () => AccessTools.Method(typeof(ItemManager), nameof(ItemManager.ResolveConsumeReturnItems)), log, postfix: nameof(RememberReturnCall))
                       && Hooks.TryPatch(harmony, typeof(CookingSettle), "removing an item",
                           () => AccessTools.Method(typeof(ItemManager), nameof(ItemManager.RemoveItem), new[] { typeof(long) }), log, prefix: nameof(TakeUsedUnits));

        if (!isHooked) return false;

        Hooks.TryPatch(harmony, typeof(CookingSettle), "the pop-up listing what a cook left behind",
            () => AccessTools.Method(typeof(ItemManager), nameof(ItemManager.NotifyConsumeReturnPop)), log, prefix: nameof(ShowExtraReturns));

        return true;
    }

    private static void OpenSettle() => _settle = new Settle(Time.frameCount);

    private static void CloseSettle() => _settle = null;

    private static void OpenLedger(GameItems workbenchItems, MatchedRecipes __result)
    {
        var settle = Current();

        if (settle == null || workbenchItems == null || __result == null) return;

        try
        {
            settle.Ledger = SettleLedger.Open(NeedPerRecipe(__result), BenchItemIds(workbenchItems));
        }
        catch (Exception ex)
        {
            Cooking.Stop("reading the recipes a cook used", ex);
        }
    }

    /// <summary>The game works out what a unit leaves behind just before removing it; the same call is repeated per extra unit.</summary>
    private static void RememberReturnCall(int itemConfigId, long ownerId, bool skipBurnableWaste)
    {
        if (_settle is { } settle) settle.LastReturnCall = new ReturnCall(itemConfigId, ownerId, skipBurnableWaste);
    }

    /// <summary>Runs before every removal in the game, so anything outside a settle returns at the first check.</summary>
    private static void TakeUsedUnits(ItemManager __instance, long itemInstanceId)
    {
        var settle = _settle == null ? null : Current();

        if (settle?.Ledger == null) return;

        var returnCall = settle.LastReturnCall;

        try
        {
            var item = __instance.GetItemData(itemInstanceId);
            var config = item == null ? null : GameSingleton.Existing<ConfigManager>()?.Get_Config_Item(item.ItemConfigId);

            if (item == null || config == null) return;

            var extra = settle.Ledger.ExtraUnits(itemInstanceId, item.ItemConfigId, item.ItemCount, Cooking.UseOf(config));

            if (extra <= 0) return;

            if (!__instance.DecreaseItemCount(item, extra)) throw new InvalidOperationException($"the game would not take {extra} units of item {item.ItemConfigId}");

            if (returnCall is { } call && call.ItemConfigId == item.ItemConfigId) settle.ExtraReturns.AddRange(ReturnsFor(__instance, call, extra));
        }
        catch (Exception ex)
        {
            Cooking.Stop("taking the units a cook used", ex);
        }
        finally
        {
            // Each removal pairs with the call just before it; this also drops the ones made for the extra units.
            settle.LastReturnCall = null;
        }
    }

    /// <summary>Only the pop-up: the items themselves were added to the bag when they were worked out.</summary>
    private static void ShowExtraReturns(GameReturns consumeReturns)
    {
        var settle = _settle;

        if (settle == null || settle.ExtraReturns.Count == 0 || consumeReturns == null) return;

        try
        {
            foreach (var returned in settle.ExtraReturns)
            {
                for (var i = 0; i < returned.Count; i++) consumeReturns.Add(returned[i]);
            }
        }
        catch (Exception ex)
        {
            _popUpFailures++;

            if (_popUpFailures <= MaxLoggedPopUpFailures) _log($"listing what a cook left behind failed, the items are still in the bag (logged {_popUpFailures} of {MaxLoggedPopUpFailures}): {ex}");
        }
        finally
        {
            settle.ExtraReturns.Clear();
        }
    }

    /// <summary>Without a finalizer a cook that throws leaves its settle open; a frame later it is known to be stale.</summary>
    private static Settle? Current()
    {
        var settle = _settle;

        if (settle == null || settle.Frame == Time.frameCount) return settle;

        _settle = null;

        return null;
    }

    private static List<IReadOnlyDictionary<int, int>> NeedPerRecipe(MatchedRecipes matched)
    {
        var need = new List<IReadOnlyDictionary<int, int>>(matched.Count);

        for (var i = 0; i < matched.Count; i++)
        {
            var participated = matched[i]?.ParticipatedCounts;

            if (participated != null) need.Add(Copy(participated));
        }

        return need;
    }

    private static Dictionary<int, int> Copy(GameTally tally)
    {
        var copy = new Dictionary<int, int>(tally.Count);

        foreach (var pair in tally) copy[pair.Key] = pair.Value;

        return copy;
    }

    private static List<long> BenchItemIds(GameItems items)
    {
        var ids = new List<long>(items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is { } item) ids.Add(item.InstanceId);
        }

        return ids;
    }

    private static List<GameReturns> ReturnsFor(ItemManager items, ReturnCall call, int units)
    {
        var returns = new List<GameReturns>();

        for (var i = 0; i < units; i++)
        {
            var returned = items.ResolveConsumeReturnItems(call.ItemConfigId, call.OwnerId, call.SkipBurnableWaste);

            if (returned != null && returned.Count > 0) returns.Add(returned);
        }

        return returns;
    }

    private readonly record struct ReturnCall(int ItemConfigId, long OwnerId, bool SkipBurnableWaste);

    private sealed class Settle
    {
        public Settle(int frame) => Frame = frame;

        public int Frame { get; }
        public SettleLedger? Ledger { get; set; }
        public ReturnCall? LastReturnCall { get; set; }
        public List<GameReturns> ExtraReturns { get; } = new();
    }
}
