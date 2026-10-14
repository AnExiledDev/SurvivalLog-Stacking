using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using Stacking.Core;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>
/// Catches a stack dropped with Shift or Ctrl held. The bag's drag handler runs the game's own checks first and then
/// either moves the stack or drops it on the item under the cursor; the split takes the place of that move or drop,
/// so every drop the game would refuse is still refused. The bag also shows inside the shop and the station views
/// (crafting table, brewing, cooking, shredder and the rest), each with a drag handler of its own that ends in the
/// same move. The split itself is carried out next frame by the ticker, never inside the game's handler. Static
/// because Harmony patches are.
/// </summary>
internal static class SplitHooks
{
    private const long PendingExpiryMs = 500;
    private const int MaxLoggedFailures = 3;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly ConcurrentQueue<SplitRequest> Requests = new();

    /// <summary>
    /// Each view's drag handler, which ends in the bag's move. The Dobby rule holds for each new one (no relative jump
    /// or call in the first 14 bytes; a register save, pushes and a stack reserve come before the first compare and
    /// short jump): compressor rva 0x28c8ec0, dyeing 0x295dbf0, drone hub 0x2954ab0, greenhouse 0x299b800, shredder
    /// 0x2a6d6f0, aid depot 0x2878380, traps 0x2ae2cb0, rat cage 0x2a24350.
    /// </summary>
    private static readonly (string View, Type Reducer, string Method)[] ViewDrags =
    {
        ("the crafting table", typeof(Reducer_Web_ToolTable), nameof(Reducer_Web_ToolTable.RA_ItemMove)),
        ("brewing", typeof(Reducer_Web_Brew), nameof(Reducer_Web_Brew.RA_ItemMove)),
        ("the shop", typeof(Reducer_Web_ShopUI), nameof(Reducer_Web_ShopUI.RA_DragItem)),
        ("the shredder", typeof(Reducer_Web_Shredder), nameof(Reducer_Web_Shredder.RA_ItemMove)),
        ("the compressor", typeof(Reducer_Web_Compressor), nameof(Reducer_Web_Compressor.RA_ItemMove)),
        ("dyeing", typeof(Reducer_Web_Dye), nameof(Reducer_Web_Dye.RA_ItemMove)),
        ("the drone hub", typeof(Reducer_Web_DroneHub), nameof(Reducer_Web_DroneHub.RA_ItemMove)),
        ("greenhouse building", typeof(Reducer_Web_GreenhouseBuild), nameof(Reducer_Web_GreenhouseBuild.RA_ItemMove)),
        ("the aid depot", typeof(Reducer_Web_AidDepot), nameof(Reducer_Web_AidDepot.RA_ItemMove)),
        ("traps", typeof(Reducer_Web_TrapPanel), nameof(Reducer_Web_TrapPanel.RA_ItemMove)),
        ("the rat cage", typeof(Reducer_Web_RatCage), nameof(Reducer_Web_RatCage.RA_FoodMove)),
    };

    [ThreadStatic] private static bool _isInViewDrag;

    private static Action<string> _log = _ => { };
    private static (SplitRequest Request, long SeenAt)? _pending;
    private static volatile bool _wasBackpackClosed;
    private static int _failures;

    /// <summary>Without it the game would add the split units to any stack in the bag, wherever the player dropped them.</summary>
    public static bool CanStartStacks { get; private set; }

    /// <summary>False when no drop can be split; a split onto a free spot also needs CanStartStacks.</summary>
    public static bool Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        var isCatchingMoves = TryPatch(harmony, "moving an item in a bag", () => AccessTools.Method(typeof(Ac_Item_ChangePos), nameof(Ac_Item_ChangePos.SendAction)),
            prefix: nameof(SplitInsteadOfMove));
        var isCatchingDrops = TryPatch(harmony, "dropping an item on another", () => AccessTools.Method(typeof(ItemManager), nameof(ItemManager.OnSwapItem)),
            prefix: nameof(SplitInsteadOfDrop));

        if (!isCatchingMoves && !isCatchingDrops) return false;

