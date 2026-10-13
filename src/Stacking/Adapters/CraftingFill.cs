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

namespace Stacking.Adapters;

/// <summary>
/// The crafting table fills a recipe only with whole items that fit what is still needed, so with nothing but stacks
/// at hand a recipe reads as short of materials. Its material checks are asked again over units, and its fill takes
/// single units off stacks onto the bench, each by the station rule's own move (see StationMover), before the game
/// moves the rest of its picks there itself. Static because Harmony patches are.
/// </summary>
internal static class CraftingFill
{
    private const int MaxLoggedFailures = 3;
    private const int FailureCountEvery = 100;

    private static StationMover? _mover;
    private static Action<string> _log = _ => { };
    private static Action<string> _logFileOnly = _ => { };
    private static int _failures;

    /// <summary>The material checks are only hooked once the fill is, so a recipe never reads as ready that its fill cannot take.</summary>
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

        var hooked = new[]
        {
            TryPatch(harmony, "the crafting table's material check across linked storage", nameof(Reducer_Web_ToolTable.CheckMaterialSufficiencyAcrossLinked), nameof(CountUnitsAcrossLinked)),
            TryPatch(harmony, "the crafting table's material check", nameof(Reducer_Web_ToolTable.CheckMaterialSufficiency), nameof(CountUnitsInBag)),
        };

