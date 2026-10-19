using System;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Stacking.Core;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>
/// Offering part of a stack in the trade window: a drag from the bag onto the drone, or a right-click on a stack in
/// the bag, with Shift (half), Ctrl (one) or Ctrl+Shift (choose) held. The game offers whole stacks only, so the hooks
/// take the drag or click, the split makes a new stack beside the old one in the bag, and the ticker offers that stack
/// the way the player offered the old one once the trade window shows it. A refused offer goes back on the stack it
/// came from. The same gestures from the drone back to the bag take part of an offer back: the units split off into
/// the bag, and the smaller offer is put down again. When to do what is the core's call (see TradeOffers). Static
/// because Harmony patches are.
/// </summary>
internal static class TradeSplit
{
    private const int MaxLoggedFailures = 3;

    private static Action<string> _log = _ => { };
    private static PendingOffer? _offer;
    private static PendingTakeBack? _takeBack;
    private static int _failures;

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        // Both save registers and reserve stack (24 and 18 bytes) before their first compare and jump, so neither has
        // a relative jump or call in its first 14 bytes (Dobby rule).
        var drags = Hooks.TryPatch(harmony, typeof(TradeSplit), "dragging an item in the trade window", // rva 0x2c5a220
            () => AccessTools.Method(typeof(Reducer_Web_TradeUI), "DragItemCore"), log, prefix: nameof(SplitInsteadOfDrag));
        var clicks = Hooks.TryPatch(harmony, typeof(TradeSplit), "clicking an item in the trade window", // rva 0x2c562e0
            () => AccessTools.Method(typeof(Reducer_Web_TradeUI), "ClickItemCore"), log, prefix: nameof(SplitInsteadOfClick));

        if (drags || clicks) log("a drag or a right-click with Shift or Ctrl offers part of a stack in the trade window, or takes part of an offer back");
    }

    /// <summary>
    /// Once per frame: offers a split-off stack once the trade window shows it, and takes it back if refused; once units
    /// taken back off an offer show in the bag, puts the smaller offer down again.
    /// </summary>
    public static void Tick(float now)
    {
        if (_offer is null && _takeBack is null) return;

        if (OpenTrade() is not { } trade)
        {
            _offer = null;
            _takeBack = null;

            return;
        }

        if (_offer is { } offer) TickOffer(trade, offer, now);
        if (_takeBack is { } takeBack) TickTakeBack(trade, takeBack, now);
    }

    private static void TickTakeBack(State_Web_TradeUI trade, PendingTakeBack takeBack, float now)
    {
        switch (TradeOffers.Next(takeBack, now, IsInBag(trade, takeBack.StackId)))
        {
            case OfferStep.Wait:
                break;
            case OfferStep.Send:
                _takeBack = null;
                Recount(takeBack);
                break;
            case OfferStep.GiveUp:
                _takeBack = null;
                _log("the trade window never showed units taken back off an offer; they stay in the bag");
                break;
            default:
                throw new InvalidOperationException("every take-back step needs carrying out: " + takeBack);
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
                Rejoin(offer);
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
            var kind = SplitKeys.ForDrop();

            if (!TradeOffers.SplitsDrag(fromGrid, toGrid, kind) || !Split(itemKey, kind, fromGrid, state, new OfferTarget(new Cell(x, y)))) return true;

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
            var kind = SplitKeys.ForClick();

            if (!TradeOffers.SplitsClick(fromGrid, rightClick, kind) || !Split(itemKey, kind, fromGrid, state, new OfferTarget(At: null))) return true;

            __result = state;

            return false;
        }
        catch (Exception ex)
        {
            Fail(ex);

            return true;
        }
    }

    /// <summary>
    /// True when the split is under way. The offer of a split-off stack comes back through these hooks with the keys
    /// perhaps still held, and must reach the game as it is. An offered stack stays in its bag, so units taken back are
    /// split off there too.
    /// </summary>
    private static bool Split(string itemKey, SplitKind kind, string fromGrid, State_Web_TradeUI state, OfferTarget target)
    {
        if (!long.TryParse(itemKey, out var stackId) || _offer?.StackId == stackId) return false;

        if (SplitScene.LiveItems()?.GetItemData(stackId) is not { } stack) return false;

        Action<long> onNewStack;

        if (TradeOffers.TakesBack(fromGrid))
        {
            if (TradeOffers.OfferedAt(state.GiveJson, stackId) is not { } offeredAt) return false;

            onNewStack = newStackId => _takeBack = new PendingTakeBack(newStackId, stackId, offeredAt, Time.unscaledTime);
        }
        else
        {
            onNewStack = newStackId => _offer = new PendingOffer(newStackId, stackId, target, Time.unscaledTime);
        }

        var at = new Cell(stack.BagPos.x, stack.BagPos.y);
        var request = new SplitRequest(stackId, stack.OwnerId, stack.OwnerId, kind, at, Cursor: null, IsRightClick: true, OnNewStack: onNewStack);

        return !SplitHooks.RunsOriginal(request);
    }

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

    /// <summary>The smaller offer put down where it lies, so the window counts what is left of it.</summary>
    private static void Recount(PendingTakeBack takeBack) => SendDrag(TradeOffers.DroneGrid, takeBack.OfferedId.ToString(), takeBack.OfferedAt);

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

    /// <summary>A plain drop of the split-off stack on the one it came from, which joins them.</summary>
    private static void Rejoin(PendingOffer offer)
    {
        var items = SplitScene.LiveItems();

        if (items?.GetItemData(offer.SourceId) is not { } source || items.GetItemData(offer.StackId) is not { } split) return;

        var at = new Cell(source.BagPos.x, source.BagPos.y);

        SplitHooks.RunsOriginal(new SplitRequest(split.InstanceId, split.OwnerId, source.OwnerId, SplitKind.Join, at, Cursor: at));
    }

    private static State_Web_TradeUI? OpenTrade()
    {
        var ui = GameSingleton.Existing<ReduxUISystem>();
        var screen = Il2CppType.Of<WebUI_TradeUI>();

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
