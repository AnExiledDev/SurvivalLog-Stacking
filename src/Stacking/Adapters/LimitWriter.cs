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

    public LimitWriter(IReadOnlyDictionary<int, int> wantedByCategory, bool canGuardFreshness, Action<string> log, Action<string> logFileOnly)
    {
        _wantedByCategory = wantedByCategory;
        _canGuardFreshness = canGuardFreshness;
        _log = log;
        _logFileOnly = logFileOnly;
    }

    public void Sweep()
    {
        var table = BaseSingleton<ConfigManager>.IsInstanceCreated ? BaseSingleton<ConfigManager>.Instance._Config_Item_Dict : null;

        if (table == null || table.Count == 0) return;

        var items = new List<Config_Item>(table.Count);

        foreach (var item in table.Values)
        {
            if (item != null) items.Add(item);
        }

        var rows = items.Select(GameRow).ToList();
        var plan = StackLimits.Plan(rows, _wantedByCategory, _canGuardFreshness);
        var written = items.Count(item => Write(item, plan));

        StackGuard.NewlyStackable = plan.NewlyStackable;
        Report(rows, plan, written);
    }

    private ItemRow GameRow(Config_Item item)
    {
        if (!_gameLimits.TryGetValue(item.ID, out var gameLimit)) _gameLimits[item.ID] = gameLimit = item.StackLimit;

        return new ItemRow(item.ID, item.Category, gameLimit, item.UseTimes);
    }

    private static bool Write(Config_Item item, StackPlan plan)
    {
        if (!plan.Limits.TryGetValue(item.ID, out var limit) || item.StackLimit == limit) return false;

        item.StackLimit = limit;

        return true;
    }

    private void Report(List<ItemRow> rows, StackPlan plan, int written)
    {
        if (_hasApplied)
        {
            if (written > 0) _log($"the game reloaded its item table, {written} stack limits written again");

            return;
        }

        _hasApplied = true;
        _log($"stack limits written for {written} items, {plan.NewlyStackable.Count} of which the game never stacked");

        foreach (var line in StackLimits.Describe(rows, plan, CategoryName)) _logFileOnly(line);
    }

    private static string CategoryName(int category) =>
        Enum.IsDefined(typeof(ItemCategory), category) ? ((ItemCategory)category).ToString() : $"category {category}";
}
