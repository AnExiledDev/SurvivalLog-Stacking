using System;
using GameCore.HotUpdate;
using HarmonyLib;
using Stacking.Core;

namespace Stacking.Adapters;

/// <summary>
/// Lets a cooking bench count every unit of a stack, as it counts loose items. Two halves: the planner counts the
/// units (CookingPlan) and the finished cook takes them (CookingSettle). The planner half alone would cook several
/// dishes from one unit, so it runs only while the settle half is hooked and has not failed. The settle half alone
/// changes nothing, so it stays on for cooks planned before a failure.
/// </summary>
internal static class Cooking
{
    private const int MaxLoggedFailures = 3;

    private static Action<string> _log = _ => { };
    private static volatile bool _isCountingStacks;
    private static int _failures;

    public static bool IsCountingStacks => _isCountingStacks;

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        if (!CookingSettle.Apply(harmony, log))
        {
            log("cooking still counts a stack as one ingredient: a finished cook could not be hooked");

            return;
        }

        if (!CookingPlan.Apply(harmony, log))
        {
            log("cooking still counts a stack as one ingredient: the recipe planner could not be hooked");

            return;
        }

        _isCountingStacks = true;
        log("cooking counts every unit of a stack, and a finished cook uses up the units its dishes took");
    }

    public static ItemUse UseOf(Config_Item config) => new(config.StackLimit, config.Category);

    /// <summary>Any failure stops the planner counting units, so no later cook can plan units it might not take.</summary>
    public static void Stop(string what, Exception ex)
    {
        var wasCounting = _isCountingStacks;

        _isCountingStacks = false;
        _failures++;

        if (wasCounting) _log($"{what} failed, so cooking counts a stack as one ingredient until the game restarts: {ex}");
        else if (_failures <= MaxLoggedFailures) _log($"{what} failed (logged {_failures} of {MaxLoggedFailures}): {ex}");
    }
}
