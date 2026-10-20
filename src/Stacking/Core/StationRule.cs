namespace Stacking.Core;

/// <summary>What happens when a stack goes into a cooking or crafting station.</summary>
public enum StationEntry
{
    /// <summary>The game's own move, unchanged.</summary>
    Vanilla,

    /// <summary>One unit goes in; the rest stays where it was.</summary>
    OneUnit,
}

/// <summary>
/// A station counts what sits in each of its slots, and a stack in one slot counted as one ingredient, so a stack
/// never enters a station: moving a stack in moves one unit, and each further unit takes a slot of its own. Only
/// items this mod made stackable are held to it; the game already stacks the rest in its stations itself.
/// </summary>
public static class StationRule
{
    public static StationEntry Entry(bool isIntoStation, bool isWithinOneBag, int count, bool isNewlyStackable, bool hasOwnWeight) =>
        isIntoStation && !isWithinOneBag && count > 1 && isNewlyStackable && !hasOwnWeight ? StationEntry.OneUnit : StationEntry.Vanilla;

    /// <summary>
    /// A click on a stack in a station view feeds a container one unit at a time: the shredder hopper, the compressor
    /// bin and the dye vat take one unit per click whatever the item, the game's own stacks too, so the player chooses
    /// how much goes in. A drag still moves what it carries.
    /// </summary>
    public static StationEntry ClickEntry(bool isIntoFeeder, bool isWithinOneBag, int count, bool hasOwnWeight) =>
        isIntoFeeder && !isWithinOneBag && count > 1 && !hasOwnWeight ? StationEntry.OneUnit : StationEntry.Vanilla;

    /// <summary>
    /// How many units of a stack that arrived whole in a station go back where it came from. Moves that carry a whole
    /// bag at once go around the single-item move, so they are put right after the fact.
    /// </summary>
    public static int UnitsToSendBack(int count, bool isNewlyStackable, bool hasOwnWeight) =>
        isNewlyStackable && !hasOwnWeight && count > 1 ? count - 1 : 0;
}
