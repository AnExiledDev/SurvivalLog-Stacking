using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using Stacking.Core;
using GameIds = Il2CppSystem.Collections.Generic.List<long>;
using GameNeeds = Il2CppSystem.Collections.Generic.Dictionary<int, int>;
using GameSources = Il2CppSystem.Collections.Generic.Dictionary<long, long>;
using OwnerCache = Il2CppSystem.Collections.Generic.Dictionary<long, Il2CppSystem.Collections.Generic.Dictionary<long, GameCore.HotUpdate.ReduxUI.Data_Item>>;

namespace Stacking.Adapters;

/// <summary>
/// The crafting table fills a recipe only with whole items that fit what is still needed, so with nothing but stacks
/// at hand a recipe reads as short of materials. Its recipe list's material check is asked again over units, and its
/// fill takes single units off stacks onto the bench (see CraftingUnits), before the game moves the rest of its picks
/// there itself. Static because Harmony patches are.
/// </summary>
internal static class CraftingFill
{
    private const int MaxLoggedFailures = 3;
    private const int FailureCountEvery = 100;

    private static StationMover? _mover;
    private static Action<string> _log = _ => { };
    private static Action<string> _logFileOnly = _ => { };
    private static int _failures;
    private static bool _isRebuildingList;
    private static State_Web_ToolTable? _listState;
    private static State_Data_Item? _listItems;

    /// <summary>
    /// What the bench and its linked storages hold, read once per recipe list rebuild. The list checks every recipe
    /// against the same storages, and reading them through the game is the slow part. These and the buffers below
    /// are reused, and only touched on the game's main thread.
    /// </summary>
    private static readonly CraftingShelves Shelves = new();

    private static readonly HashSet<long> OwnersRead = new();
    private static readonly List<KeyValuePair<int, int>> NeedsRead = new();

    /// <summary>The material check is only hooked once the fill is, so a recipe never reads as ready that its fill cannot take.</summary>
    public static void Apply(Harmony harmony, StationMover mover, Action<string> log, Action<string> logFileOnly)
    {
        _mover = mover;
        _log = log;
        _logFileOnly = logFileOnly;

        if (!TryPatch(harmony, "the crafting table's fill", nameof(Reducer_Web_ToolTable.SelectItemsForRecipeLinked), nameof(TakeUnitsFromStacks)))
        {
            log("the crafting table fills recipes with whole items only this session, so a stack fills no recipe that needs fewer than it holds");

            return;
        }

        // The recipe list asks the pool of everything the bench and its storages hold whether it fits each recipe
        // exactly; the pool knows neither the table nor its storages, so the list rebuild hands them over.
        var isListScoped = Hooks.TryPatchScope(harmony, typeof(CraftingFill), "the crafting table's recipe list",
            () => AccessTools.Method(typeof(Reducer_Web_ToolTable), nameof(Reducer_Web_ToolTable.RefreshRecipeList)), log, nameof(OpenRebuild), nameof(CloseRebuild));

        if (isListScoped && Hooks.TryPatch(harmony, typeof(CraftingFill), "the crafting table's material check",
                () => AccessTools.Method(typeof(Reducer_Web_ToolTable.MaterialPoolIndex), nameof(Reducer_Web_ToolTable.MaterialPoolIndex.CanExactFill)), log, postfix: nameof(CountPooledUnits)))
        {
            log("the crafting table counts every item in a stack, and its fill takes single items off stacks");
        }
        else
        {
            log("the crafting table's fill takes single items off stacks, but its recipe list may still show a recipe as short of materials: see the line above");
        }
    }

    private static bool TryPatch(Harmony harmony, string what, string method, string postfix) =>
        Hooks.TryPatch(harmony, typeof(CraftingFill), what, () => AccessTools.Method(typeof(Reducer_Web_ToolTable), method), _log, postfix: postfix);

    /// <summary>
    /// The pool's check during a recipe list rebuild, asked again over units for the table being rebuilt, over the
    /// bench and every storage linked to the table. It turns only a no into a yes, and only when the fill would step
    /// in: when no stack at hand is one the rule cuts, or the recipe needs none of them, it returns before reading the
    /// recipe, and nothing is made for any recipe.
    /// </summary>
    private static void CountPooledUnits(GameNeeds materialNeeded, ref bool __result)
    {
        if (__result || !_isRebuildingList || _listState is not { } state || _listItems is not { } itemState || materialNeeded == null) return;

        try
        {
            if (!ShelvesFor(state, itemState).HasAnyCut || !NeedsAnyCut(materialNeeded)) return;

            __result = Shelves.CanFill(ReadNeeds(materialNeeded));
        }
        catch (Exception ex)
        {
            // A read cut short holds only part of the shelves, so the next recipe reads them again, as before.
            Shelves.Forget();
            Fail($"counting a recipe's materials by unit failed: {ex}");
        }
    }

