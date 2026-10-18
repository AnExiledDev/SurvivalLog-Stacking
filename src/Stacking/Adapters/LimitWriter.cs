using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.HotUpdate;
using Stacking.Core;

namespace Stacking.Adapters;

/// <summary>
/// Writes the chosen stack limits onto the game's item table. The table only exists once the game has loaded it,
/// and a reload would bring back the game's limits, so this runs again on every sweep and rewrites what changed.
/// </summary>
internal sealed class LimitWriter
{
    private readonly IReadOnlyDictionary<int, int> _wantedByCategory;
    private readonly bool _canGuardFreshness;
    private readonly Action<string> _log;
    private readonly Action<string> _logFileOnly;

    /// <summary>Each item's limit as the game first defined it, before this mod wrote over it.</summary>
    private readonly Dictionary<int, int> _gameLimits = new();

    private bool _hasApplied;
    private bool _hasReportedWaiting;

    public LimitWriter(IReadOnlyDictionary<int, int> wantedByCategory, bool canGuardFreshness, Action<string> log, Action<string> logFileOnly)
    {
        _wantedByCategory = wantedByCategory;
        _canGuardFreshness = canGuardFreshness;
        _log = log;
        _logFileOnly = logFileOnly;
    }

    public void Sweep()
    {
        var config = BaseSingleton<ConfigManager>.IsInstanceCreated ? BaseSingleton<ConfigManager>.Instance : null;
        var table = config?._Config_Item_Dict;

        if (config == null || table == null || table.Count == 0) return;

        var cookedItems = CookedItems(config);

        if (cookedItems == null)
        {
            ReportWaitingForRecipes();

            return;
        }

        var items = new List<Config_Item>(table.Count);

        foreach (var item in table.Values)
        {
            if (item != null) items.Add(item);
        }

        var rows = items.Select(item => GameRow(item, cookedItems)).ToList();
        var plan = StackLimits.Plan(rows, _wantedByCategory, _canGuardFreshness);
        var written = items.Count(item => Write(item, plan));

        StackGuard.NewlyStackable = plan.NewlyStackable;
        StackGuard.NeverSpoils = StackLimits.NeverSpoiling(rows);
        Report(rows, plan, written, StackGuard.NeverSpoils);
    }

    /// <summary>
    /// The dishes the recipe table can make, or null until the game has loaded it: writing before then would raise
    /// every dish, and a later sweep never lowers a limit it wrote.
    /// </summary>
    private static IReadOnlySet<int>? CookedItems(ConfigManager config)
    {
        var recipes = config._Config_CookingRecipe_Dict;

        if (recipes == null || recipes.Count == 0) return null;

        var outputs = new List<RecipeOutputs>(recipes.Count);

        foreach (var recipe in recipes.Values)
        {
            if (recipe != null) outputs.Add(new RecipeOutputs(recipe.PerfectItemID, recipe.GoodItemID, recipe.NormalItemID, recipe.FailItemID));
        }

        return StackLimits.CookedItems(outputs);
    }

    private ItemRow GameRow(Config_Item item, IReadOnlySet<int> cookedItems)
    {
        if (!_gameLimits.TryGetValue(item.ID, out var gameLimit)) _gameLimits[item.ID] = gameLimit = item.StackLimit;

        return new ItemRow(item.ID, item.Category, gameLimit, item.UseTimes, item.Life, cookedItems.Contains(item.ID), !item.CantUse);
    }

    private static bool Write(Config_Item item, StackPlan plan)
    {
        if (!plan.Limits.TryGetValue(item.ID, out var limit) || item.StackLimit == limit) return false;

        item.StackLimit = limit;

        return true;
    }

    private void ReportWaitingForRecipes()
    {
        if (_hasReportedWaiting) return;

        _hasReportedWaiting = true;
        _log("the item table is loaded but the cooking recipe table is not; stack limits wait for it, so no limit is raised until then");
    }

    private void Report(List<ItemRow> rows, StackPlan plan, int written, IReadOnlySet<int> neverSpoils)
    {
        if (_hasApplied)
        {
            if (written > 0) _log($"the game reloaded its item table, {written} stack limits written again");

            return;
        }

        _hasApplied = true;
        _log($"stack limits written for {written} items, {plan.NewlyStackable.Count} of which the game never stacked");
        _log($"{plan.NewlyStackable.Count(neverSpoils.Contains)} of the newly stackable items never spoil, so when they were picked up does not keep them apart");

        foreach (var line in StackLimits.Describe(rows, plan, CategoryName)) _logFileOnly(line);
    }

    private static string CategoryName(int category) =>
        Enum.IsDefined(typeof(ItemCategory), category) ? ((ItemCategory)category).ToString() : $"category {category}";
}
