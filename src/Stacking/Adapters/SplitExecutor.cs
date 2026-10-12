using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using Stacking.Core;

namespace Stacking.Adapters;

/// <summary>
/// Carries out a split drop on the main thread, a frame after the game handed it over. Plans it again from the live
/// bags, runs the checks the game's own move would have run, then moves the units. Every unit is accounted for: if a
/// step fails, the stacks are put back as they were before anything is reported.
/// </summary>
internal sealed class SplitExecutor
{
    private const string TakeOutOnly = "Items can only be taken out of this box";
    private const string NotForThisBasket = "That can't go in this basket";
    private const string NoNewStacks = "Splitting onto a free spot is off this session: see BepInEx/stacking.log";
    private const string NoPicker = "Ctrl+Shift needs the amount picker, which could not start: see BepInEx/stacking.log";
    private const string Failed = "The split failed and nothing moved: see BepInEx/stacking.log";

    private readonly Action<string> _log;
    private readonly Action<string> _logFileOnly;
    private readonly SplitPicker _picker;
    private readonly bool _canStartStacks;
    private readonly bool _canAsk;

    public SplitExecutor(Action<string> log, Action<string> logFileOnly, SplitPicker picker, bool canStartStacks, bool canAsk)
    {
        _log = log;
        _logFileOnly = logFileOnly;
        _picker = picker;
        _canStartStacks = canStartStacks;
        _canAsk = canAsk;
    }

    public void Run(SplitRequest request, int? chosenUnits, float now)
    {
        var items = SplitScene.LiveItems();
        var source = items?.GetItemData(request.ItemId);

        if (items == null || source == null)
        {
            _log("the stack was gone before it could be split, so nothing moved");

            return;
        }

        var plan = SplitScene.Plan(items, source, request, chosenUnits, _log);

        switch (plan)
        {
            case SplitPlan.Vanilla:
                _log("the stack changed before the split, so nothing moved");
                break;
            case SplitPlan.Refused refused:
                if (request.IsRightClick) _logFileOnly($"a right-click split of {source.ItemCount} (item {source.ItemConfigId}) moved nothing: {Splitting.Explain(refused.Reason)}");

                _picker.Toast(Splitting.Explain(refused.Reason));
                break;
            case SplitPlan.Choose when !_canAsk:
                _picker.Toast(NoPicker);
                break;
            case SplitPlan.NewStack when !_canStartStacks:
                _picker.Toast(NoNewStacks);
                break;
            case SplitPlan.Choose choose:
                if (IsAllowed(request, source.ItemConfigId)) _picker.Ask(request, choose.Max, choose.Suggested, now);
                break;
            case SplitPlan.NewStack newStack:
                if (IsAllowed(request, source.ItemConfigId)) StartStack(items, source, request, newStack);
                break;
            case SplitPlan.JoinStack join:
                if (IsAllowed(request, source.ItemConfigId)) JoinStack(items, source, request, join);
                break;
            default:
                throw new InvalidOperationException("every split plan needs carrying out: " + plan);
        }
    }

    /// <summary>
    /// The checks the game's move runs once it knows the item may leave its bag. Each of the game's own shows the
    /// player why; the rest show a toast.
    /// </summary>
    private bool IsAllowed(SplitRequest request, int configId)
    {
        var (from, to) = (request.SrcOwnerId, request.DstOwnerId);

        if (from == to) return true;

        if (ItemManager.IsBagLockedBlocked(from, to, silent: false) || ItemManager.IsBagCategoryBlocked(to, configId, silent: false)) return false;

        if (ItemManager.IsBagCategoryRejected(to, configId))
        {
            ItemManager.NotifyBagCategoryHint();

            return false;
        }

        var refusal = ItemManager.IsNeighborBasketForbidden(to, configId) ? NotForThisBasket
            : VehicleConfig.IsBoxOwner(to) ? TakeOutOnly
            : null;

        if (refusal != null) _picker.Toast(refusal);

        return refusal == null;
    }

