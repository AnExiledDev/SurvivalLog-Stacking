using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Stacking.Core;
using UnityEngine;

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

        var plan = SplitScene.Plan(items, source, request, chosenUnits);

        switch (plan)
        {
            case SplitPlan.Vanilla:
                _log("the stack changed before the split, so nothing moved");
                break;
            case SplitPlan.Refused refused:
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
        var created = SplitHooks.StartingNewStack(() => AddLike(items, source, request.DstOwnerId, plan.At, plan.Units));

        if (created == null)
        {
            _log("the game made no new stack, so nothing moved");

            return;
        }

        try
        {
            CopyFreshness(source, created);
            items.DecreaseItemCount(source, plan.Units);
            ExpectCount(source, countBefore - plan.Units);
        }
        catch (Exception ex)
        {
            Undo("starting a new stack", ex, () =>
            {
                RestoreCount(items, source, countBefore);
                items.ForceRemoveItem(created.InstanceId);
            });
            items.SyncBothBags(request.SrcOwnerId, request.DstOwnerId);

            return;
        }

        if (created.OwnerId != request.DstOwnerId || created.BagPos != new Vector2Int(plan.At.X, plan.At.Y))
        {
            _log($"the new stack of {plan.Units} did not fit where it was dropped and the game put it elsewhere (owner {created.OwnerId}, cell {created.BagPos})");
        }

        items.SyncBothBags(request.SrcOwnerId, request.DstOwnerId);
        _logFileOnly($"split {plan.Units} of {countBefore} (item {source.ItemConfigId}) into a new stack at {plan.At.X},{plan.At.Y}");
    }

    /// <summary>Taking every unit merges the two stacks, so the emptied one is removed the way the game's own merge removes it.</summary>
    private void JoinStack(ItemManager items, ItemData source, SplitRequest request, SplitPlan.JoinStack plan)
    {
        var target = items.GetItemData(plan.StackId);

        if (target == null)
        {
            _log("the stack to top up was gone, so nothing moved");

            return;
        }

        var (sourceBefore, targetBefore) = (source.ItemCount, target.ItemCount);
        var isWholeStack = plan.Units >= sourceBefore;

        try
        {
            items.IncreaseItemCount(target, plan.Units);
            ExpectCount(target, targetBefore + plan.Units);

            if (isWholeStack) RemoveEmptied(items, request.SrcOwnerId, request.ItemId);
            else
            {
                items.DecreaseItemCount(source, plan.Units);
                ExpectCount(source, sourceBefore - plan.Units);
            }
        }
        catch (Exception ex)
        {
            // Once the dragged stack is gone its units are in the target, which is where they were going.
            Undo("topping up a stack", ex, () =>
            {
                if (items.GetItemData(request.ItemId) == null) return;

                RestoreCount(items, target, targetBefore);
                RestoreCount(items, source, sourceBefore);
            });
            items.SyncBothBags(request.SrcOwnerId, request.DstOwnerId);

            return;
        }

        items.SyncBothBags(request.SrcOwnerId, request.DstOwnerId);
        _logFileOnly($"moved {plan.Units} of {sourceBefore} (item {source.ItemConfigId}) onto a stack of {targetBefore}");
    }

    /// <summary>A copy of the dragged stack: same freshness, uses, quality and flags, never a weight of its own.</summary>
    private static ItemData? AddLike(ItemManager items, ItemData source, long ownerId, Cell at, int units) =>
        items.AddItem(ownerId, source.ItemConfigId, units, source.StartTime, source.TimeLeft, new Vector2Int(at.X, at.Y), source.UseTimes,
            skipBagPos: false, timeScale: source.TimeScale, instanceVD: Clone(source.InstanceVD), instanceEffectEnd: Clone(source.InstanceEffectEnd),
            maxUseTimes: source.MaxUseTimes, instanceBurnValue: source.InstanceBurnValue, polluted: source.Polluted, isMapPreset: source.IsMapPreset,
            noPackage: source.NoPackage, instanceWeight: 0);

    /// <summary>
    /// The game sets the new stack's speed of decay from the bag it lands in. In a bag that keeps the same speed the
    /// fractions the add leaves out are copied too, so the two halves age as one.
    /// </summary>
    private static void CopyFreshness(ItemData source, ItemData created)
    {
        if (created.TimeScale != source.TimeScale) return;

        created.OriginalTimeLeft = source.OriginalTimeLeft;
        created.TimeLeftFrac = source.TimeLeftFrac;
    }

    /// <summary>Each stack gets its own copy, or a change to one stack's values would show on the other.</summary>
    private static Il2CppStructArray<float>? Clone(Il2CppStructArray<float>? values)
    {
        if (values == null) return null;

        var copy = new Il2CppStructArray<float>(values.Length);

        for (var i = 0; i < values.Length; i++) copy[i] = values[i];

        return copy;
    }

    private static void RemoveEmptied(ItemManager items, long ownerId, long itemId)
    {
        items.RemoveItem(ownerId, itemId);
        GameSingleton.Existing<BattleLogicWorld>()?._ActionManager?.OnItemInstanceDestroyed(itemId);

        if (items.GetItemData(itemId) != null) items.ForceRemoveItem(itemId);
    }

    /// <summary>The game's count calls report whether a stack emptied, not whether they worked, so the count itself is checked.</summary>
    private static void ExpectCount(ItemData item, int expected)
    {
        if (item.ItemCount != expected) throw new InvalidOperationException($"the stack holds {item.ItemCount}, expected {expected}");
    }

    private static void RestoreCount(ItemManager items, ItemData item, int count)
    {
        var missing = count - item.ItemCount;

        if (missing > 0) items.IncreaseItemCount(item, missing);
        else if (missing < 0) items.DecreaseItemCount(item, -missing);
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
