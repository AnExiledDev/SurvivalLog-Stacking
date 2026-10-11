using System;
using System.Collections.Generic;

namespace Stacking.Core;

/// <summary>What the game's item table says about how an item is used up.</summary>
public readonly record struct ItemUse(int StackLimit, int Category, int ConfigUses);

/// <summary>One stack on a cooking bench: its item, its ingredient tag and how many units it holds.</summary>
public readonly record struct BenchStack(int ConfigId, int SubCategory, int Count, ItemUse Use);

/// <summary>Units the cooking planner should count on top of the one it counts per stack, by item and by ingredient tag.</summary>
public sealed record BenchExtras(IReadOnlyDictionary<int, int> ByItem, IReadOnlyDictionary<int, int> ByTag)
{
    public bool IsEmpty => ByItem.Count == 0 && ByTag.Count == 0;
}

/// <summary>
/// The game's cooking planner counts one ingredient per stack, so three strawberries in a stack make one smoothie
/// where three loose ones make three. These are the units it leaves out.
/// </summary>
public static class CookingCounts
{
    private const int ElectricalAppliances = 4;

    public static BenchExtras Extras(IEnumerable<BenchStack> bench)
    {
        var byItem = new Dictionary<int, int>();
        var byTag = new Dictionary<int, int>();

        foreach (var stack in bench)
        {
            var extra = CountsEveryUnit(stack.Use) ? Math.Max(1, stack.Count) - 1 : 0;

            if (extra <= 0) continue;

            byItem[stack.ConfigId] = byItem.GetValueOrDefault(stack.ConfigId) + extra;
            byTag[stack.SubCategory] = byTag.GetValueOrDefault(stack.SubCategory) + extra;
        }

        return new BenchExtras(byItem, byTag);
    }

    /// <summary>
    /// True when cooking uses up every unit of a stack. An appliance with uses is the exception: the game's removal
    /// leaves it in place, so counting its units would cook more than the bench holds.
    /// </summary>
    public static bool CountsEveryUnit(ItemUse use) => use.StackLimit > 1 && !(use.Category == ElectricalAppliances && use.ConfigUses > 0);

    /// <summary>How much of a stack the game counts against the recipes' need: all of it when stackable, else the one item.</summary>
    internal static int CountedUnits(int count, ItemUse use) => use.StackLimit > 1 ? Math.Max(1, count) : 1;
}

/// <summary>
/// What a finished cook still owes, per item. The game counts a whole stack against what the recipes need but removes
/// one unit from it; this walks the same need in the same order and returns the units the recipes used beyond that one.
/// </summary>
public sealed class SettleLedger
{
    private readonly Dictionary<int, int> _needByItem;
    private readonly HashSet<long> _benchItemIds;
    private readonly HashSet<long> _settledItemIds = new();

    private SettleLedger(Dictionary<int, int> needByItem, HashSet<long> benchItemIds)
    {
        _needByItem = needByItem;
        _benchItemIds = benchItemIds;
    }

    /// <summary>The need is what every accepted recipe took part with, added up; only items on the bench may be settled.</summary>
    public static SettleLedger Open(IEnumerable<IReadOnlyDictionary<int, int>> needPerRecipe, IEnumerable<long> benchItemIds)
    {
        var needByItem = new Dictionary<int, int>();

        foreach (var recipe in needPerRecipe)
        {
            foreach (var (configId, units) in recipe) needByItem[configId] = needByItem.GetValueOrDefault(configId) + units;
        }

        return new SettleLedger(needByItem, new HashSet<long>(benchItemIds));
    }

    /// <summary>
    /// Units to take from a stack on top of the one the game takes as it removes it. The game lowers the need by the
    /// whole stack before removing, so the units used are the smaller of the stack and the need left.
    /// </summary>
    public int ExtraUnits(long itemId, int configId, int count, ItemUse use)
    {
        if (!_benchItemIds.Contains(itemId) || !_settledItemIds.Add(itemId)) return 0;

        var need = _needByItem.GetValueOrDefault(configId);

        if (need <= 0) return 0;

        var counted = CookingCounts.CountedUnits(count, use);

        _needByItem[configId] = Math.Max(0, need - counted);

        return CookingCounts.CountsEveryUnit(use) ? Math.Min(counted, need) - 1 : 0;
    }
}