    private static void OpenRebuild(State_Web_ToolTable state, State_Data_Item itemState)
    {
        _isRebuildingList = true;
        _listState = state;
        _listItems = itemState;
        Shelves.Forget();
    }

    private static void CloseRebuild()
    {
        _isRebuildingList = false;
        _listState = null;
        _listItems = null;
        Shelves.Forget();
    }

    /// <summary>
    /// Storages do not change while the list rebuilds, so one read serves every recipe in it. With no item made
    /// stackable, no table open or no bench, nothing is cut, so nothing is read and every check returns at once.
    /// </summary>
    private static CraftingShelves ShelvesFor(State_Web_ToolTable state, State_Data_Item itemState)
    {
        if (Shelves.IsBuiltFor(state.Pointer, itemState.Pointer)) return Shelves;

        Shelves.Start(state.Pointer, itemState.Pointer);

        // The bench holds one unit a slot only while the table's screen is open; with it closed nothing is cut and the game's own check stands.
        if (StackGuard.NewlyStackable.Count == 0 || Live() is not { } items || BenchOf(state) is not { } bench || !Stations.IsStation(items, bench)) return Shelves;

        if (itemState._OwnerCache_k__BackingField is not { } cache) return Shelves;

        OwnersRead.Clear();
        Shelve(cache, bench, bench);

        var linked = Reducer_Web_ToolTable.GetAllLinkedOwnerIds(state);

        for (var i = 0; linked != null && i < linked.Count; i++) Shelve(cache, linked[i], bench);

        return Shelves;
    }

    /// <summary>
    /// One container's items onto the shelves, in the cache's own order, read straight from the items' fields. Only an
    /// item made stackable has its weight read, since only those can be cut.
    /// </summary>
    private static void Shelve(OwnerCache cache, long owner, long bench)
    {
        if (owner == 0 || !OwnersRead.Add(owner) || !cache.TryGetValue(owner, out var held) || held == null) return;

        var newlyStackable = StackGuard.NewlyStackable;

        foreach (var item in held.Values)
        {
            // The cache can keep stale entries whose owner moved on; the game's moves would refuse those.
            if (item == null || item._OwnerId_k__BackingField != owner) continue;

            var configId = item._ItemConfigId_k__BackingField;
            var count = item._ItemCount_k__BackingField;

            Shelves.Add(configId, count, isCut: owner != bench && newlyStackable.Contains(configId) && StationRule.UnitsToSendBack(count, true, item._InstanceWeight_k__BackingField != 0) > 0);
        }

        Shelves.EndOwner();
    }

    /// <summary>Whether the recipe needs an item a cut stack holds; asked item by item, so a recipe that needs none is never read whole.</summary>
    private static bool NeedsAnyCut(GameNeeds materialNeeded)
    {
        var cut = Shelves.CutConfigs;

        for (var i = 0; i < cut.Count; i++)
        {
            if (materialNeeded.ContainsKey(cut[i])) return true;
        }

        return false;
    }

    private static List<KeyValuePair<int, int>> ReadNeeds(GameNeeds materialNeeded)
    {
        NeedsRead.Clear();

        foreach (var pair in materialNeeded) NeedsRead.Add(new KeyValuePair<int, int>(pair.Key, pair.Value));

        return NeedsRead;
    }

    /// <summary>
    /// The fill button and batch crafting pick here and then move the picks onto the bench. When the game found nothing,
    /// or picked a stack the station rule would cut to one unit on the way, units are taken off stacks here and now and
    /// the pick is made again around them; the game's move then finds them on the bench already and moves the rest.
    /// Every move redraws the table's recipe list, so the redraws are held and at most one goes out at the end; none
    /// when a pick is returned, since the game's fill redraws the list itself once it has moved its picks.
    /// </summary>
    private static void TakeUnitsFromStacks(State_Web_ToolTable state, State_Data_Item itemState, GameNeeds materialNeeded, long leftOwnerId, ref GameIds? __result, ref GameSources? sourceOf)
    {
        ScreenRefresh.Hold();

        try
        {
            FillWithUnits(state, itemState, materialNeeded, leftOwnerId, ref __result, ref sourceOf);
        }
        finally
        {
            ScreenRefresh.Release(isToolTableSentNext: __result != null);
        }
    }

