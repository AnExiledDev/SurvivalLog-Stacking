using System;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>Every needed item in the containers, in fill order; within a container the largest first, as the game sorts them, a tie kept in the cache's order.</summary>
    /// <param name="held">Each container's items in fill order, each in the cache's own order.</param>
    public static List<PickCandidate> Candidates(List<List<PickCandidate>> held, IReadOnlyDictionary<int, int> needs)
    {
        var candidates = new List<PickCandidate>();

        foreach (var inOwner in held) candidates.AddRange(inOwner.Where(candidate => needs.ContainsKey(candidate.ConfigId)).OrderByDescending(candidate => candidate.Count));

        return candidates;
    }

    /// <summary>The game's pick stands unless it found nothing, or took a stack the rule cuts, which would reach the bench as one unit.</summary>
    /// <param name="picked">The items the game picked; null when it found nothing.</param>
    public static bool NeedsUnits(IReadOnlyList<long>? picked, IReadOnlyList<PickCandidate> candidates)
    {
        var cut = candidates.Where(candidate => candidate.IsCut).Select(candidate => candidate.ItemId).ToHashSet();

        if (cut.Count == 0) return false;

        if (picked == null) return true;

        for (var i = 0; i < picked.Count; i++)
        {
            if (cut.Contains(picked[i])) return true;
        }

        return false;
    }

    /// <summary>The items already on the bench that the plan uses, which stay there while the bench is cleared.</summary>
    public static HashSet<long> KeptOnBench(IEnumerable<WholePick> whole, long bench) =>
        whole.Where(pick => pick.OwnerId == bench).Select(pick => pick.ItemId).ToHashSet();

    /// <summary>The pick made again around the units: the units on the bench first, then the whole items, each with the container it came from.</summary>
    public static List<(long Id, long OwnerId)> Picks(IReadOnlyList<BenchUnit> onBench, IEnumerable<WholePick> whole) =>
        onBench.Select(unit => (Id: unit.UnitId, OwnerId: unit.FromOwnerId)).Concat(whole.Select(pick => (Id: pick.ItemId, pick.OwnerId))).ToList();

    private static int UnitsOf(PickCandidate item) => Math.Max(item.Count, 1);
}