        if (!Hooks.TryPatchScope(harmony, typeof(SplitHooks), "dragging an item in a bag",
                () => AccessTools.Method(typeof(Reducer_Web_BackpackUI), nameof(Reducer_Web_BackpackUI.RA_DragItem)), log,
                open: nameof(RememberSplitDrop), close: nameof(ForgetSplitDrop)))
        {
            return false;
        }

        CatchViewDrags(harmony, log, isCatchingMoves);

        if (!isCatchingMoves) log("a split stack cannot go to a free spot this session: a drag held Shift or Ctrl moves the whole stack there");
        if (!isCatchingDrops) log("a split stack cannot top up another this session: a drag held Shift or Ctrl onto a stack does what the game does");

        CanStartStacks = NewStacks.Apply(harmony, log);
        TryPatch(harmony, "closing the bag", () => AccessTools.Method(typeof(Reducer_Web_BackpackUI), nameof(Reducer_Web_BackpackUI.RA_Close)),
            postfix: nameof(NoteBackpackClosed));

        return true;
    }

    public static bool TryTakeRequest(out SplitRequest? request) => Requests.TryDequeue(out request);

    /// <summary>
    /// The shop and most station views move a dragged item with the same move the bag uses, after their own checks;
    /// the cooking view sends its own move to the item manager instead, so it is caught there. The heater, fuel
    /// generator and material rack views send no move within the bag at all, so a drag there has nothing to split.
    /// </summary>
    private static void CatchViewDrags(Harmony harmony, Action<string> log, bool isCatchingMoves)
    {
        var splitsIn = new List<string>();

        if (isCatchingMoves)
        {
            foreach (var (view, reducer, method) in ViewDrags)
            {
                if (TryPatchViewDrag(harmony, log, view, reducer, method)) splitsIn.Add(view);
            }
        }

        if (TryPatch(harmony, "dragging an item in the cooking view", () => AccessTools.Method(typeof(ItemManager), nameof(ItemManager.OnCookingDragMove)),
                prefix: nameof(SplitInsteadOfCookingMove)))
        {
            splitsIn.Add("cooking");
        }

        log(splitsIn.Count == 0 ? "a drag in a view other than the bag moves the whole stack this session" : "a drag also splits in the views of " + string.Join(", ", splitsIn));
        log("a drag with Shift or Ctrl onto a free spot does not split in the heater, fuel generator or material rack views: the game never moves an item within the bag there");
    }

    private static bool TryPatchViewDrag(Harmony harmony, Action<string> log, string view, Type reducer, string method) =>
        Hooks.TryPatchScope(harmony, typeof(SplitHooks), $"dragging an item in {view}", () => AccessTools.Method(reducer, method), log,
            open: nameof(EnterViewDrag), close: nameof(LeaveViewDrag));

    public static bool TakeBackpackClosed()
    {
        var wasClosed = _wasBackpackClosed;

        _wasBackpackClosed = false;

        return wasClosed;
    }

    private static bool TryPatch(Harmony harmony, string what, Func<MethodBase?> target, string? prefix = null, string? postfix = null) =>
        Hooks.TryPatch(harmony, typeof(SplitHooks), what, target, _log, prefix, postfix);

    private static void RememberSplitDrop(Ac_BackpackUI_DragItem ac)
    {
        _pending = null;

        try
        {
            var kind = SplitKeys.Current();

            if (kind == SplitKind.None || ac == null || ac.DstOwnerId == 0) return;

            var item = SplitScene.LiveItems()?.GetItemData(ac.ItemId);

            if (item == null || item.ItemCount < 2 || item.InstanceWeight != 0 || item.OwnerId != ac.SrcOwnerId) return;

            var request = new SplitRequest(ac.ItemId, ac.SrcOwnerId, ac.DstOwnerId, kind, new Cell(ac.NewX, ac.NewY), CellOrNone(ac.CursorX, ac.CursorY));

            _pending = (request, Clock.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            Fail("noticing a split drop", ex);
        }
    }

    /// <summary>
    /// The game moves or drops the stack inside its drag handler (the drop's event reaches ItemManager before the
    /// handler returns), so a split drop the handler did not take is a drop it refused, and must not wait for the next one.
    /// </summary>
    private static void ForgetSplitDrop() => _pending = null;

    private static void EnterViewDrag() => _isInViewDrag = true;

    private static void LeaveViewDrag() => _isInViewDrag = false;

    /// <summary>A bag drag was noted before its move; a move inside another view's drag handler is read from the move itself.</summary>
    private static bool SplitInsteadOfMove(long ItemId, long FromOwnerId, long ToOwnerId, Vector2Int BagPos)
    {
        if (TakePending(request => request.ItemId == ItemId && request.SrcOwnerId == FromOwnerId && request.DstOwnerId == ToOwnerId) is { } request) return RunsOriginal(request);

        return !_isInViewDrag || ViewDragRequest(ItemId, FromOwnerId, ToOwnerId, BagPos) is not { } viewDrag || RunsOriginal(viewDrag);
    }

    /// <summary>A move into a station the station rule holds the stack to is planned as the game's own, which that rule cuts to one unit.</summary>
    private static bool SplitInsteadOfCookingMove(ItemManager __instance, long itemId, long fromOwnerId, long toOwnerId, Vector2Int bagPos) =>
        !SplitScene.IsLive(__instance) || ViewDragRequest(itemId, fromOwnerId, toOwnerId, bagPos) is not { } request || RunsOriginal(request);

    /// <summary>Views other than the bag never say where the cursor was, so the split lands where the stack was dropped.</summary>
    private static SplitRequest? ViewDragRequest(long itemId, long fromOwnerId, long toOwnerId, Vector2Int bagPos)
    {
        try
        {
            var kind = SplitKeys.Current();

            if (kind == SplitKind.None || toOwnerId == 0) return null;

            var item = SplitScene.LiveItems()?.GetItemData(itemId);

            if (item == null || item.ItemCount < 2 || item.InstanceWeight != 0 || item.OwnerId != fromOwnerId) return null;

            return new SplitRequest(itemId, fromOwnerId, toOwnerId, kind, new Cell(bagPos.x, bagPos.y), Cursor: null);
        }
        catch (Exception ex)
        {
            Fail("noticing a split drag", ex);

            return null;
        }
    }

    private static bool SplitInsteadOfDrop(ItemManager __instance, long dragId) =>
        !SplitScene.IsLive(__instance)
        || TakePending(request => request.ItemId == dragId) is not { } request
        || RunsOriginal(request);

    private static void NoteBackpackClosed() => _wasBackpackClosed = true;

    /// <summary>The expiry only matters when the game would not take a finalizer and a throwing drag handler skipped ForgetSplitDrop.</summary>
    private static SplitRequest? TakePending(Func<SplitRequest, bool> isThisDrop)
    {
        if (_pending is not { } pending || Clock.ElapsedMilliseconds - pending.SeenAt > PendingExpiryMs || !isThisDrop(pending.Request)) return null;

        _pending = null;

        return pending.Request;
    }

    /// <summary>A drop the core would hand to the game runs as the game's; anything else waits for the ticker.</summary>
    internal static bool RunsOriginal(SplitRequest request)
    {
        try
        {
            var items = SplitScene.LiveItems();
            var source = items?.GetItemData(request.ItemId);

            if (items == null || source == null || SplitScene.Plan(items, source, request, chosenUnits: null, log: _ => { }) is SplitPlan.Vanilla) return true;

            Requests.Enqueue(request);

            return false;
        }
        catch (Exception ex)
        {
            Fail("planning a split", ex);

            return true;
        }
    }

    private static Cell? CellOrNone(int x, int y) => x < 0 || y < 0 ? null : new Cell(x, y);

    private static void Fail(string what, Exception ex)
    {
        _failures++;

        if (_failures <= MaxLoggedFailures) _log($"{what} failed, the drop moves the whole stack (logged {_failures} of {MaxLoggedFailures}): {ex}");
    }
}