    private static void FillWithUnits(State_Web_ToolTable state, State_Data_Item itemState, GameNeeds materialNeeded, long leftOwnerId, ref GameIds? __result, ref GameSources? sourceOf)
    {
        var landed = new List<BenchUnit>();
        ItemManager? items = null;
        long bench = 0;

        try
        {
            if (_mover is not { } mover || Live() is not { } live || BenchOf(state) is not { } benchId || itemState == null || materialNeeded == null) return;

            items = live;
            bench = benchId;
            var needs = Needs(materialNeeded);
            var candidates = Candidates(live, itemState, needs, Managed(Reducer_Web_ToolTable.GetLinkedOwnersInFillOrder(state, leftOwnerId)), benchId);

            if (!NeedsUnits(__result, candidates) || CraftingPick.Plan(needs, candidates) is not PickPlan.Filled plan) return;

            if (plan.Units.Count > 0) ClearBench(state, itemState, benchId, leftOwnerId, plan.Whole);

            switch (TakeUnits(mover, live, benchId, plan.Units))
            {
                case UnitsTaken.PutBack putBack:
                    ReportPutBack(putBack, benchId);

                    return;
                case UnitsTaken.Landed taken:
                    landed.AddRange(taken.OnBench);
                    _logFileOnly($"a recipe's fill took {taken.OnBench.Count} single units off stacks onto crafting bench {benchId}");
                    Answer(state, taken.OnBench, plan.Whole.Concat(taken.Whole), ref __result, ref sourceOf);

                    return;
                default:
                    throw new InvalidOperationException("every way of taking units needs handling");
            }
        }
        catch (Exception ex)
        {
            var why = $"taking single units off stacks for a recipe failed: {ex}";

            if (items != null && landed.Count > 0) ReportPutBack(CraftingUnits.PutBack(Find(items), bench, landed, MoveOff(items, bench), why), bench);
            else Fail(why);
        }
    }

    private static ItemManager? Live() => StationGuard.IsInstalled ? SplitScene.LiveItems() : null;

    /// <summary>The game's pick stands unless it found nothing, or took a stack the rule cuts, which would reach the bench as one unit.</summary>
    private static bool NeedsUnits(GameIds? picked, IReadOnlyList<PickCandidate> candidates)
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

    /// <summary>
    /// The game sends items a recipe does not use off the bench only as it moves its picks there, after the pick, so
    /// items left from an earlier recipe would hold the cells the units need. They go home first, by the game's own
    /// call; the bench items the plan uses stay.
    /// </summary>
    private static void ClearBench(State_Web_ToolTable state, State_Data_Item itemState, long bench, long leftOwnerId, IEnumerable<WholePick> whole)
    {
        var kept = whole.Where(pick => pick.OwnerId == bench).Select(pick => pick.ItemId).ToHashSet();
        var others = new GameIds();

        foreach (var item in HeldBy(itemState, bench))
        {
            var itemId = item._LogicId_k__BackingField;

            if (item._OwnerId_k__BackingField == bench && !kept.Contains(itemId)) others.Add(itemId);
        }

        if (others.Count > 0) Reducer_Web_ToolTable.ReturnBenchItemsToOrigins(state, itemState, others, bench, leftOwnerId, null);
    }

    private static UnitsTaken TakeUnits(StationMover mover, ItemManager items, long bench, IReadOnlyList<UnitTake> takes) =>
        CraftingUnits.Take(
            Find(items),
            takes,
            bench,
            (stackId, ownerId) => mover.TakeUnitNow(items, new StationWork.TakeOne(stackId, ownerId, bench, StationPath.Quick, default, IsSilent: true, AvoidsMold: false)),
            MoveOff(items, bench));

    private static Func<long, ItemLookup> Find(ItemManager items) => itemId => GameStationStore.Find(items, itemId);

    private static Func<long, long, bool> MoveOff(ItemManager items, long bench) => (unitId, toOwnerId) => items.TryQuickMoveItem(unitId, bench, toOwnerId, true, false);

    /// <summary>
    /// The pick made again around the units. The game made the source map itself and reads it after this returns, so
    /// everything is built first and the map and the result are set last, back to back, so a failure leaves both as
    /// the game made them.
    /// </summary>
    private static void Answer(State_Web_ToolTable state, IReadOnlyList<BenchUnit> onBench, IEnumerable<WholePick> whole, ref GameIds? result, ref GameSources? sourceOf)
    {
        var picks = onBench.Select(unit => (Id: unit.UnitId, OwnerId: unit.FromOwnerId)).Concat(whole.Select(pick => (Id: pick.ItemId, pick.OwnerId))).ToList();
        var ids = new GameIds();
        var sources = new GameSources();

        foreach (var (id, ownerId) in picks)
        {
            ids.Add(id);
            sources.Add(id, ownerId);
        }

        // The game notes where each item it moves came from, so emptying the bench later sends it home; it moves none of these units itself.
        foreach (var unit in onBench) Reducer_Web_ToolTable.RecordBenchOrigin(state, unit.UnitId, unit.FromOwnerId);

        if (sourceOf == null)
        {
            sourceOf = sources;
        }
        else
        {
            sourceOf.Clear();

            foreach (var (id, ownerId) in picks) sourceOf.Add(id, ownerId);
        }

        result = ids;
    }

