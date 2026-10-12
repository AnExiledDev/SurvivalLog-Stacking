using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using Stacking.Core;

namespace Stacking.Adapters;

/// <summary>
/// Catches a stack dropped with Shift or Ctrl held. The bag's drag handler runs the game's own checks first and then
/// either moves the stack or drops it on the item under the cursor; the split takes the place of that move or drop,
/// so every drop the game would refuse is still refused. The split itself is carried out next frame by the ticker,
/// never inside the game's handler. Static because Harmony patches are.
/// </summary>
internal static class SplitHooks
{
    private const long PendingExpiryMs = 500;
    private const int MaxLoggedFailures = 3;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly ConcurrentQueue<SplitRequest> Requests = new();

    [ThreadStatic] private static bool _isStartingStack;

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

        if (!TryPatch(harmony, "dragging an item in a bag", () => AccessTools.Method(typeof(Reducer_Web_BackpackUI), nameof(Reducer_Web_BackpackUI.RA_DragItem)),
                prefix: nameof(RememberSplitDrop)))
        {
            return false;
        }

        if (!isCatchingMoves) log("a split stack cannot go to a free spot this session: a drag held Shift or Ctrl moves the whole stack there");
        if (!isCatchingDrops) log("a split stack cannot top up another this session: a drag held Shift or Ctrl onto a stack does what the game does");

        CanStartStacks = TryPatch(harmony, "starting a new stack", () => AccessTools.Method(typeof(ItemManager), nameof(ItemManager.FindStackableItem)),
            postfix: nameof(NoStackWhileStarting));
        TryPatch(harmony, "closing the bag", () => AccessTools.Method(typeof(Reducer_Web_BackpackUI), nameof(Reducer_Web_BackpackUI.RA_Close)),
            postfix: nameof(NoteBackpackClosed));

        return true;
    }

    public static bool TryTakeRequest(out SplitRequest? request) => Requests.TryDequeue(out request);

    public static bool TakeBackpackClosed()
    {
        var wasClosed = _wasBackpackClosed;

        _wasBackpackClosed = false;

        return wasClosed;
    }

    /// <summary>Adds an item that always starts its own stack at the cell it is given.</summary>
    public static ItemData? StartingNewStack(Func<ItemData?> add)
    {
        _isStartingStack = true;

        try
        {
            return add();
        }
        finally
        {
            _isStartingStack = false;
        }
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

    private static bool SplitInsteadOfMove(long ItemId, long FromOwnerId, long ToOwnerId) =>
        TakePending(request => request.ItemId == ItemId && request.SrcOwnerId == FromOwnerId && request.DstOwnerId == ToOwnerId) is not { } request
        || RunsOriginal(request);

    private static bool SplitInsteadOfDrop(ItemManager __instance, long dragId) =>
        !SplitScene.IsLive(__instance)
        || TakePending(request => request.ItemId == dragId) is not { } request
        || RunsOriginal(request);

    private static void NoStackWhileStarting(ref ItemData? __result)
    {
        if (_isStartingStack) __result = null;
    }

    private static void NoteBackpackClosed() => _wasBackpackClosed = true;

    private static SplitRequest? TakePending(Func<SplitRequest, bool> isThisDrop)
    {
        if (_pending is not { } pending || Clock.ElapsedMilliseconds - pending.SeenAt > PendingExpiryMs || !isThisDrop(pending.Request)) return null;

        _pending = null;

        return pending.Request;
    }

    /// <summary>A drop the core would hand to the game runs as the game's; anything else waits for the ticker.</summary>
    private static bool RunsOriginal(SplitRequest request)
    {
        try
        {
            var items = SplitScene.LiveItems();
            var source = items?.GetItemData(request.ItemId);

            if (items == null || source == null || SplitScene.Plan(items, source, request, chosenUnits: null) is SplitPlan.Vanilla) return true;

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
