using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;
using Stacking.Core;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>The game's move a stack was stopped on, so one unit can be sent the same way.</summary>
internal enum StationPath
{
    Move,
    Quick,
    CookingDrag,
    CookingQuick,
}

/// <summary>Work the station rule leaves for the ticker, which carries it out on the next frame.</summary>
internal abstract record StationWork
{
    private StationWork()
    {
    }

    /// <summary>A stack the game was about to move into a station whole.</summary>
    public sealed record TakeOne(long ItemId, long FromOwnerId, long ToOwnerId, StationPath Path, Cell BagPos, bool IsSilent, bool AvoidsMold) : StationWork;

    /// <summary>Stacks a move put in a station whole, each with what it was, and where it stood, before it moved.</summary>
    public sealed record SendBack(long FromOwnerId, long StationOwnerId, IReadOnlyList<(long ItemId, StackCopy Before)> Stacks) : StationWork;
}

/// <summary>
/// Keeps stacks out of cooking and crafting stations at the game's own move points, so a move made by the player, by
/// the game's fill buttons or by another mod is held to the rule alike: a move of one item into a station is stopped
/// and replayed with one unit, a whole-bag move is put right after it, and a station never tops up a stack. Any other
/// move that lands a stack in a station is caught where every move ends, as the item enters the station's list, and
/// put right after it. The work is done next frame by the ticker, never inside the game's handler, except for the
/// quick move, whose callers must learn at once whether the item moved. Static because Harmony patches are.
/// </summary>
internal static class StationGuard
{
    private const int MaxLoggedFailures = 3;
    private const int FailureCountEvery = 100;

    private static readonly ConcurrentQueue<StationWork> Work = new();

    /// <summary>Stacks of items this mod made stackable that just left a container, by id, so one that lands in a station can be sent back. Cleared every frame.</summary>
    private static readonly Dictionary<long, (long OwnerId, StackCopy Before)> Leaving = new();

    private static StationMover? _mover;
    private static Action<string> _log = _ => { };
    private static Action<string> _logFileOnly = _ => { };
    private static int _failures;

    /// <summary>True once any move into a station is held to one unit; a split into a station is then left to that move.</summary>
    public static bool IsInstalled { get; private set; }

    /// <summary>
    /// False when no move into a station could be caught, so stacks go in whole, as in 0.2.0. Needs NewStacks: the
    /// units kept out of a station must start a stack of their own, never top up another.
    /// </summary>
    public static bool Apply(Harmony harmony, StationMover mover, Action<string> log, Action<string> logFileOnly)
    {
        _mover = mover;
        _log = log;
        _logFileOnly = logFileOnly;

        var hooked = new[]
        {
            TryPatch(harmony, "moving an item", nameof(ItemManager.OnMoveItem), prefix: nameof(OneUnitOnMove)),
            TryPatch(harmony, "quick-moving an item", nameof(ItemManager.TryQuickMoveItem), prefix: nameof(OneUnitOnQuickMove)),
            TryPatch(harmony, "dragging an item in a cooking view", nameof(ItemManager.OnCookingDragMove), prefix: nameof(OneUnitOnCookingDrag)),
            TryPatch(harmony, "quick-moving an item in a cooking view", nameof(ItemManager.OnCookingQuickMove), prefix: nameof(OneUnitOnCookingQuick)),
            TryPatch(harmony, "swapping two items", nameof(ItemManager.OnSwapItem), prefix: nameof(OneUnitOnSwap)),
            TryPatch(harmony, "moving a whole bag", nameof(ItemManager.ExecuteMoveBag), prefix: nameof(RememberStacksMoving), postfix: nameof(SendStacksBack)),
            TryPatch(harmony, "topping up a stack", nameof(ItemManager.FindStackableItem), postfix: nameof(NoStackInStation)),
            TryPatch(harmony, "noting an item leaving a container", nameof(ItemManager.RemoveItemFromOwnerCache), prefix: nameof(NoteLeaving)),
            TryPatch(harmony, "noting an item entering a container", nameof(ItemManager.AddItemToOwnerCache), postfix: nameof(SendBackIfStackLanded)),
        };

        if (!hooked.Any(isHooked => isHooked))
        {
            log("stacks can go into cooking and crafting stations whole this session: no move into one could be caught");

            return false;
        }

        if (!hooked.All(isHooked => isHooked)) log("some moves into a station could not be caught and still put a whole stack in: see the lines above");
        else log("a stack moved into a cooking or crafting station puts in one unit, and a station never holds a stack");

        IsInstalled = true;

        return true;
    }

    public static bool TryTake(out StationWork? work) => Work.TryDequeue(out work);

