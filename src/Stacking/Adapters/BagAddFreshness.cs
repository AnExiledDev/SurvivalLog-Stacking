using System;
using System.Collections.Generic;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;
using Stacking.Core;

namespace Stacking.Adapters;

/// <summary>
/// Gives a stack topped up by an add the shelf life the add brought, at the stack's own speed of decay
/// (<see cref="TopUpFreshness"/>). The stacks the add's search finds are noted, and once the add is done each one the
/// game wrote at the wrong speed is set back to the add's speed and converted by the game itself. Static because
/// Harmony patches are.
/// </summary>
internal static class BagAddFreshness
{
    private const int MaxLoggedFailures = 3;
    private const int MaxLoggedFixes = 5;

    /// <summary>One add tops up at most a few stacks; more means a list left behind by an add that threw.</summary>
    private const int MaxNotedPerAdd = 32;

    /// <summary>The stacks found by the add running on this thread, with the speed each had when found.</summary>
    [ThreadStatic] private static List<(ItemData Stack, float TimeScale)>? _found;

    private static Action<string> _log = _ => { };
    private static Action<string> _logFileOnly = _ => { };
    private static int _failures;
    private static int _fixesLogged;

    public static void Apply(Harmony harmony, Action<string> log, Action<string> logFileOnly)
    {
        _log = log;
        _logFileOnly = logFileOnly;

        // The search is noted only once the add is hooked, so stacks are noted only while an add runs. An add that
        // throws leaves its list behind, never read again; NoteFound caps how much it can hold.
        if (!Hooks.TryPatch(harmony, typeof(BagAddFreshness), "adding to a bag, for shelf life in a cooler bag",
                () => AccessTools.Method(typeof(ItemManager), nameof(ItemManager.TryAddItemToBag)), log, prefix: nameof(OpenAdd), postfix: nameof(CloseAdd)))
        {
            log("a stack topped up in a cooler bag may lose shelf life, as in the base game");

            return;
        }

        if (Hooks.TryPatch(harmony, typeof(BagAddFreshness), "the stack search, for shelf life in a cooler bag",
                () => AccessTools.Method(typeof(ItemManager), nameof(ItemManager.FindStackableItem)), log, postfix: nameof(NoteFound)))
        {
            log("a stack topped up in a cooler bag keeps the shelf life the added items had");
        }
    }

    private static void OpenAdd(out List<(ItemData Stack, float TimeScale)>? __state)
    {
        __state = _found;
        _found = new List<(ItemData Stack, float TimeScale)>();
    }

    private static void NoteFound(ItemData? __result)
    {
        if (__result == null || _found == null || _found.Count >= MaxNotedPerAdd) return;

        try
        {
            _found.Add((__result, __result.TimeScale));
        }
        catch (Exception ex)
        {
            Fail("noting a stack", ex);
        }
    }

    private static void CloseAdd(ItemManager __instance, int startTime, int timeLeft, float timeScale, List<(ItemData Stack, float TimeScale)>? __state)
    {
        var found = _found;

        _found = __state;

        if (found == null || found.Count == 0) return;

        try
        {
            foreach (var (stack, stackScale) in found) Rescale(__instance, stack, stackScale, startTime, timeLeft, timeScale);
        }
        catch (Exception ex)
        {
            Fail("fixing a topped-up stack's shelf life", ex);
        }
    }

    private static void Rescale(ItemManager items, ItemData stack, float stackScale, int startTime, int timeLeft, float timeScale)
    {
        if (stack.TimeScale != stackScale
            || !TopUpFreshness.NeedsRescale(stack.StartTime, stack.TimeLeft, stackScale, startTime, timeLeft, timeScale)) return;

        // The stack now holds the add's values, which were given at the add's speed with no fraction of an hour.
        stack.TimeScale = timeScale;
        stack.TimeLeftFrac = 0f;
        items.SetItemTimeScale(stack, stackScale);

        if (_fixesLogged < MaxLoggedFixes)
        {
            _fixesLogged++;
            _logFileOnly($"item {stack.ItemConfigId}: a topped-up stack's time left of {timeLeft}h at x{timeScale:0.##} is {stack.TimeLeft + stack.TimeLeftFrac:0.0}h at x{stackScale:0.##}");
        }
    }

    private static void Fail(string what, Exception ex)
    {
        _failures++;

        if (_failures <= MaxLoggedFailures) _log($"{what} failed (logged {_failures} of {MaxLoggedFailures}): {ex}");
    }
}
