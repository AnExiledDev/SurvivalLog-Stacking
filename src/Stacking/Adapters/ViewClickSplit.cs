using System;
using System.Collections.Generic;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using Stacking.Core;

namespace Stacking.Adapters;

/// <summary>
/// Splitting a stack in the player's bag by right-clicking it with a modifier held, in the views that show the bag
/// beside a station. The bag screen's right-click watch waits for the bag screen, which these views never open; their
/// pages send every item click to the game instead, saying which button made it, so each view's click is caught on
/// its way in and the split takes its place. Which clicks split is the core's call (see ItemClicks). Static because
/// Harmony patches are.
/// </summary>
internal static class ViewClickSplit
{
    private const int MaxLoggedFailures = 3;

    /// <summary>
    /// Each view's item click. Every one opens mov [rsp+8],rbx; mov [rsp+10h],rsi; push rdi; sub rsp,20h, 15 bytes,
    /// before its first compare and short jump, so none has a relative jump or call in its first 14 bytes (Dobby rule).
    /// </summary>
    private static readonly (string View, Type Click)[] Views =
    {
        ("the crafting table", typeof(Ac_ToolTable_ItemClick)), // rva 0x2805d70
        ("brewing", typeof(Ac_Brew_ItemClick)), // rva 0x27acbe0
        ("cooking", typeof(Ac_Cooking_ItemClick)), // rva 0x27beb10
        ("the shredder", typeof(Ac_Shredder_ItemClick)), // rva 0x2800440
        ("the compressor", typeof(Ac_Compressor_ItemClick)), // rva 0x27b0340
        ("dyeing", typeof(Ac_Dye_ItemClick)), // rva 0x27cad40
        ("the drone hub", typeof(Ac_DroneHub_ItemClick)), // rva 0x27c9a90
        ("greenhouse building", typeof(Ac_GreenhouseBuild_ItemClick)), // rva 0x27d12b0
        ("the aid depot", typeof(Ac_AidDepot_ItemClick)), // rva 0x27a7860
        ("traps", typeof(Ac_TrapPanel_ItemClick)), // rva 0x28099c0
        ("the rat cage", typeof(Ac_RatCage_ItemClick)), // rva 0x27f49b0
        ("the heater", typeof(Ac_Heater_ItemClick)), // rva 0x27e7d40
        ("the fuel generator", typeof(Ac_FuelGenerator_ItemClick)), // rva 0x27ceb30
        ("the material rack", typeof(Ac_MaterialRack_ItemClick)), // rva 0x27ec5c0
    };

    private static Action<string> _log = _ => { };
    private static int _failures;

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        var splitsIn = new List<string>();

        foreach (var (view, click) in Views)
        {
            if (Hooks.TryPatch(harmony, typeof(ViewClickSplit), $"clicking an item in {view}",
                    () => AccessTools.Method(click, "SendAction", new[] { typeof(string) }), log, prefix: nameof(SplitInsteadOfClick)))
            {
                splitsIn.Add(view);
            }
        }

        log(splitsIn.Count == 0
            ? "a right-click with Shift or Ctrl does not split in any station view this session"
            : "a right-click with Shift or Ctrl also splits a stack in your bag in the views of " + string.Join(", ", splitsIn));
    }

    /// <summary>True when the view's own click should run instead, because this click splits nothing.</summary>
    private static bool SplitInsteadOfClick(string JsonData)
    {
        try
        {
            if (ItemClicks.ParseViewClick(JsonData) is not { } click) return true;

            var kind = SplitKeys.ForClick();
            var item = SplitScene.LiveItems()?.GetItemData(click.ItemId);

            if (item == null || !ItemClicks.SplitsInView(click, kind, item.OwnerId == PlayersBag())) return true;

            var at = new Cell(item.BagPos.x, item.BagPos.y);

            return SplitHooks.RunsOriginal(new SplitRequest(item.InstanceId, item.OwnerId, item.OwnerId, kind, at, Cursor: null, IsRightClick: true));
        }
        catch (Exception ex)
        {
            Fail(ex);

            return true;
        }
    }

    /// <summary>The game's own test for the player's bag (ItemManager.IsPlayerOwnedContainer) compares with this id.</summary>
    private static long? PlayersBag() => GameSingleton.Existing<BattleLogicWorld>()?._AgentManager?.LeadingRoleId;

    private static void Fail(Exception ex)
    {
        _failures++;

        if (_failures <= MaxLoggedFailures) _log($"splitting a right-clicked stack in a station view failed, the click does what the game does (logged {_failures} of {MaxLoggedFailures}): {ex}");
    }
}