    /// <summary>Once per frame: a stack that left a container and landed nowhere by now was used up or dropped.</summary>
    public static void ForgetLeaving()
    {
        lock (Leaving) Leaving.Clear();
    }

    private static bool TryPatch(Harmony harmony, string what, string method, string? prefix = null, string? postfix = null) =>
        Hooks.TryPatch(harmony, typeof(StationGuard), what, () => AccessTools.Method(typeof(ItemManager), method), _log, prefix, postfix);

    private static bool OneUnitOnMove(ItemManager __instance, long fromOwnerId, long itemId, long toOwnerId, Vector2Int bagPos) =>
        !TookOneUnit(__instance, new StationWork.TakeOne(itemId, fromOwnerId, toOwnerId, StationPath.Move, CellOf(bagPos), IsSilent: false, AvoidsMold: false));

    private static bool OneUnitOnCookingDrag(ItemManager __instance, long itemId, long fromOwnerId, long toOwnerId, Vector2Int bagPos) =>
        !TookOneUnit(__instance, new StationWork.TakeOne(itemId, fromOwnerId, toOwnerId, StationPath.CookingDrag, CellOf(bagPos), IsSilent: false, AvoidsMold: false));

    private static bool OneUnitOnCookingQuick(ItemManager __instance, long itemId, long fromOwnerId, long toOwnerId) =>
        !TookOneUnit(__instance, new StationWork.TakeOne(itemId, fromOwnerId, toOwnerId, StationPath.CookingQuick, default, IsSilent: false, AvoidsMold: false));

    /// <summary>
    /// Callers read the result as whether the item moved and act on it at once (a fill tries another container on
    /// false), so the one unit is moved here and now, and the result says what really happened.
    /// </summary>
    private static bool OneUnitOnQuickMove(ItemManager __instance, long itemId, long fromOwnerId, long toOwnerId, bool silent, bool avoidMold, ref bool __result)
    {
        var move = new StationWork.TakeOne(itemId, fromOwnerId, toOwnerId, StationPath.Quick, default, silent, avoidMold);

        if (_mover == null || !IsOneUnit(__instance, move) || _mover.TakeOneNow(__instance, move) is not { } moved) return true;

        __result = moved;

        return false;
    }

    /// <summary>
    /// A drop on an item in another bag trades the two places. A stack dropped on a station item goes in as one
    /// quick-moved unit, and the station item stays; a station item dropped on a stack outside would bring the stack
    /// in whole, so that drop does nothing.
    /// </summary>
    private static bool OneUnitOnSwap(ItemManager __instance, long dragId, long targetId)
    {
        try
        {
            if (!SplitScene.IsLive(__instance)) return true;

            var dragged = __instance.GetItemData(dragId);
            var target = __instance.GetItemData(targetId);

            if (dragged == null || target == null) return true;

            var (draggedFrom, targetFrom) = (dragged.OwnerId, target.OwnerId);

            if (EntryOf(__instance, dragged, draggedFrom, targetFrom) == StationEntry.OneUnit)
            {
                Work.Enqueue(new StationWork.TakeOne(dragId, draggedFrom, targetFrom, StationPath.Quick, default, IsSilent: false, AvoidsMold: false));

                return false;
            }

            if (EntryOf(__instance, target, targetFrom, draggedFrom) != StationEntry.OneUnit) return true;

            _logFileOnly($"a station item was not traded for a stack of {target.ItemCount} (item {target.ItemConfigId}), which would have gone into the station whole");

            return false;
        }
        catch (Exception ex)
        {
            Fail("checking a drop on an item", ex);

            return true;
        }
    }

    /// <summary>A whole-bag move keeps each item it carries whole, so the stacks are noted before it and put right after it.</summary>
    private static void RememberStacksMoving(ItemManager __instance, long fromOwnerId, long toOwnerId, out Dictionary<long, StackCopy>? __state)
    {
        __state = null;

        try
        {
            if (!SplitScene.IsLive(__instance) || fromOwnerId == toOwnerId || !Stations.IsStation(__instance, toOwnerId)) return;

            var inBag = __instance.GetItemDataList(fromOwnerId);
            var stacks = new Dictionary<long, StackCopy>();

            for (var i = 0; inBag != null && i < inBag.Count; i++)
            {
                var item = inBag[i];

                if (item != null && UnitsToSendBack(item) > 0) stacks[item.InstanceId] = StackCopy.Of(item);
            }

            __state = stacks;
        }
        catch (Exception ex)
        {
            Fail("noting the stacks a whole-bag move carries", ex);
        }
    }

