using System;
using System.Collections.Generic;
using System.Diagnostics;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using Stacking.Core;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>
/// Splitting a stack by right-clicking it with a modifier held: Shift splits off half, Ctrl one, Ctrl+Shift asks how
/// many. The bag page cannot read modifier keys, so the ticker watches the mouse and keys, and the item comes from
/// what the page sends for that click. With one bag open the page sends nothing for a right-click, and nothing in the
/// game knows which item the cursor is over, so the ticker presses the drop key, whose answer names the item under
/// the tooltip; which answers are this mod's is the core's call (see DropKeyProbes), and an answer to this mod's
/// press never drops anything. With two bags open a plain right-click sends the item over, and that send is taken as
/// the split. A held highlight key makes the page send every item of a kind instead, which stays the game's.
/// Static because Harmony patches are.
/// </summary>
internal static class RightClickSplit
{
    private const long ClickExpiryMs = 500;
    private const int MaxLoggedFailures = 3;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly object Gate = new();

    /// <summary>The split each outstanding press of this mod asked for, oldest first; never longer than the presses owed an answer.</summary>
    private static readonly Queue<SplitKind> Asked = new();

    private static Action<string> _log = _ => { };
    private static DropKeyProbes _probes = DropKeyProbes.None;
    private static (SplitKind Kind, long At)? _click;
    private static bool _canProbe;
    private static int _failures;

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        var canSeeKeys = Hooks.TryPatch(harmony, typeof(RightClickSplit), "noticing the drop key",
            () => AccessTools.Method(typeof(Reducer_Web_BackpackUI), nameof(Reducer_Web_BackpackUI.RA_DropKey)), log, prefix: nameof(NoteDropKey));

        // Without seeing every drop key, the player's own could not be told from this mod's, so no press is sent.
        _canProbe = canSeeKeys && Hooks.TryPatch(harmony, typeof(RightClickSplit), "dropping an item from the bag",
            () => AccessTools.Method(typeof(Reducer_Web_BackpackUI), nameof(Reducer_Web_BackpackUI.RA_DropItem)), log, prefix: nameof(SplitInsteadOfDrop));

        var canCatchTransfer = Hooks.TryPatch(harmony, typeof(RightClickSplit), "sending an item to the other bag",
            () => AccessTools.Method(typeof(Reducer_Web_BackpackUI), nameof(Reducer_Web_BackpackUI.RA_QuickTransferItem)), log, prefix: nameof(SplitInsteadOfTransfer));

        if (!_canProbe) log("a right-click with Shift or Ctrl does not split with one bag open this session");
        if (!canCatchTransfer) log("a right-click with Shift or Ctrl does not split with two bags open this session");
    }

    /// <summary>Once per frame, on the main thread, after the keys are sampled and a closed bag is noticed.</summary>
    public static void Watch()
    {
        if (!Input.GetMouseButtonDown(1)) return;

        try
        {
            var kind = SplitKeys.Current();

            if (kind == SplitKind.None || BackpackScreen.Open() is not { } bag) return;

            if (!bag.IsDualBag)
            {
                if (_canProbe) Probe(kind);
            }
            else if (bag.IsHighlightKeyHeld?.Value != true)
            {
                _click = (kind, Clock.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            Fail("noticing a right-click", ex);
        }
    }

    /// <summary>Once per frame, before a new click is watched for: a press left unanswered too long no longer decides anything (see DropKeyProbes).</summary>
    public static void Expire()
    {
        lock (Gate)
        {
            _probes = _probes.Expired(Time.frameCount, Clock.ElapsedMilliseconds);

            Forget();
        }
    }

    /// <summary>The bag screen closed: no press of this mod is owed an answer any more.</summary>
    public static void Closed()
    {
        lock (Gate)
        {
            _probes = _probes.Closed();

            Forget();
        }

        _click = null;
    }

    /// <summary>The press is counted before it is sent, so a page that answers at once still finds it owed.</summary>
    private static void Probe(SplitKind kind)
    {
        lock (Gate)
        {
            _probes = _probes.Probed(Time.frameCount, Clock.ElapsedMilliseconds);

            Asked.Enqueue(kind);
        }

        Ac_BackpackUI_DropKey.SendAction(false);
    }

    /// <summary>Observes every drop key that reaches the game, this mod's and the player's alike; it changes nothing.</summary>
    private static void NoteDropKey()
    {
        lock (Gate)
        {
            _probes = _probes.KeySeen();

            Forget();
        }
    }

    /// <summary>The answer to this mod's press names the item to split and never drops it; the player's drop key drops as the game drops.</summary>
    private static bool SplitInsteadOfDrop(Ac_BackpackUI_DropItem ac, State_Web_BackpackUI state, ref State_Web_BackpackUI __result)
    {
        var (reply, kind) = Answer();

        switch (reply)
        {
            case DropKeyReply.Drop:
                return true;
            case DropKeyReply.Split:
                if (kind is { } asked) SplitOff(ac?.ItemId, asked);
                break;
            case DropKeyReply.Swallow:
                break;
            default:
                throw new InvalidOperationException("every answer to a drop key needs handling: " + reply);
        }

        __result = state;

        return false;
    }

    private static (DropKeyReply Reply, SplitKind? Kind) Answer()
    {
        bool isOpen;

        try
        {
            isOpen = BackpackScreen.Open() != null;
        }
        catch (Exception ex)
        {
            // A screen that cannot be read counts as closed, which never splits and never drops on this mod's account.
            Fail("reading whether the bag is open", ex);

            isOpen = false;
        }

        lock (Gate)
        {
            var (next, reply) = _probes.Replied(isOpen);
            SplitKind? kind = reply == DropKeyReply.Split && Asked.Count > 0 ? Asked.Dequeue() : null;

            _probes = next;

            Forget();

            return (reply, kind);
        }
    }

    /// <summary>Drops the asked splits no press is owed an answer for any more, oldest first. Callers hold the gate.</summary>
    private static void Forget()
    {
        while (Asked.Count > _probes.Outstanding) Asked.Dequeue();
    }

    private static bool SplitInsteadOfTransfer(Ac_BackpackUI_QuickTransferItem ac, State_Web_BackpackUI state, ref State_Web_BackpackUI __result)
    {
        if (TakeClick() is not { } kind || SplitOff(ac?.ItemId, kind)) return true;

        __result = state;

        return false;
    }

    private static SplitKind? TakeClick()
    {
        if (_click is not { } seen) return null;

        _click = null;

        return Clock.ElapsedMilliseconds - seen.At <= ClickExpiryMs ? seen.Kind : null;
    }

    /// <summary>True when the game's own action should run instead, because there is nothing to split.</summary>
    private static bool SplitOff(long? itemId, SplitKind kind)
    {
        try
        {
            var item = itemId is { } id ? SplitScene.LiveItems()?.GetItemData(id) : null;

            if (item == null) return true;

            var at = new Cell(item.BagPos.x, item.BagPos.y);

            return SplitHooks.RunsOriginal(new SplitRequest(item.InstanceId, item.OwnerId, item.OwnerId, kind, at, Cursor: null, IsRightClick: true));
        }
        catch (Exception ex)
        {
            Fail("splitting a right-clicked stack", ex);

            return true;
        }
    }

    private static void Fail(string what, Exception ex)
    {
        _failures++;

        if (_failures <= MaxLoggedFailures) _log($"{what} failed, the click does what the game does (logged {_failures} of {MaxLoggedFailures}): {ex}");
    }
}
