using System;
using System.Collections.Generic;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Stacking.Core;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>
/// Splitting stacks in the trade window. A drag with Shift (half), Ctrl (one) or Ctrl+Shift (choose) held splits the
/// stack wherever it goes and the units land where they were dropped: offered on the drone, taken back into the bag,
/// moved to another drone cell as an offer of their own, or put down elsewhere in the bag. A right-click with those
/// keys offers part of a stack or takes part of an offer back. A transfer click on the bag offers one unit, topping up
/// the same kind's offer when there is one. The game offers whole stacks only, so the units are split off into a stack
/// of their own in the bag, and the ticker offers that stack once the window shows it; a refused offer goes back on
/// the stack it came from. Whenever units leave or join a stack on offer, that offer is put down again where it lies
/// so the window counts it afresh. A plain drop of a stack onto another (heard by DropWatch) joins them, and an offered
/// stack poured whole is taken off its offer first. When to do what is the core's call (see TradeOffers). Static because Harmony
/// patches are.
/// </summary>
internal static class TradeSplit
{
    private const int MaxLoggedFailures = 3;

    private static Action<string> _log = _ => { };
    private static PendingOffer? _offer;
    private static readonly List<PendingTakeBack> Recounts = new();
    private static (PendingJoin Wait, SplitRequest Request)? _join;
    private static (long StackId, float SentAt)? _recountSent;
    private static int _failures;

    /// <summary>The game's type for the trade window, looked up once: each lookup makes a new object.</summary>
    private static Il2CppSystem.Type? _tradeScreen;

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        // Both save registers and reserve stack (24 and 18 bytes) before their first compare and jump, so neither has
        // a relative jump or call in its first 14 bytes (Dobby rule).
        var drags = Hooks.TryPatch(harmony, typeof(TradeSplit), "dragging an item in the trade window", // rva 0x2c5a220
            () => AccessTools.Method(typeof(Reducer_Web_TradeUI), "DragItemCore"), log, prefix: nameof(SplitInsteadOfDrag));
        var clicks = Hooks.TryPatch(harmony, typeof(TradeSplit), "clicking an item in the trade window", // rva 0x2c562e0
            () => AccessTools.Method(typeof(Reducer_Web_TradeUI), "ClickItemCore"), log, prefix: nameof(SplitInsteadOfClick));