    private static void SendStacksBack(ItemManager __instance, long fromOwnerId, long toOwnerId, Dictionary<long, StackCopy>? __state)
    {
        if (__state is not { Count: > 0 }) return;

        try
        {
            var landed = __state
                .Where(stack => __instance.GetItemData(stack.Key) is { } item && item.OwnerId == toOwnerId)
                .Select(stack => (ItemId: stack.Key, Before: stack.Value))
                .ToList();

            if (landed.Count > 0) Work.Enqueue(new StationWork.SendBack(fromOwnerId, toOwnerId, landed));
        }
        catch (Exception ex)
        {
            Fail("finding the stacks a whole-bag move put in a station", ex);
        }
    }

    /// <summary>
    /// Every move ends by taking the item off its container's list and putting it on the new one's. A stack of an item
    /// this mod made stackable is noted as it leaves, so a move no hook above caught still has it sent back.
    /// </summary>
    private static void NoteLeaving(ItemManager __instance, long ownerId, long itemId)
    {
        try
        {
            if (!SplitScene.IsLive(__instance) || __instance.GetItemData(itemId) is not { } item || UnitsToSendBack(item) == 0) return;

            var before = StackCopy.Of(item);

            lock (Leaving) Leaving[itemId] = (ownerId, before);
        }
        catch (Exception ex)
        {
            Fail("noting a stack leaving a container", ex);
        }
    }

    /// <summary>
    /// A stack that left another container and landed in a station whole is put right next frame, the same way as a
    /// whole-bag move. A stack a hook above already sent back is skipped then, its count being one.
    /// </summary>
    private static void SendBackIfStackLanded(ItemManager __instance, long ownerId, ItemData item)
    {
        try
        {
            if (item == null || UnitsToSendBack(item) == 0 || !SplitScene.IsLive(__instance)) return;

            (long OwnerId, StackCopy Before) left;

            lock (Leaving)
            {
                if (!Leaving.Remove(item.InstanceId, out left)) return;
            }

            if (left.OwnerId == ownerId || !Stations.IsStation(__instance, ownerId)) return;

            Work.Enqueue(new StationWork.SendBack(left.OwnerId, ownerId, new[] { (item.InstanceId, left.Before) }));
            _logFileOnly($"a stack of {item.ItemCount} (item {item.ItemConfigId}) landed in station {ownerId} by a move no other hook caught; all but one unit go back");
        }
        catch (Exception ex)
        {
            Fail("checking a stack entering a container", ex);
        }
    }

    /// <summary>A unit added to a station starts its own slot, so two eggs put in one at a time take two slots.</summary>
    private static void NoStackInStation(ItemManager __instance, long ownerId, int itemConfigId, ref ItemData? __result)
    {
        if (__result == null || !StackGuard.NewlyStackable.Contains(itemConfigId)) return;

        try
        {
            if (Stations.IsStation(__instance, ownerId)) __result = null;
        }
        catch (Exception ex)
        {
            Fail("keeping a station from topping up a stack", ex);
        }
    }

    /// <summary>True when the move was stopped and one unit will be sent the same way next frame.</summary>
    private static bool TookOneUnit(ItemManager items, StationWork.TakeOne move)
    {
        if (!IsOneUnit(items, move)) return false;

        Work.Enqueue(move);

        return true;
    }

    /// <summary>True when the station rule holds this move to one unit.</summary>
    private static bool IsOneUnit(ItemManager items, StationWork.TakeOne move)
    {
        try
        {
            return SplitScene.IsLive(items) && items.GetItemData(move.ItemId) is { } item && EntryOf(items, item, move.FromOwnerId, move.ToOwnerId) == StationEntry.OneUnit;
        }
        catch (Exception ex)
        {
            Fail("checking a move into a station", ex);

            return false;
        }
    }

    private static StationEntry EntryOf(ItemManager items, ItemData item, long fromOwnerId, long toOwnerId) =>
        StationRule.Entry(Stations.IsStation(items, toOwnerId), fromOwnerId == toOwnerId, item.ItemCount,
            StackGuard.NewlyStackable.Contains(item.ItemConfigId), item.InstanceWeight != 0);

    internal static int UnitsToSendBack(ItemData item) =>
        StationRule.UnitsToSendBack(item.ItemCount, StackGuard.NewlyStackable.Contains(item.ItemConfigId), item.InstanceWeight != 0);

    private static Cell CellOf(Vector2Int bagPos) => new(bagPos.x, bagPos.y);

    private static void Fail(string what, Exception ex)
    {
        _failures++;

        if (_failures <= MaxLoggedFailures) _log($"{what} failed, the game's own move went ahead (logged {_failures} of {MaxLoggedFailures}): {ex}");
        else if (_failures % FailureCountEvery == 0) _log($"{_failures - MaxLoggedFailures} more failures since the first {MaxLoggedFailures}, the game's own move went ahead each time; the latest: {what}");
    }
}
