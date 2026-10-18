using Stacking.Adapters;

namespace Stacking;

/// <summary>
/// For other mods, found by name so they run without this one: takes one unit off a stack and moves it to another
/// container by the game's own quick move, leaving the rest where it was. Call it on the main thread.
/// </summary>
public static class UnitMoves
{
    /// <summary>
    /// True when the unit left, false when it stayed. Null when nothing was tried and the caller should move the item
    /// itself: a single item, a stack that changed, or no game loaded.
    /// </summary>
    public static bool? MoveOne(long itemId, long fromOwnerId, long toOwnerId) => StationGuard.MoveOne(itemId, fromOwnerId, toOwnerId);
}
