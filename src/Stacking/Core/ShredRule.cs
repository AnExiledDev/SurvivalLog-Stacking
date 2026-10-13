namespace Stacking.Core;

/// <summary>What a finished shred does to the item it just shredded.</summary>
public enum ShredRemoval
{
    /// <summary>The game's own removal, unchanged.</summary>
    Vanilla,

    /// <summary>One unit comes off the stack and the rest stays in the hopper for the next shred.</summary>
    OneUnit,
}

/// <summary>
/// The shredder gives the yield of one item per shred, then removes the whole item it shredded. For a stack that
/// throws away every unit but one, so a finished shred takes one unit off a stack instead, and the shredder goes on
/// to shred the rest one at a time, each with its own yield.
/// </summary>
public static class ShredRule
{
    public static ShredRemoval Removal(bool isSettlingShred, long removedItemId, long shreddedItemId, int count) =>
        isSettlingShred && removedItemId == shreddedItemId && count > 1 ? ShredRemoval.OneUnit : ShredRemoval.Vanilla;
}
