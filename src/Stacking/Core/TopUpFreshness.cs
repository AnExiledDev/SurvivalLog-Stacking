using System;

namespace Stacking.Core;

/// <summary>
/// When the game tops up a stack in a bag, it writes the added item's start time and time left onto the stack but
/// keeps the stack's speed of decay. Those values were given at the add's own speed (a harvest gives them at the field
/// speed of 1), so in a bag that slows decay, such as a backpack with the refrigerated upgrade, the stack ends up
/// with less shelf life than either part had.
/// </summary>
public static class TopUpFreshness
{
    private const float SameScale = 0.001f;

    /// <summary>
    /// True when the stack now holds the add's start time and time left at a speed of decay other than the add's, so
    /// its time left must be read at the add's speed and converted to the stack's.
    /// </summary>
    public static bool NeedsRescale(int stackStartTime, int stackTimeLeft, float stackTimeScale, int addStartTime, int addTimeLeft, float addTimeScale) =>
        addTimeLeft != 0
        && addTimeScale > 0f
        && stackTimeScale > 0f
        && stackStartTime == addStartTime
        && stackTimeLeft == addTimeLeft
        && Math.Abs(stackTimeScale - addTimeScale) > SameScale;
}
