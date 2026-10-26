using System;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using Stacking.Core;

namespace Stacking.Adapters;

/// <summary>
/// The crafting table's and the cooking screen's bag refresh, held while this mod makes a run of moves. The game sends
/// one per item added or moved while the screen is open, and the crafting table's redraws the whole recipe list; a
/// fill that took units off stacks redrew it once per move. While held, each refresh is only noted, and one goes out
/// when the run ends. Both SendAction targets open mov [rsp+8],rbx; push rdi; sub rsp,20h; cmp byte [rip+x],0, 17
/// bytes before their first jump, so they have no relative jump or call in their first 14 bytes (Dobby rule; rva
/// 0x29e1ce0 and 0x29b0ad0). Static because Harmony patches are; main thread only.
/// </summary>
internal static class ScreenRefresh
{
    private static readonly HeldRefresh ToolTable = new();
    private static readonly HeldRefresh Cooking = new();

    private static Action<string> _log = _ => { };
    private static bool _hasReportedFailure;

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;
        Hooks.TryPatch(harmony, typeof(ScreenRefresh), "the crafting table's bag refresh",
            () => AccessTools.Method(typeof(Ac_ToolTable_RefreshBag), nameof(Ac_ToolTable_RefreshBag.SendAction)), log, prefix: nameof(HoldToolTable));
        Hooks.TryPatch(harmony, typeof(ScreenRefresh), "the cooking screen's bag refresh",
            () => AccessTools.Method(typeof(Ac_Cooking_RefreshBag), nameof(Ac_Cooking_RefreshBag.SendAction)), log, prefix: nameof(HoldCooking));
    }

    /// <summary>Holds both refreshes until the returned batch is disposed, which sends each one that was asked for.</summary>
    public static Batch Hold()
    {
        ToolTable.Hold();
        Cooking.Hold();

        return new Batch();
    }

    /// <summary>
    /// Ends a hold made by <see cref="Hold"/>. With <paramref name="isToolTableSentNext"/> the game sends the crafting
    /// table's refresh itself right after, so a held one is dropped rather than sent twice.
    /// </summary>
    public static void Release(bool isToolTableSentNext)
    {
        var isToolTableDue = ToolTable.Release(isToolTableSentNext);
        var isCookingDue = Cooking.Release();

        if (isToolTableDue) Send(Ac_ToolTable_RefreshBag.SendAction);
        if (isCookingDue) Send(Ac_Cooking_RefreshBag.SendAction);
    }

    private static bool HoldToolTable() => ToolTable.Pass();

    private static bool HoldCooking() => Cooking.Pass();

    /// <summary>Runs where a hold ends, so a failure is logged here rather than thrown over the moves' own result.</summary>
    private static void Send(Action sendAction)
    {
        try
        {
            sendAction();
        }
        catch (Exception ex)
        {
            if (!_hasReportedFailure) _log($"refreshing a station screen after a run of moves failed; it shows right after the next move or once reopened: {ex}");

            _hasReportedFailure = true;
        }
    }

    public readonly struct Batch : IDisposable
    {
        public void Dispose() => Release(isToolTableSentNext: false);
    }
}