        if (drags) log("a drag with Shift or Ctrl splits a stack anywhere in the trade window, and the units land where they are dropped");
        if (clicks) log("a transfer click offers one unit in the trade window, and a right-click with Shift or Ctrl offers or takes back part of a stack");
    }

    /// <summary>
    /// Once per frame: once the window shows the units that left or joined an offer, puts that offer down again; offers
    /// a split-off stack once the window shows it, and takes it back if refused.
    /// </summary>
    public static void Tick(float now)
    {
        if (_offer is null && Recounts.Count == 0 && _join is null) return;

        if (OpenTrade() is not { } trade)
        {
            _offer = null;
            Recounts.Clear();
            _join = null;

            return;
        }

        if (_join is { } join) TickJoin(trade, join.Wait, join.Request, now);
        if (Recounts.Count > 0) TickRecount(trade, Recounts[0], now);
        if (_offer is { } offer) TickOffer(trade, offer, now);
    }

    /// <summary>
    /// A stack let go over another in the trade window, which the page puts back without telling the game. The units
    /// join the stack under it, and an offer that gains or loses units is put down again. False when the window is closed.
    /// </summary>
    public static bool DroppedOnto(SplitRequest request, int draggedCount)
    {
        if (OpenTrade() is not { } trade) return false;

        try
        {
            if (SplitHooks.PlanNow(request) is not SplitPlan.JoinStack join)
            {
                SplitHooks.RunsOriginal(request);

                return true;
            }

            var draggedAt = TradeOffers.OfferedAt(trade.GiveJson, request.ItemId);
            var takesBackFirst = TradeOffers.TakesBackFirst(draggedAt != null, join.Units, draggedCount);
            var joinedAt = TradeOffers.OfferedAt(trade.GiveJson, join.StackId);
            var landing = request with { OnLanded = RecountBoth(join.StackId, joinedAt, request.ItemId, takesBackFirst ? null : draggedAt) };

            if (!takesBackFirst)
            {
                SplitHooks.RunsOriginal(landing);

                return true;
            }

            _join = (new PendingJoin(request.ItemId, Time.unscaledTime), landing);
            Ac_TradeUI_ClickItem.SendAction(request.ItemId.ToString(), TradeOffers.DroneGrid, true);
        }
        catch (Exception ex)
        {
            _join = null;
            Fail(ex);
        }

        return true;
    }

    private static void TickJoin(State_Web_TradeUI trade, PendingJoin wait, SplitRequest request, float now)
    {
        switch (TradeOffers.Next(wait, now, TradeOffers.IsOffered(trade.GiveJson, wait.StackId)))
        {
            case OfferStep.Wait:
                break;
            case OfferStep.Send:
                _join = null;
                SplitHooks.RunsOriginal(request);
                break;
            case OfferStep.GiveUp:
                _join = null;
                _log("the trade window did not take an offer back, so the stacks dropped onto each other were not joined");
                break;
            default:
                throw new InvalidOperationException("every join step needs carrying out: " + wait);
        }
    }

    /// <summary>One offer is put down again at a time, once the last has reached the game, so each is told apart from a player's drag.</summary>
    private static void TickRecount(State_Web_TradeUI trade, PendingTakeBack recount, float now)
    {
        if (_recountSent is { } sent && now - sent.SentAt <= TradeOffers.ShowWaitSeconds) return;

        switch (TradeOffers.Next(recount, now, IsInBag(trade, recount.StackId)))
        {
            case OfferStep.Wait:
                break;
            case OfferStep.Send:
                Recounts.RemoveAt(0);
                Recount(recount, now);
                break;
            case OfferStep.GiveUp:
                Recounts.RemoveAt(0);
                _log("the trade window never showed units split off an offer, so the offer was not counted again");
                break;
            default:
                throw new InvalidOperationException("every recount step needs carrying out: " + recount);
        }
    }

    private static void TickOffer(State_Web_TradeUI trade, PendingOffer offer, float now)
    {
        switch (TradeOffers.Next(offer, now, IsInBag(trade, offer.StackId), TradeOffers.IsOffered(trade.GiveJson, offer.StackId)))
        {
            case OfferStep.Wait:
                break;
            case OfferStep.Send:
                _offer = offer with { SentAt = now };
                Send(offer);
                break;
            case OfferStep.Done:
                _offer = null;
                break;
            case OfferStep.Rejoin:
                _offer = null;
                Rejoin(offer, trade.GiveJson);
                break;
            case OfferStep.GiveUp:
                _offer = null;
                _log("the trade window never showed a split-off stack, so it stays in the bag unoffered");
                break;
            default:
                throw new InvalidOperationException("every offer step needs carrying out: " + offer);
        }
    }

    /// <summary>True when the game's own drag should run instead, because this drag splits nothing.</summary>
    private static bool SplitInsteadOfDrag(string fromGrid, string toGrid, string itemKey, int x, int y, State_Web_TradeUI state, ref State_Web_TradeUI __result)
    {
        try
        {
            if (!long.TryParse(itemKey, out var stackId) || IsOwnMove(stackId)) return true;

            var kind = SplitKeys.ForDrop();
            var drag = TradeOffers.DragOf(fromGrid, toGrid, kind);

            if (drag == TradeDrag.Game || DragRequest(drag, stackId, kind, new Cell(x, y), state) is not { } request || SplitHooks.RunsOriginal(request)) return true;

            __result = state;

            return false;
        }
        catch (Exception ex)
        {
            Fail(ex);

            return true;
        }
    }

    /// <summary>True when the game's own click should run instead, because this click splits nothing.</summary>
    private static bool SplitInsteadOfClick(string itemKey, string fromGrid, bool rightClick, State_Web_TradeUI state, ref State_Web_TradeUI __result)
    {
        try
        {
            if (!long.TryParse(itemKey, out var stackId) || IsOwnMove(stackId)) return true;

            var kind = SplitKeys.ForClick();
            var request = TradeOffers.ClickOf(fromGrid, rightClick, kind, state.IsHighlightKeyHeld?.Value == true) switch
            {
                TradeClick.Split => SplitClickRequest(stackId, kind, fromGrid, state),
                TradeClick.OfferOne => OfferOneRequest(stackId, state),
                _ => null,
            };

            if (request == null || SplitHooks.RunsOriginal(request)) return true;

            __result = state;

            return false;
        }
        catch (Exception ex)
        {
            Fail(ex);

            return true;
        }
    }

    /// <summary>The mod's own offer and recount come back through these hooks, perhaps with the keys still held, and must reach the game as they are.</summary>
    private static bool IsOwnMove(long stackId)
    {
        if (_offer?.StackId == stackId || _join?.Wait.StackId == stackId) return true;

        if (_recountSent is not { } sent || sent.StackId != stackId) return false;

        _recountSent = null;

        return Time.unscaledTime - sent.SentAt <= TradeOffers.ShowWaitSeconds;
    }

    /// <summary>An offered stack stays in its bag, so units split off it go into the bag too, and leave the offer smaller.</summary>
    private static SplitRequest? DragRequest(TradeDrag drag, long stackId, SplitKind kind, Cell droppedAt, State_Web_TradeUI state)
    {
        if (SplitScene.LiveItems()?.GetItemData(stackId) is not { } stack) return null;

        var offeredAt = TradeOffers.OfferedAt(state.GiveJson, stackId);
        var bag = ShownBag(state, stack);

        return drag switch
        {
            TradeDrag.Offer => Beside(stack, bag, kind, OfferThen(stackId, new OfferTarget(droppedAt), offeredAt)),
            TradeDrag.SplitOffer when offeredAt != null => Beside(stack, bag, kind, OfferThen(stackId, new OfferTarget(droppedAt), offeredAt)),
            TradeDrag.TakeBack when offeredAt != null => DroppedIn(stack, bag, kind, droppedAt, RecountThen(stackId, offeredAt)),
            TradeDrag.InBag => DroppedIn(stack, bag, kind, droppedAt, RecountThen(stackId, offeredAt)),
            _ => null,
        };
    }

    private static SplitRequest? SplitClickRequest(long stackId, SplitKind kind, string fromGrid, State_Web_TradeUI state)
    {
        if (SplitScene.LiveItems()?.GetItemData(stackId) is not { } stack) return null;

        var offeredAt = TradeOffers.OfferedAt(state.GiveJson, stackId);

        if (!TradeOffers.TakesBack(fromGrid)) return Beside(stack, stack.OwnerId, kind, OfferThen(stackId, new OfferTarget(At: null), offeredAt));

        return offeredAt == null ? null : Beside(stack, stack.OwnerId, kind, RecountThen(stackId, offeredAt));
    }

    /// <summary>
    /// One unit offered: added to an offer of the same kind from the same bag when the two would stack, so clicks
    /// build one offer, or else split off and offered on its own. A single unit, or a stack already on offer, is the
    /// game's to move.
    /// </summary>
    private static SplitRequest? OfferOneRequest(long stackId, State_Web_TradeUI state)
    {
        var items = SplitScene.LiveItems();

        if (items?.GetItemData(stackId) is not { } stack || TradeOffers.IsOffered(state.GiveJson, stackId)) return null;

        if (TopUp(items, stack, state.GiveJson) is { } topUp) return topUp;

        return stack.ItemCount < 2 ? null : Beside(stack, stack.OwnerId, SplitKind.One, OfferThen(stackId, new OfferTarget(At: null), sourceOfferedAt: null));
    }

    private static SplitRequest? TopUp(ItemManager items, ItemData stack, string? offeredJson)
    {
        var kind = stack.ItemCount > 1 ? SplitKind.One : SplitKind.Join;

        foreach (var offered in TradeOffers.Offered(offeredJson))
        {
            if (items.GetItemData(offered.Id) is not { } target || target.OwnerId != stack.OwnerId || target.ItemConfigId != stack.ItemConfigId) continue;

            var at = new Cell(target.BagPos.x, target.BagPos.y);
            var request = new SplitRequest(stack.InstanceId, stack.OwnerId, stack.OwnerId, kind, at, Cursor: at, OnLanded: RecountThen(offered.Id, offered.At));

            if (SplitHooks.PlanNow(request) is SplitPlan.JoinStack join && join.StackId == offered.Id) return request;
        }

        return null;
    }

    /// <summary>A split that lands at the free spot nearest the stack, in the bag given.</summary>
    private static SplitRequest Beside(ItemData stack, long bag, SplitKind kind, Action<long>? onLanded) =>
        new(stack.InstanceId, stack.OwnerId, bag, kind, new Cell(stack.BagPos.x, stack.BagPos.y), Cursor: null, IsRightClick: true, OnLanded: onLanded);

    /// <summary>A split that lands on the cell it was dropped on. A material rack is drawn without cells, so there the units go beside the stack.</summary>
    private static SplitRequest DroppedIn(ItemData stack, long bag, SplitKind kind, Cell droppedAt, Action<long>? onLanded) =>
        ItemManager.IsMaterialRackOwner(bag)
            ? Beside(stack, bag, kind, onLanded)
            : new SplitRequest(stack.InstanceId, stack.OwnerId, bag, kind, droppedAt, Cursor: null, OnLanded: onLanded);

    /// <summary>The bag the window shows, which is where the player dropped the units.</summary>
    private static long ShownBag(State_Web_TradeUI state, ItemData stack) => state.BagOwnerId != 0 ? state.BagOwnerId : stack.OwnerId;

    private static Action<long> OfferThen(long sourceId, OfferTarget target, Cell? sourceOfferedAt) => landedId =>
    {
        _offer = new PendingOffer(landedId, sourceId, target, Time.unscaledTime);

        if (sourceOfferedAt is { } at) Recounts.Add(new PendingTakeBack(landedId, sourceId, at, Time.unscaledTime));
    };

    private static Action<long>? RecountThen(long offeredId, Cell? offeredAt) =>
        offeredAt is { } at ? landedId => Recounts.Add(new PendingTakeBack(landedId, offeredId, at, Time.unscaledTime)) : null;

    /// <summary>After a join, the stack joined and the stack the units came from are each counted again when on offer.</summary>
    private static Action<long> RecountBoth(long joinedId, Cell? joinedAt, long draggedId, Cell? draggedAt) => landedId =>
    {
        if (joinedAt is { } joined) Recounts.Add(new PendingTakeBack(landedId, joinedId, joined, Time.unscaledTime));
        if (draggedAt is { } dragged) Recounts.Add(new PendingTakeBack(draggedId, draggedId, dragged, Time.unscaledTime));
    };

    private static void Send(PendingOffer offer)
    {
        var key = offer.StackId.ToString();

        if (offer.Target.At is not { } cell)
        {
            Ac_TradeUI_ClickItem.SendAction(key, TradeOffers.BagGrid, true);

            return;
        }

        SendDrag(TradeOffers.BagGrid, key, cell);
    }

    /// <summary>The offer put down where it lies, so the window counts what is in it now.</summary>
    private static void Recount(PendingTakeBack recount, float now)
    {
        _recountSent = (recount.OfferedId, now);
        SendDrag(TradeOffers.DroneGrid, recount.OfferedId.ToString(), recount.OfferedAt);
    }

    private static void SendDrag(string fromGrid, string itemKey, Cell toDroneCell)
    {
        // The game's build has no compiled SendAction for this one, so the action is made the way theirs are.
        var drag = new Ac_TradeUI_DragItem
        {
            ActionType = Il2CppType.Of<Ac_TradeUI_DragItem>(),
            FromGrid = fromGrid,
            ToGrid = TradeOffers.DroneGrid,
            ItemKey = itemKey,
            X = toDroneCell.X,
            Y = toDroneCell.Y,
        };

        GameSingleton.Existing<ReduxUISystem>()?.SendAction(drag);
    }

    /// <summary>A plain drop of the split-off stack on the one it came from, which joins them; an offer it joins is counted again.</summary>
    private static void Rejoin(PendingOffer offer, string? offeredJson)
    {
        var items = SplitScene.LiveItems();

        if (items?.GetItemData(offer.SourceId) is not { } source || items.GetItemData(offer.StackId) is not { } split) return;

        var at = new Cell(source.BagPos.x, source.BagPos.y);
        var onLanded = RecountThen(offer.SourceId, TradeOffers.OfferedAt(offeredJson, offer.SourceId));

        SplitHooks.RunsOriginal(new SplitRequest(split.InstanceId, split.OwnerId, source.OwnerId, SplitKind.Join, at, Cursor: at, OnLanded: onLanded));
    }

    private static State_Web_TradeUI? OpenTrade()
    {
        var ui = GameSingleton.Existing<ReduxUISystem>();
        var screen = _tradeScreen ??= Il2CppType.Of<WebUI_TradeUI>();

        if (ui == null || !ui.IsUIActive(screen)) return null;

        return ui.GetStateByUI(screen)?.TryCast<State_Web_TradeUI>();
    }

    /// <summary>The trade window's bag lists what it draws: the bag shown, and the bag behind each tab.</summary>
    private static bool IsInBag(State_Web_TradeUI trade, long stackId)
    {
        if (Holds(trade.ItemDataList, stackId)) return true;

        if (trade.TabItems == null) return false;

        foreach (var tab in trade.TabItems)
        {
            if (Holds(tab.Value, stackId)) return true;
        }

        return false;
    }

    private static bool Holds(Il2CppSystem.Collections.Generic.List<Data_Item>? items, long stackId)
    {
        if (items == null) return false;

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i]?.LogicId == stackId) return true;
        }

        return false;
    }

    private static void Fail(Exception ex)
    {
        _failures++;

        if (_failures <= MaxLoggedFailures) _log($"splitting a stack in the trade window failed, the game does what it does (logged {_failures} of {MaxLoggedFailures}): {ex}");
    }
}
