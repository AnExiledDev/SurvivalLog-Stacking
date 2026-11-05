using System;
using ExiledToolkit.Game;
using GameCore.HotUpdate.Battle.Logic;

namespace Stacking.Adapters;

/// <summary>
/// Adds units as a stack of their own. The game's add tops up any matching stack in the container first, wherever it
/// is; while an add runs through Start, the game finds no stack to top up. Splitting and the station rule both need
/// it, so it is patched once for whichever asks first. Static because Harmony patches are.
/// </summary>
internal static class NewStacks
{
    [ThreadStatic] private static bool _isStarting;

    private static bool? _isInstalled;

    /// <summary>True when every add made through Start starts its own stack.</summary>
    public static bool Apply(PatchKit hooks)
    {
        _isInstalled ??= hooks.Patch(typeof(NewStacks), typeof(ItemManager), nameof(ItemManager.FindStackableItem), postfix: nameof(NoStackWhileStarting),
            consequence: "starting a new stack");

        return _isInstalled.Value;
    }

    public static ItemData? Start(Func<ItemData?> add)
    {
        _isStarting = true;

        try
        {
            return add();
        }
        finally
        {
            _isStarting = false;
        }
    }

    private static void NoStackWhileStarting(ref ItemData? __result)
    {
        if (_isStarting) __result = null;
    }
}
