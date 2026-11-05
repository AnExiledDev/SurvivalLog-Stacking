using System;
using ExiledToolkit.Game;

namespace Stacking.Adapters;

/// <summary>Hooks that open state when a game method starts and close it when the method ends.</summary>
internal static class Hooks
{
    /// <summary>
    /// Opens state when the method starts and closes it when the method ends, even when it throws. If the game will not
    /// take a finalizer, it closes on a normal return only, and the hooks reading the state must notice a stale one.
    /// </summary>
    public static bool PatchScope(this PatchKit hooks, Type patches, Type type, string method, string what, string open, string close, Type[]? parameters = null) =>
        hooks.Patch(patches, type, method, parameters, prefix: open, finalizer: close, consequence: what)
        || hooks.Patch(patches, type, method, parameters, prefix: open, postfix: close, consequence: $"{what} (closing on a normal return only)");
}
