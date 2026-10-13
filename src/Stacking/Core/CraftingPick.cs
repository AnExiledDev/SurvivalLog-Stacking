using System;
using System.Collections.Generic;

namespace Stacking.Core;

/// <summary>An item a recipe could take, as the crafting table's fill sees it.</summary>
/// <param name="Count">Units it holds; the game counts an item with no count as one.</param>
/// <param name="IsCut">The station rule lets only one unit of it onto the bench at a time, so it gives single units.</param>
public readonly record struct PickCandidate(long ItemId, long OwnerId, int ConfigId, int Count, bool IsCut);

/// <summary>An item that goes onto the bench whole, from the container it is in.</summary>
public readonly record struct WholePick(long ItemId, long OwnerId);

/// <summary>Single units taken off a stack, one at a time, from the container it is in.</summary>
public readonly record struct UnitTake(long StackId, long OwnerId, int Units);

/// <summary>What a recipe's fill takes, or that the materials do not cover it.</summary>
public abstract record PickPlan
{
    private PickPlan()
    {
    }

    public sealed record Filled(IReadOnlyList<WholePick> Whole, IReadOnlyList<UnitTake> Units) : PickPlan;

    /// <summary>The units at hand do not cover every material, so nothing is taken.</summary>
    public sealed record Short : PickPlan;
}

/// <summary>
/// The game fills a recipe only with whole items that fit what is still needed, so a stack of eight glass never fills
/// a need of two. A stack the station rule cuts gives its units one at a time instead. Whole items that fit are taken
/// first, as the game takes them, and units come off stacks only for what is left, so a fill moves as few items as it
/// can. Containers are used in the fill order given.
/// </summary>
public static class CraftingPick
{
    /// <param name="needs">Units needed, by item.</param>
    /// <param name="candidates">Items that could fill them, in fill order.</param>
    public static PickPlan Plan(IReadOnlyDictionary<int, int> needs, IReadOnlyList<PickCandidate> candidates)
    {
        var whole = new List<WholePick>();
        var units = new List<UnitTake>();

        foreach (var (configId, needed) in needs)
        {
            var left = TakeWhole(configId, needed, candidates, whole);

            if (TakeUnits(configId, left, candidates, units) > 0) return new PickPlan.Short();
        }

        return new PickPlan.Filled(whole, units);
    }

    /// <summary>Units still needed after every item that goes in whole and fits is taken.</summary>
    private static int TakeWhole(int configId, int needed, IReadOnlyList<PickCandidate> candidates, List<WholePick> picks)
    {
        var left = needed;

        foreach (var item in candidates)
        {
            if (left <= 0) break;

            if (item.ConfigId != configId || item.IsCut || UnitsOf(item) > left) continue;

            picks.Add(new WholePick(item.ItemId, item.OwnerId));
            left -= UnitsOf(item);
        }

        return left;
    }

    /// <summary>Units still needed after single units are taken off the stacks the rule cuts.</summary>
    private static int TakeUnits(int configId, int needed, IReadOnlyList<PickCandidate> candidates, List<UnitTake> takes)
    {
        var left = needed;

        foreach (var stack in candidates)
        {
            if (left <= 0) break;

            if (stack.ConfigId != configId || !stack.IsCut) continue;

            var units = Math.Min(UnitsOf(stack), left);

            takes.Add(new UnitTake(stack.ItemId, stack.OwnerId, units));
            left -= units;
        }

        return left;
    }

    private static int UnitsOf(PickCandidate item) => Math.Max(item.Count, 1);
}
