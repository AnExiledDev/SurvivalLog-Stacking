using System;
using System.Collections.Generic;
using ExiledToolkit.Game;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;
using Stacking.Core;
using UnityEngine;
using GameTally = Il2CppSystem.Collections.Generic.Dictionary<int, int>;

namespace Stacking.Adapters;

/// <summary>
/// Makes the cooking planner count every unit of a stack. It tallies the bench one stack at a time, by item and by
/// ingredient tag, so three strawberries in one stack plan one smoothie where three loose ones plan three. While it
/// plans, every tally it reads gets the missing units before its first read: the bench's own and the copy it plans the
/// other dish mode from. The game checks the planned dishes against the units on the bench again before it cooks them,
/// so a tally that is too high cannot cook more than is there.
/// </summary>
internal static class CookingPlan
{
    [ThreadStatic] private static Plan? _plan;

    public static bool Apply(PatchKit hooks) =>
        hooks.PatchScope(typeof(CookingPlan), typeof(Reducer_Web_Cooking), nameof(Reducer_Web_Cooking.PreMatchRecipes), "the recipe planner", nameof(OpenPlan), nameof(ClosePlan))
        && hooks.Patch(typeof(CookingPlan), typeof(Reducer_Web_Cooking), nameof(Reducer_Web_Cooking.ResolvePredictionTier), prefix: nameof(CountStackedItems),
            consequence: "the planner's item tally")
        && hooks.Patch(typeof(CookingPlan), typeof(Reducer_Web_Cooking), nameof(Reducer_Web_Cooking.SelectBestMatchedRecipe), prefix: nameof(CountStackedTags),
            consequence: "the planner's tag tally");

    private static void OpenPlan(State_Web_Cooking state)
    {
        _plan = null;

        if (!Cooking.IsCountingStacks || state == null) return;

        try
        {
            var extras = CookingCounts.Extras(ReadBench(state));

            if (!extras.IsEmpty) _plan = new Plan(Time.frameCount, extras);
        }
        catch (Exception ex)
        {
            Cooking.Stop("reading the cooking bench", ex);
        }
    }

    private static void ClosePlan() => _plan = null;

    /// <summary>Each pass's first tier check is handed its item tally, before any recipe is picked from it.</summary>
    private static void CountStackedItems(GameTally participated)
    {
        var plan = _plan == null ? null : Current();

        if (plan == null || participated == null || !plan.Claims.ClaimItems(participated.Pointer.ToInt64())) return;

        AddTo(participated, plan.Extras.ByItem, "the planner's item tally");
    }

    /// <summary>A tag tally travels with its item tally into each recipe pick; it gets its units at the first one.</summary>
    private static void CountStackedTags(GameTally workbenchIds, GameTally workbenchTags)
    {
        var plan = _plan == null ? null : Current();

        if (plan == null || workbenchIds == null || workbenchTags == null) return;

        if (!plan.Claims.ClaimTags(workbenchIds.Pointer.ToInt64(), workbenchTags.Pointer.ToInt64())) return;

        AddTo(workbenchTags, plan.Extras.ByTag, "the planner's tag tally");
    }

    /// <summary>Only adds to entries the planner made itself, so an item or tag it chose to leave out stays out.</summary>
    private static void AddTo(GameTally tally, IReadOnlyDictionary<int, int> extras, string what)
    {
        try
        {
            foreach (var (key, units) in extras)
            {
                if (tally.ContainsKey(key)) tally[key] += units;
            }
        }
        catch (Exception ex)
        {
            Cooking.Stop($"adding to {what}", ex);
        }
    }

    private static Plan? Current()
    {
        var plan = _plan;

        if (plan == null || plan.Frame == Time.frameCount) return plan;

        _plan = null;

        return null;
    }

    private static List<BenchStack> ReadBench(State_Web_Cooking state)
    {
        var bench = new List<BenchStack>();
        var items = state.WorkbenchItems;
        var configs = GameSingleton.Existing<ConfigManager>();

        if (items == null || configs == null) return bench;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var configId = item?.ConfigId?.Value ?? 0;
            var config = configId == 0 ? null : configs.Get_Config_Item(configId);

            if (item == null || config == null) continue;

            bench.Add(new BenchStack(configId, item.SubCategory?.Value ?? config.SubCategory, item.Count?.Value ?? 1, Cooking.UseOf(config)));
        }

        return bench;
    }

    private sealed class Plan
    {
        public Plan(int frame, BenchExtras extras)
        {
            Frame = frame;
            Extras = extras;
        }

        public int Frame { get; }
        public BenchExtras Extras { get; }

        public TallyClaims Claims { get; } = new();
    }
}