    /// <summary>The new stack is added first and the units taken second, so a failure part way leaves too many units, never too few, until it is undone.</summary>
    private void StartStack(ItemManager items, ItemData source, SplitRequest request, SplitPlan.NewStack plan)
    {
        var countBefore = source.ItemCount;
        var copy = StackCopy.Of(source);
        var created = AddNewStack(items, copy, request.DstOwnerId, plan);

        if (created == null)
        {
            items.SyncBothBags(request.SrcOwnerId, request.DstOwnerId);

            return;
        }

        try
        {
            copy.CopyFreshnessTo(created);
            items.DecreaseItemCount(source, plan.Units);
            StackCounts.Expect(source, countBefore - plan.Units);
        }
        catch (Exception ex)
        {
            Undo("starting a new stack", ex, () =>
            {
                StackCounts.Restore(items, source, countBefore);
                GroundDrops.RemoveStack(items, created.InstanceId);
            });
            items.SyncBothBags(request.SrcOwnerId, request.DstOwnerId);

            return;
        }

        var landing = Splitting.LandingOf(created.OwnerId, new Cell(created.BagPos.x, created.BagPos.y), request.DstOwnerId, plan.At);

        if (Splitting.Explain(landing) is { } whereItWent)
        {
            _log($"the new stack of {plan.Units} did not fit where it was dropped and the game put it elsewhere (owner {created.OwnerId}, cell {created.BagPos})");
            _picker.Toast(whereItWent);
        }

        items.SyncBothBags(request.SrcOwnerId, request.DstOwnerId);
        _logFileOnly($"split {plan.Units} of {countBefore} (item {source.ItemConfigId}) into a new stack at {plan.At.X},{plan.At.Y}");
    }

    /// <summary>
    /// The new stack, or null when there is none and the bag is as it was. The game's add can put the units in and
    /// then throw, or return nothing for units it placed, so unless it plainly placed them somewhere else (which the
    /// split reports), what it did is read off the bag and the ground (see AddedUnits), never off what it returned.
    /// </summary>
    private ItemData? AddNewStack(ItemManager items, StackCopy copy, long ownerId, SplitPlan.NewStack plan)
    {
        IReadOnlyDictionary<long, int> before;

        try
        {
            before = StackCounts.In(items, ownerId);
        }
        catch (Exception ex)
        {
            _picker.Toast(Failed);
            _log($"reading container {ownerId} before a split failed, so nothing moved: {ex}");

            return null;
        }

        var groundBefore = ReadGround("before");
        var (returned, thrown) = TryAdd(items, copy, ownerId, plan);

        if (thrown == null && returned != null && returned.OwnerId != ownerId) return returned;

        try
        {
            return Judge(items, before, StackCounts.In(items, ownerId), plan.Units, returned, thrown, groundBefore);
        }
        catch (Exception ex)
        {
            _picker.Toast(Failed);
            _log($"container {ownerId} could not be read or put right after {plan.Units} units were added to it; check this bag's counts. The add: {thrown?.ToString() ?? "returned"}. Then: {ex}");

            return null;
        }
    }

    private static (ItemData? Returned, Exception? Thrown) TryAdd(ItemManager items, StackCopy copy, long ownerId, SplitPlan.NewStack plan)
    {
        try
        {
            return (copy.AddNewStack(items, ownerId, plan.At, plan.Units), null);
        }
        catch (Exception ex)
        {
            return (null, ex);
        }
    }

    private ItemData? Judge(ItemManager items, IReadOnlyDictionary<long, int> before, IReadOnlyDictionary<long, int> after, int units, ItemData? returned, Exception? thrown,
        IReadOnlyDictionary<long, int>? groundBefore)
    {
        switch (AddedUnits.Judge(before, after, units))
        {
            case AddResult.Started started:
                if (thrown != null || returned?.InstanceId != started.StackId) _log($"the game's add of {units} units did not hand back the stack it made, but one new stack holds them, so the split goes on: {thrown?.ToString() ?? "it returned another item or none"}");

                return items.GetItemData(started.StackId) ?? throw new InvalidOperationException($"the new stack {started.StackId} could not be read");
            case AddResult.Undo { DidNothing: true } when thrown == null:
                _log("the game made no new stack, so nothing moved");

                return null;
            case AddResult.Undo { DidNothing: true }:
                return DroppedOnGround(items, groundBefore, units, thrown);
            case AddResult.Undo undo:
                Undo("starting a new stack", thrown ?? new InvalidOperationException($"the add put {units} units in no single new stack"), () => TakeBackAdd(items, undo));

                return null;
            default:
                throw new InvalidOperationException("every add needs judging: " + units);
        }
    }

