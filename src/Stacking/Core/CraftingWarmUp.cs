using System;
using System.Collections.Generic;
using System.Linq;

namespace Stacking.Core;

/// <summary>
/// The first recipe fill compiles its whole path (the pick, the units taken one at a time by the station steps, putting
/// them back, and the shelves the recipe list reads) on the game's frame. Run once on a worker at load, on made-up
/// items, this pays that cost before the first fill. Pure .NET only: nothing here touches the game.
/// </summary>
public static class CraftingWarmUp
{
    private const long Bench = 1;
    private const long Storage = 2;

    /// <summary>One fill that takes three units off a stack, putting them back, and one recipe list check; what the fill took is returned for tests.</summary>
    public static UnitsTaken Run()
    {
        var needs = new Dictionary<int, int> { [10] = 3, [11] = 1 };
        var held = new List<List<PickCandidate>>
        {
            new() { new PickCandidate(100, Bench, 11, 1, IsCut: false) },
            new() { new PickCandidate(101, Storage, 10, 5, IsCut: true), new PickCandidate(102, Storage, 12, 1, IsCut: false) },
        };
        var candidates = CraftingPick.Candidates(held, needs);

        CraftingPick.NeedsUnits(null, candidates);
        CraftingPick.NeedsUnits(new List<long> { 100 }, candidates);

        if (CraftingPick.Plan(needs, candidates) is not PickPlan.Filled plan) throw new InvalidOperationException("the made-up fill must fill");

        CraftingPick.KeptOnBench(plan.Whole, Bench);

        var store = new MadeUpStore().With(100, Bench, 1).With(101, Storage, 5).With(102, Storage, 1);
        var taken = CraftingUnits.Take(
            store.Find,
            plan.Units,
            Bench,
            (stackId, ownerId) => StationMoves.TakeOne(store, stackId, ownerId, Bench, () => store.MoveTo(stackId, Bench, default)),
            store.MoveBack);

        if (taken is UnitsTaken.Landed landed)
        {
            CraftingPick.Picks(landed.OnBench, plan.Whole.Concat(landed.Whole));
            CraftingUnits.PutBack(store.Find, Bench, landed.OnBench, store.MoveBack, "the made-up fill is undone");
        }

        CheckShelves(needs);
        StationRule.UnitsToSendBack(5, isNewlyStackable: true, hasOwnWeight: false);

        return taken;
    }

    private static void CheckShelves(Dictionary<int, int> needs)
    {
        var shelves = new CraftingShelves();

        shelves.Start((nint)Bench, (nint)Storage);
        shelves.Add(11, 1, isCut: false);
        shelves.EndOwner();
        shelves.Add(10, 5, isCut: true);
        shelves.Add(12, 1, isCut: false);
        shelves.EndOwner();
        shelves.CanFill(needs.ToList());
        shelves.IsCut(10);
        shelves.IsBuiltFor((nint)Bench, (nint)Storage);
        shelves.Forget();
    }

    /// <summary>Items as a plain table that takes every step as asked; one container is the bench.</summary>
    private sealed class MadeUpStore : IStationStore
    {
        private readonly Dictionary<long, Placed> _items = new();
        private long _nextId = 1000;

        public MadeUpStore With(long itemId, long ownerId, int count)
        {
            _items[itemId] = new Placed(ownerId, default, count);

            return this;
        }

        public ItemLookup Find(long itemId) => _items.TryGetValue(itemId, out var item) ? new ItemLookup.Found(item) : new ItemLookup.Gone();

        public bool IsStation(long ownerId) => ownerId == Bench;

        public IReadOnlyDictionary<long, int> CountsIn(long ownerId)
        {
            var counts = new Dictionary<long, int>();

            foreach (var (itemId, item) in _items)
            {
                if (item.OwnerId == ownerId) counts[itemId] = item.Count;
            }

            return counts;
        }

        public void SetCount(long itemId, int count) => _items[itemId] = _items[itemId] with { Count = count };

        public void MoveTo(long itemId, long ownerId, Cell at) => _items[itemId] = _items[itemId] with { OwnerId = ownerId, At = at };

        public bool MoveBack(long unitId, long ownerId)
        {
            MoveTo(unitId, ownerId, default);

            return true;
        }

        public long? AddStack(long ownerId, Cell at, int units)
        {
            var stackId = _nextId++;

            _items[stackId] = new Placed(ownerId, at, units);

            return stackId;
        }

        public IReadOnlyDictionary<long, int> CountsOnGround() => new Dictionary<long, int>();

        public void CopyFreshness(long stackId)
        {
        }

        public void Remove(long itemId) => _items.Remove(itemId);

        public Cell? FreeCell(long ownerId, long itemId, Cell near) => near;
    }
}
