using System;
using System.Reflection;
using HarmonyLib;

namespace Stacking.Adapters;

/// <summary>Patches game methods so that a missing or changed method costs the feature that needed it, logged, never the plugin.</summary>
internal static class Hooks
{
    public static bool TryPatch(Harmony harmony, Type patches, string what, Func<MethodBase?> target, Action<string> log,
        string? prefix = null, string? postfix = null, string? finalizer = null)
    {
        try
        {
            harmony.Patch(target() ?? throw new MissingMethodException(what),
                prefix: Patch(patches, prefix), postfix: Patch(patches, postfix), finalizer: Patch(patches, finalizer));

            return true;
        }
        catch (Exception ex)
        {
            log($"could not hook {what}: {ex.Message}");

            return false;
        }
    }

    /// <summary>
    /// Opens state when the method starts and closes it when the method ends, even when it throws. If the game will not
    /// take a finalizer, it closes on a normal return only, and the hooks reading the state must notice a stale one.
    /// </summary>
    public static bool TryPatchScope(Harmony harmony, Type patches, string what, Func<MethodBase?> target, Action<string> log, string open, string close) =>
        TryPatch(harmony, patches, what, target, log, prefix: open, finalizer: close)
        || TryPatch(harmony, patches, $"{what} (closing on a normal return only)", target, log, prefix: open, postfix: close);

    private static HarmonyMethod? Patch(Type patches, string? name) => name == null ? null : new HarmonyMethod(patches, name);
}