        if (hooked.All(isHooked => isHooked)) log("the crafting table counts every item in a stack, and its fill takes single items off stacks");
        else log("the crafting table's fill takes single items off stacks, but some recipes may still show as short of materials: see the lines above");
    }

    private static bool TryPatch(Harmony harmony, string what, string method, string postfix) =>
        Hooks.TryPatch(harmony, typeof(CraftingFill), what, () => AccessTools.Method(typeof(Reducer_Web_ToolTable), method), _log, postfix: postfix);

    /// <summary>The recipe list's check, over the bench and every storage linked to the table.</summary>
    private static void CountUnitsAcrossLinked(State_Web_ToolTable state, State_Data_Item itemState, GameNeeds materialNeeded, ref bool __result)
    {
        if (__result) return;

        try
        {
            if (BenchOf(state) is not { } bench) return;

            var owners = new List<long> { bench };

            owners.AddRange(Managed(Reducer_Web_ToolTable.GetAllLinkedOwnerIds(state)));

            __result = CanFill(itemState, materialNeeded, owners, bench);
        }
        catch (Exception ex)
        {
            Fail($"counting a recipe's materials by unit failed: {ex}");
        }
    }

    /// <summary>The same check over the bench and the bag only.</summary>
    private static void CountUnitsInBag(State_Web_ToolTable state, State_Data_Item itemState, GameNeeds materialNeeded, ref bool __result)
    {
        if (__result) return;

        try
        {
            if (BenchOf(state) is not { } bench || state.BagOwnerId?.Value is not { } bag) return;

            __result = CanFill(itemState, materialNeeded, new List<long> { bench, bag }, bench);
        }
        catch (Exception ex)
        {
            Fail($"counting a recipe's materials by unit failed: {ex}");
        }
    }

    /// <summary>
    /// The fill button and batch crafting pick here and then move the picks onto the bench. When the game found nothing,
    /// or picked a stack the station rule would cut to one unit on the way, units are taken off stacks here and now and
    /// the pick is made again around them; the game's move then finds them on the bench already and moves the rest.
    /// </summary>
    private static void TakeUnitsFromStacks(State_Web_ToolTable state, State_Data_Item itemState, GameNeeds materialNeeded, long leftOwnerId, ref GameIds? __result, ref GameSources? sourceOf)
    {
        if (!IsReady() || SplitScene.LiveItems() is not { } items) return;

        var onBench = new List<(long UnitId, long FromOwnerId)>();
        long bench = 0;

        try
        {
            if (BenchOf(state) is not { } benchId || itemState == null || materialNeeded == null) return;

            bench = benchId;
            var candidates = Candidates(itemState, Managed(Reducer_Web_ToolTable.GetLinkedOwnersInFillOrder(state, leftOwnerId)), bench);

            if (!NeedsUnits(__result, candidates) || CraftingPick.Plan(Needs(materialNeeded), candidates) is not PickPlan.Filled plan) return;

            var whole = new List<WholePick>(plan.Whole);

            foreach (var take in plan.Units)
            {
                if (TakeUnits(items, take, bench, onBench, whole)) continue;

                PutBack(items, bench, onBench);
                Fail($"a single unit of stack {take.StackId} did not land on crafting bench {bench}, so the units taken went back");

                return;
            }

            var picks = onBench.Concat(whole.Select(pick => (UnitId: pick.ItemId, FromOwnerId: pick.OwnerId))).ToList();
            var ids = new GameIds();

            foreach (var (id, _) in picks) ids.Add(id);

            // The game notes where each item it moves came from, so emptying the bench later sends it home; it moves none of these units itself.
            foreach (var (unitId, fromOwnerId) in onBench) Reducer_Web_ToolTable.RecordBenchOrigin(state, unitId, fromOwnerId);

            // The game made this map itself and reads it after this returns, so it is refilled in place.
            sourceOf ??= new GameSources();

            sourceOf.Clear();

            foreach (var (id, owner) in picks) sourceOf.Add(id, owner);

            __result = ids;

            _logFileOnly($"a recipe's fill took {onBench.Count} single units off stacks onto crafting bench {bench}");
        }
        catch (Exception ex)
        {
            PutBack(items, bench, onBench);
            Fail($"taking single units off stacks for a recipe failed: {ex}");
        }
    }

    private static bool IsReady() => _mover != null && StationGuard.IsInstalled && SplitScene.LiveItems() != null;

    private static bool CanFill(State_Data_Item itemState, GameNeeds materialNeeded, IReadOnlyList<long> owners, long bench) =>
        IsReady() && itemState != null && materialNeeded != null
        && CraftingPick.Plan(Needs(materialNeeded), Candidates(itemState, owners, bench)) is PickPlan.Filled;

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
    /// Moves the take's units onto the bench one at a time, each off the stack the last one left behind. A stack down to
    /// its last unit goes in whole with the game's picks instead. False when a unit did not land; any unit that reached
    /// the bench is noted for putting back.
    /// </summary>
    private static bool TakeUnits(ItemManager items, UnitTake take, long bench, List<(long UnitId, long FromOwnerId)> onBench, List<WholePick> whole)
    {
        var stackId = take.StackId;

        for (var unit = 1; unit <= take.Units; unit++)
        {
            if (_mover == null || items.GetItemData(stackId) is not { } stack || stack.OwnerId != take.OwnerId) return false;

            if (stack.ItemCount <= 1)
            {
                whole.Add(new WholePick(stackId, take.OwnerId));

                return unit == take.Units;
            }

            var report = _mover.TakeUnitNow(items, new StationWork.TakeOne(stackId, take.OwnerId, bench, StationPath.Quick, default, IsSilent: true, AvoidsMold: false));
            var isOnBench = IsOn(items, stackId, bench);

            if (isOnBench) onBench.Add((stackId, take.OwnerId));

            if (!isOnBench || report.Outcome != StationOutcome.OneUnitIn || report.RestId is not { } rest) return false;

            stackId = rest;
        }

        return true;
    }

    /// <summary>Units this fill put on the bench go back to where they came from, so a fill that failed leaves the containers as it found them.</summary>
    private static void PutBack(ItemManager items, long bench, IReadOnlyList<(long UnitId, long FromOwnerId)> onBench)
    {
        foreach (var (unitId, fromOwnerId) in onBench)
        {
            try
            {
                if (IsOn(items, unitId, bench)) items.TryQuickMoveItem(unitId, bench, fromOwnerId, true, false);
            }
            catch (Exception ex)
            {
                _log($"a unit (item {unitId}) taken onto crafting bench {bench} could not go back to container {fromOwnerId}, so it stays on the bench: {ex}");
            }
        }
    }

    /// <summary>Every item in the containers, in fill order; within a container the largest first, as the game sorts them.</summary>
    private static List<PickCandidate> Candidates(State_Data_Item itemState, IEnumerable<long> owners, long bench)
    {
        var candidates = new List<PickCandidate>();
        var seen = new HashSet<long>();
        var cache = itemState.OwnerCache;

        if (cache == null) return candidates;

        foreach (var owner in owners)
        {
            if (owner == 0 || !seen.Add(owner) || !cache.TryGetValue(owner, out var held) || held == null) continue;

            var inOwner = new List<PickCandidate>();

            foreach (var item in held.Values)
            {
                // The cache can keep stale entries whose owner moved on; the game's moves would refuse those.
                if (item == null || item.OwnerId != owner) continue;

                inOwner.Add(new PickCandidate(item.LogicId, owner, item.ItemConfigId, item.ItemCount, IsCut: owner != bench && IsCut(item)));
            }

            candidates.AddRange(inOwner.OrderByDescending(candidate => candidate.Count));
        }

        return candidates;
    }

    private static bool IsCut(Data_Item item) =>
        StationRule.UnitsToSendBack(item.ItemCount, StackGuard.NewlyStackable.Contains(item.ItemConfigId), item.InstanceWeight != 0) > 0;

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

    private static bool IsOn(ItemManager items, long itemId, long ownerId) => items.GetItemData(itemId) is { } item && item.OwnerId == ownerId;

    private static void Fail(string what)
    {
        _failures++;

        if (_failures <= MaxLoggedFailures) _log($"{what}; the game's own result stands (logged {_failures} of {MaxLoggedFailures})");
        else if (_failures % FailureCountEvery == 0) _log($"{_failures - MaxLoggedFailures} more failures since the first {MaxLoggedFailures}, the game's own result stood each time; the latest: {what}");
    }
}
