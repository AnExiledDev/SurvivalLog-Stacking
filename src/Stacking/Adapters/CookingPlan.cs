using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using Stacking.Core;
using UnityEngine;
using GameTally = Il2CppSystem.Collections.Generic.Dictionary<int, int>;

namespace Stacking.Adapters;

/// <summary>
/// Makes the cooking planner count every unit of a stack. It tallies the bench one stack at a time, by item and by
/// ingredient tag, so three strawberries in one stack plan one smoothie where three loose ones plan three. While it
/// plans, both tallies get the missing units before it first reads them. The game checks the planned dishes against
/// the units on the bench again before it cooks them, so a tally that is too high cannot cook more than is there.
/// </summary>
internal static class CookingPlan
{
    [ThreadStatic] private static Plan? _plan;

    public static bool Apply(Harmony harmony, Action<string> log) =>
        Hooks.TryPatchScope(harmony, typeof(CookingPlan), "the recipe planner",
            () => AccessTools.Method(typeof(Reducer_Web_Cooking), nameof(Reducer_Web_Cooking.PreMatchRecipes)), log, nameof(OpenPlan), nameof(ClosePlan))
        && Hooks.TryPatch(harmony, typeof(CookingPlan), "the planner's item tally",
            () => AccessTools.Method(typeof(Reducer_Web_Cooking), nameof(Reducer_Web_Cooking.ResolvePredictionTier)), log, prefix: nameof(CountStackedItems))
        && Hooks.TryPatch(harmony, typeof(CookingPlan), "the planner's tag tally",
            () => AccessTools.Method(typeof(Reducer_Web_Cooking), nameof(Reducer_Web_Cooking.SelectBestMatchedRecipe)), log, prefix: nameof(CountStackedTags));

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

    /// <summary>The planner's first tier check is handed its item tally, before any recipe is picked from it.</summary>
    private static void CountStackedItems(GameTally participated)
    {
        var plan = _plan == null ? null : Current();

        if (plan == null || plan.ItemTally != IntPtr.Zero || participated == null) return;

        plan.ItemTally = participated.Pointer;
        AddTo(participated, plan.Extras.ByItem, "the planner's item tally");
    }

    /// <summary>The tag tally travels with the item tally into each recipe pick; it gets its units at the first one.</summary>
    private static void CountStackedTags(GameTally workbenchIds, GameTally workbenchTags)
    {
        var plan = _plan == null ? null : Current();

        if (plan == null || plan.HasCountedTags || workbenchIds == null || workbenchTags == null || workbenchIds.Pointer != plan.ItemTally) return;

        plan.HasCountedTags = true;
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

        /// <summary>The planner's item tally, found at its first use.</summary>
        public IntPtr ItemTally { get; set; }

        public bool HasCountedTags { get; set; }
    }
}