    /// <summary>Every needed item in the containers, in fill order; within a container the largest first, as the game sorts them.</summary>
    private static List<PickCandidate> Candidates(ItemManager items, State_Data_Item itemState, IReadOnlyDictionary<int, int> needs, IEnumerable<long> owners, long bench) =>
        Needed(Held(items, itemState, owners, bench, needs.ContainsKey), needs);

    private static List<PickCandidate> Needed(List<List<PickCandidate>> held, IReadOnlyDictionary<int, int> needs)
    {
        var candidates = new List<PickCandidate>();

        foreach (var inOwner in held) candidates.AddRange(inOwner.Where(candidate => needs.ContainsKey(candidate.ConfigId)).OrderByDescending(candidate => candidate.Count));

        return candidates;
    }

    /// <summary>The items of each container once, in fill order and in the cache's own order within a container.</summary>
    private static List<List<PickCandidate>> Held(ItemManager items, State_Data_Item itemState, IEnumerable<long> owners, long bench, Predicate<int> isWanted)
    {
        // The bench holds one unit a slot only while the table's screen is open; with it closed nothing is cut and the game's own pick stands.
        var canCut = Stations.IsStation(items, bench);
        var held = new List<List<PickCandidate>>();
        var seen = new HashSet<long>();

        foreach (var owner in owners)
        {
            if (!seen.Add(owner)) continue;

            var inOwner = new List<PickCandidate>();

            foreach (var item in HeldBy(itemState, owner))
            {
                var configId = item._ItemConfigId_k__BackingField;

                // The cache can keep stale entries whose owner moved on; the game's moves would refuse those.
                if (!isWanted(configId) || item._OwnerId_k__BackingField != owner) continue;

                var count = item._ItemCount_k__BackingField;

                inOwner.Add(new PickCandidate(item._LogicId_k__BackingField, owner, configId, count, IsCut: canCut && owner != bench && IsCut(count, configId, item._InstanceWeight_k__BackingField != 0)));
            }

            held.Add(inOwner);
        }

        return held;
    }

    private static IEnumerable<Data_Item> HeldBy(State_Data_Item itemState, long owner)
    {
        if (owner == 0 || itemState._OwnerCache_k__BackingField is not { } cache || !cache.TryGetValue(owner, out var held) || held == null) yield break;

        foreach (var item in held.Values)
        {
            if (item != null) yield return item;
        }
    }

    private static bool IsCut(int count, int configId, bool hasOwnWeight) =>
        StationRule.UnitsToSendBack(count, StackGuard.NewlyStackable.Contains(configId), hasOwnWeight) > 0;

    private static Dictionary<int, int> Needs(GameNeeds materialNeeded)
    {
        var needs = new Dictionary<int, int>(materialNeeded.Count);

        foreach (var pair in materialNeeded) needs[pair.Key] = pair.Value;

        return needs;
    }

    private static List<long> Managed(GameIds? ids)
    {
        var managed = new List<long>(ids?.Count ?? 0);

        for (var i = 0; ids != null && i < ids.Count; i++) managed.Add(ids[i]);

        return managed;
    }

    private static long? BenchOf(State_Web_ToolTable? state) => state?.WorkbenchOwnerId?.Value is { } bench && bench != 0 ? bench : null;

    /// <summary>A unit that could not go back is named every time, past the failure cap: it is a real item left on the bench.</summary>
    private static void ReportPutBack(UnitsTaken.PutBack putBack, long bench)
    {
        var stayed = putBack.StillOnBench;

        if (stayed.Count > 0) _log($"units {string.Join(", ", stayed.Select(unit => unit.UnitId))} could not go back from crafting bench {bench} to the containers they came from, so they stay on the bench");

        var back = stayed.Count == 0 ? "every unit it took went back" : $"{stayed.Count} units it took stayed on the bench";

        Fail($"a recipe's fill could not take single units off stacks onto crafting bench {bench}, and {back}: {string.Join("; ", putBack.Notes)}");
    }

    private static void Fail(string what)
    {
        _failures++;

        if (_failures <= MaxLoggedFailures) _log($"{what}; the game's own result stands (logged {_failures} of {MaxLoggedFailures})");
        else if (_failures % FailureCountEvery == 0) _log($"{_failures - MaxLoggedFailures} more failures since the first {MaxLoggedFailures}, the game's own result stood each time; the latest: {what}");
    }
}