    /// <summary>
    /// The add threw and put nothing in the bag, but a bag with no room has the game drop the units on the ground
    /// before anything can throw (DropItemManager.TryDropOverflowItemData). One new stack of exactly the units there is
    /// the split's, landed on the ground as a split that fits nowhere lands; with none, nothing moved.
    /// </summary>
    private ItemData? DroppedOnGround(ItemManager items, IReadOnlyDictionary<long, int>? groundBefore, int units, Exception? thrown)
    {
        var groundAfter = groundBefore == null ? null : ReadGround("after");
        var dropped = groundBefore == null || groundAfter == null ? null : AddedUnits.NewStacksOf(groundBefore, groundAfter, units);

        if (dropped is { Count: 1 })
        {
            _log($"the game's add of {units} units dropped them on the ground and then threw, so the split goes on with that stack: {thrown}");

            return items.GetItemData(dropped[0]) ?? throw new InvalidOperationException($"the stack of {units} dropped on the ground ({dropped[0]}) could not be read");
        }

        var what = dropped switch
        {
            null => "the ground could not be read, so whether the units were dropped there is unknown; check the ground near the player",
            { Count: 0 } => "no unit went in, so nothing moved",
            { Count: var count } => $"{count} new stacks of {units} lie on the ground, so which is the split's is unknown; check the ground near the player",
        };

        _picker.Toast(Failed);
        _log($"starting a new stack failed and {what}: {thrown}");

        return null;
    }

    private IReadOnlyDictionary<long, int>? ReadGround(string when)
    {
        try
        {
            return GroundDrops.Counts();
        }
        catch (Exception ex)
        {
            _log($"reading the ground {when} a split failed: {ex}");

            return null;
        }
    }

    /// <summary>Stacks the add made are taken away, and stacks already there that it topped up go back to their counts.</summary>
    private static void TakeBackAdd(ItemManager items, AddResult.Undo undo)
    {
        foreach (var (stackId, countBefore) in undo.Grown)
        {
            var grown = items.GetItemData(stackId) ?? throw new InvalidOperationException($"stack {stackId} the add topped up is gone");

            StackCounts.Restore(items, grown, countBefore);
            StackCounts.Expect(grown, countBefore);
        }

        foreach (var stackId in undo.NewStacks) items.ForceRemoveItem(stackId);
    }

    /// <summary>Taking every unit merges the two stacks, so the emptied one is removed whole, as the game removes a stack it drops from a bag.</summary>
    private void JoinStack(ItemManager items, ItemData source, SplitRequest request, SplitPlan.JoinStack plan)
    {
        var target = items.GetItemData(plan.StackId);

        if (target == null)
        {
            _log("the stack to top up was gone, so nothing moved");

            return;
        }

        var (sourceBefore, targetBefore) = (source.ItemCount, target.ItemCount);
        var configId = source.ItemConfigId;
        var isWholeStack = plan.Units >= sourceBefore;

        try
        {
            items.IncreaseItemCount(target, plan.Units);
            StackCounts.Expect(target, targetBefore + plan.Units);

            // The game's own removal of a whole stack: it leaves the bag and the caches, and only then is reported
            // destroyed, the bag told it is gone, and the object pooled, so `source` must not be read after this.
            if (isWholeStack) items.ForceRemoveItem(request.ItemId);
            else
            {
                items.DecreaseItemCount(source, plan.Units);
                StackCounts.Expect(source, sourceBefore - plan.Units);
            }
        }
        catch (Exception ex)
        {
            // Once the dragged stack is gone its units are in the target, which is where they were going.
            Undo("topping up a stack", ex, () =>
            {
                if (items.GetItemData(request.ItemId) == null) return;

                StackCounts.Restore(items, target, targetBefore);
                StackCounts.Restore(items, source, sourceBefore);
            });
            items.SyncBothBags(request.SrcOwnerId, request.DstOwnerId);

            return;
        }

        items.SyncBothBags(request.SrcOwnerId, request.DstOwnerId);
        _logFileOnly($"moved {plan.Units} of {sourceBefore} (item {configId}) onto a stack of {targetBefore}");
    }

    private void Undo(string what, Exception cause, Action undo)
    {
        _picker.Toast(Failed);

        try
        {
            undo();
            _log($"{what} failed and was undone, nothing moved: {cause}");
        }
        catch (Exception ex)
        {
            _log($"{what} failed and could not be undone; check this bag's counts. First failure: {cause}. Undoing: {ex}");
        }
    }
}
