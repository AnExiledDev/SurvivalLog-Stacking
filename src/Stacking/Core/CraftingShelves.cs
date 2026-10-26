using System;
using System.Collections.Generic;

namespace Stacking.Core;

/// <summary>
/// What a crafting table's bench and its linked storages hold, kept the way the recipe list's material check needs it:
/// for each item, the counts of the items that go in whole, in fill order, and the units on stacks the station rule
/// cuts. The list checks every recipe against the same storages, so they are read once per list rebuild and every
/// recipe is answered from here without reading the game again or making anything new.
/// </summary>
/// <remarks>
/// Answers what <see cref="CraftingPick.Plan"/> answers over the same items, without making the plan: whole items are
/// taken greedily per item in fill order, and the item is short only when the cut stacks hold fewer units than are
/// left. Each item's need is met on its own, so the order of the needs makes no difference.
/// </remarks>
public sealed class CraftingShelves
{
    private static readonly IComparer<int> LargestFirst = Comparer<int>.Create((a, b) => b.CompareTo(a));

    private readonly Dictionary<int, Kind> _kinds = new();
    private readonly List<int> _cutConfigs = new();
    private readonly List<Kind> _inOwner = new();
    private nint _state;
    private nint _items;
    private bool _isBuilt;

    /// <summary>How many times the shelves were read from the game; a rebuild that reads them twice shows here.</summary>
    public int Builds { get; private set; }

    /// <summary>Whether any stack held is one the rule cuts. Without one, no recipe check changes, so nothing more is read.</summary>
    public bool HasAnyCut => _cutConfigs.Count > 0;

    /// <summary>The items that have at least one stack the rule cuts, each once.</summary>
    public IReadOnlyList<int> CutConfigs => _cutConfigs;

    /// <summary>
    /// True when the shelves were read for this table and these items during the rebuild that is running. A loaded
    /// save brings new items, and a new rebuild may follow moves, so either reads the shelves again.
    /// </summary>
    public bool IsBuiltFor(nint state, nint items) => _isBuilt && _state == state && _items == items;

    /// <summary>Drops what was read; called as each list rebuild starts and ends.</summary>
    public void Forget() => _isBuilt = false;

    /// <summary>Starts a new read for this table and these items. The lists already made are kept and refilled.</summary>
    public void Start(nint state, nint items)
    {
        foreach (var kind in _kinds.Values)
        {
            kind.Whole.Clear();
            kind.CutUnits = 0;
            kind.HasCut = false;
            kind.OwnerStart = -1;
        }

        _cutConfigs.Clear();
        _inOwner.Clear();
        _state = state;
        _items = items;
        _isBuilt = true;
        Builds++;
    }

    /// <summary>One item in the container being read. Containers come in fill order, each one closed with <see cref="EndOwner"/>.</summary>
    /// <param name="count">Units it holds; the game counts an item with no count as one.</param>
    /// <param name="isCut">The station rule lets only one unit of it onto the bench at a time.</param>
    public void Add(int configId, int count, bool isCut)
    {
        if (!_kinds.TryGetValue(configId, out var kind))
        {
            kind = new Kind();
            _kinds[configId] = kind;
        }

        if (isCut)
        {
            kind.CutUnits += Math.Max(count, 1);

            if (!kind.HasCut)
            {
                kind.HasCut = true;
                _cutConfigs.Add(configId);
            }

            return;
        }

        if (kind.OwnerStart < 0)
        {
            kind.OwnerStart = kind.Whole.Count;
            _inOwner.Add(kind);
        }

        kind.Whole.Add(Math.Max(count, 1));
    }

    /// <summary>Within a container the game takes the largest items first, so each item's counts from this container are put in that order.</summary>
    public void EndOwner()
    {
        foreach (var kind in _inOwner)
        {
            kind.Whole.Sort(kind.OwnerStart, kind.Whole.Count - kind.OwnerStart, LargestFirst);
            kind.OwnerStart = -1;
        }

        _inOwner.Clear();
    }

    /// <summary>Whether a need of this item has a stack the rule cuts to give units, which is when the check is asked again.</summary>
    public bool IsCut(int configId) => _kinds.TryGetValue(configId, out var kind) && kind.HasCut;

    /// <summary>
    /// Whether a recipe needing these units, by item, fills once units come off the stacks the rule cuts, and needs at
    /// least one of those stacks: what the fill's plan would say, without making it.
    /// </summary>
    public bool CanFill(IReadOnlyList<KeyValuePair<int, int>> needs)
    {
        var usesCut = false;

        for (var i = 0; i < needs.Count; i++)
        {
            var (configId, needed) = needs[i];

            if (!_kinds.TryGetValue(configId, out var kind))
            {
                if (needed > 0) return false;

                continue;
            }

            usesCut |= kind.HasCut;

            if (kind.CutUnits < LeftAfterWhole(kind.Whole, needed)) return false;
        }

        return usesCut;
    }

    /// <summary>Units still needed after every item that goes in whole and fits is taken, as the fill takes them.</summary>
    private static int LeftAfterWhole(List<int> whole, int needed)
    {
        var left = needed;

        for (var i = 0; i < whole.Count && left > 0; i++)
        {
            if (whole[i] <= left) left -= whole[i];
        }

        return left;
    }

    private sealed class Kind
    {
        public readonly List<int> Whole = new();
        public long CutUnits;
        public bool HasCut;
        public int OwnerStart = -1;
    }
}
