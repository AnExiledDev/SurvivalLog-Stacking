using System;

namespace Stacking.Core;

/// <summary>What a stack would lose by merging: how long the item has before it spoils, and the uses it holds.</summary>
public readonly record struct Freshness(int StartTime, int TimeLeft, float TimeLeftFrac, float TimeScale, int UseTimes, int MaxUseTimes)
{
    /// <summary>The game gives an item that never spoils no time left on its clock.</summary>
    public bool Spoils => TimeLeft != 0;

    /// <summary>The hour on the game clock at which it spoils at its current rate.</summary>
    public float ExpiresAt => StartTime + TimeLeft + TimeLeftFrac;

    public bool HoldsSeveralUses => UseTimes > 1 || MaxUseTimes > 1;
}

public static class StackIdentity
{
    /// <summary>
    /// Shelf life closer than this counts as the same. Adding an item to a bag drops the fraction of an hour the game
    /// keeps on a stack, so two halves of one batch can differ by up to an hour.
    /// </summary>
    public const float ToleranceHours = 1f;

    public static bool CanShareStack(Freshness incoming, Freshness stack, float nowHours)
    {
        if (!HaveSameUses(incoming, stack) || incoming.Spoils != stack.Spoils) return false;

        if (!incoming.Spoils) return true;

        if (incoming.TimeScale <= 0f || stack.TimeScale <= 0f)
        {
            return incoming.TimeScale == stack.TimeScale && Math.Abs(incoming.ExpiresAt - stack.ExpiresAt) <= ToleranceHours;
        }

        return Math.Abs(ShelfHoursLeft(incoming, nowHours) - ShelfHoursLeft(stack, nowHours)) <= ToleranceHours;
    }

    /// <summary>A single-use item reads 0 or 1 depending on which game path made it; only items with several uses carry information here.</summary>
    private static bool HaveSameUses(Freshness a, Freshness b)
    {
        if (!a.HoldsSeveralUses && !b.HoldsSeveralUses) return true;

        return a.UseTimes == b.UseTimes && a.MaxUseTimes == b.MaxUseTimes;
    }

    /// <summary>
    /// Shelf life left, measured as the game's own portion merge measures it: clock hours to expiry times the spoil rate
    /// where the item sits, so a bag item and a fridge item with equal freshness compare equal.
    /// </summary>
    private static float ShelfHoursLeft(Freshness item, float nowHours) => (item.ExpiresAt - nowHours) * item.TimeScale;
}
