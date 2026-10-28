using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
    private static readonly List<RawItems.Item> HeldRead = new();

    /// <summary>The item cache read from memory; each read is used up before the next call into the game.</summary>
    private static readonly RawItems Raw = new(() => _log);

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

        WarmUpInBackground();

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

    /// <summary>
    /// The first fill would otherwise compile the fill, the station moves it makes and the storage reads on the game's
    /// frame. The plain part runs once on made-up items and the rest is compiled without being run, on a worker at
    /// load, so nothing here touches the game. A failure only costs the first fill its head start, and is noted in the
    /// mod's file only: the BepInEx console is left to the game's thread.
    /// </summary>
    private static void WarmUpInBackground() =>
        Task.Run(() =>
        {
            try
            {
                CraftingWarmUp.Run();

                foreach (var type in Warmed) Prepare(type);
            }
            catch (Exception ex)
            {
                _logFileOnly("warm-up failed, the first recipe fill may take a moment longer: " + ex.Message);
            }
        });

    private static readonly Type[] Warmed =
    {
        typeof(CraftingFill), typeof(RawItems), typeof(RawDictionary), typeof(StationMover), typeof(GameStationStore), typeof(StationGuard),
        typeof(ScreenRefresh), typeof(StackCopy), typeof(StackCounts), typeof(Stations), typeof(GroundDrops), typeof(BagCells),
    };

    /// <summary>Compiles every method the type declares, its lambdas included, without running any.</summary>
    private static void Prepare(Type type)
    {
        const BindingFlags declared = BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var method in type.GetMethods(declared))
        {
            if (!method.IsAbstract && !method.ContainsGenericParameters) RuntimeHelpers.PrepareMethod(method.MethodHandle);
        }

        foreach (var nested in type.GetNestedTypes(declared))
        {
            if (!nested.ContainsGenericParameters) Prepare(nested);
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

        // The linked storages come from the game before the cache is opened, so the memory read is used up before the next game call.
        var linked = Managed(Reducer_Web_ToolTable.GetAllLinkedOwnerIds(state));
        var isRaw = Raw.TryOpen(cache);

        OwnersRead.Clear();
        Shelve(cache, isRaw, bench, bench);

        foreach (var owner in linked) Shelve(cache, isRaw, owner, bench);

        return Shelves;
    }

    /// <summary>One container's items onto the shelves, in the cache's own order.</summary>
    private static void Shelve(OwnerCache cache, bool isRaw, long owner, long bench)
    {
        if (owner == 0 || !OwnersRead.Add(owner)) return;

        var newlyStackable = StackGuard.NewlyStackable;

        foreach (var item in ReadHeld(cache, isRaw, owner))
        {
            // The cache can keep stale entries whose owner moved on; the game's moves would refuse those.
            if (item.OwnerId != owner) continue;

            Shelves.Add(item.ConfigId, item.Count, isCut: owner != bench && newlyStackable.Contains(item.ConfigId) && StationRule.UnitsToSendBack(item.Count, true, item.HasOwnWeight) > 0);
        }

        Shelves.EndOwner();
    }

    /// <summary>
    /// Every item the cache files under the owner, ones whose owner moved on included, in the cache's own order, into
    /// one buffer every read reuses: from memory when the cache was opened for this read, through the game's objects
    /// otherwise.
    /// </summary>
    private static List<RawItems.Item> ReadHeld(OwnerCache cache, bool isRaw, long owner)
    {
        HeldRead.Clear();

        if (owner == 0 || (isRaw && Raw.TryAddHeld(owner, HeldRead))) return HeldRead;

        // A memory read given up part way leaves part of the container behind.
        HeldRead.Clear();

        if (!cache.TryGetValue(owner, out var held) || held == null) return HeldRead;

        foreach (var item in held.Values)
        {
            if (item != null) HeldRead.Add(new RawItems.Item(item._LogicId_k__BackingField, item._OwnerId_k__BackingField, item._ItemCount_k__BackingField, item._ItemConfigId_k__BackingField, item._InstanceWeight_k__BackingField != 0));
        }

        return HeldRead;
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

            if (!CraftingPick.NeedsUnits(__result == null ? null : Managed(__result), candidates) || CraftingPick.Plan(needs, candidates) is not PickPlan.Filled plan) return;

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

    /// <summary>
    /// The game sends items a recipe does not use off the bench only as it moves its picks there, after the pick, so
    /// items left from an earlier recipe would hold the cells the units need. They go home first, by the game's own
    /// call; the bench items the plan uses stay.
    /// </summary>
    private static void ClearBench(State_Web_ToolTable state, State_Data_Item itemState, long bench, long leftOwnerId, IEnumerable<WholePick> whole)
    {
        if (itemState._OwnerCache_k__BackingField is not { } cache) return;

        var kept = CraftingPick.KeptOnBench(whole, bench);
        var othersRead = new List<long>();

        foreach (var item in ReadHeld(cache, Raw.TryOpen(cache), bench))
        {
            if (item.OwnerId == bench && !kept.Contains(item.LogicId)) othersRead.Add(item.LogicId);
        }

        // Built only once the memory read is used up, since each add is a call into the game.
        var others = new GameIds();

        foreach (var itemId in othersRead) others.Add(itemId);

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
        var picks = CraftingPick.Picks(onBench, whole);
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
        CraftingPick.Candidates(Held(items, itemState, owners, bench, needs), needs);

    /// <summary>The needed items of each container once, in fill order and in the cache's own order within a container.</summary>
    private static List<List<PickCandidate>> Held(ItemManager items, State_Data_Item itemState, IEnumerable<long> owners, long bench, IReadOnlyDictionary<int, int> needs)
    {
        var held = new List<List<PickCandidate>>();

        if (itemState._OwnerCache_k__BackingField is not { } cache) return held;

        // The bench holds one unit a slot only while the table's screen is open; with it closed nothing is cut and the game's own pick stands.
        var canCut = Stations.IsStation(items, bench);
        var seen = new HashSet<long>();
        var isRaw = Raw.TryOpen(cache);

        foreach (var owner in owners)
        {
            if (!seen.Add(owner)) continue;

            var inOwner = new List<PickCandidate>();

            foreach (var item in ReadHeld(cache, isRaw, owner))
            {
                // The cache can keep stale entries whose owner moved on; the game's moves would refuse those.
                if (!needs.ContainsKey(item.ConfigId) || item.OwnerId != owner) continue;

                inOwner.Add(new PickCandidate(item.LogicId, owner, item.ConfigId, item.Count, IsCut: canCut && owner != bench && IsCut(item.Count, item.ConfigId, item.HasOwnWeight)));
            }

            held.Add(inOwner);
        }

        return held;
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
