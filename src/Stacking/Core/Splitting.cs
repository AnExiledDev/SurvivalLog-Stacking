using System;
using System.Collections.Generic;
using System.Linq;

namespace Stacking.Core;

/// <summary>What the held modifier keys ask of a drag: Shift half, Ctrl one, Ctrl+Shift a chosen amount.</summary>
public enum SplitKind
{
    None,
    Half,
    One,
    Choose,
}

public readonly record struct Cell(int X, int Y);

/// <summary>The cells an item covers, from its top-left cell. The game never rotates items.</summary>
public readonly record struct Footprint(int X, int Y, int Width, int Height)
{
    public bool Contains(Cell cell) => cell.X >= X && cell.X < X + Width && cell.Y >= Y && cell.Y < Y + Height;

    public bool Overlaps(Footprint other) =>
        X < other.X + other.Width && other.X < X + Width && Y < other.Y + other.Height && other.Y < Y + Height;
}

/// <summary>Whether a stack already in the bag could take units of the dragged one.</summary>
public enum StackMatch
{
    OtherItem,
    OtherCondition,
    Same,
}

/// <summary>What decides whether two stacks of an item may become one: the game's own stack rules, plus freshness.</summary>
public readonly record struct StackFacts(int ConfigId, bool IsPolluted, bool IsMapPreset, bool HasOwnWeight, Freshness Freshness);

public sealed record GridItem(long Id, Footprint Cells, int Count, StackMatch Match);

/// <summary>
/// The bag a stack is dropped into. When it is the bag the stack came from, the stack itself is one of the items.
/// HasOwnRules marks a bag the game treats specially (a cooking bench, compost bin, rack or robot mold) on either end of the drag.
/// </summary>
public sealed record BagGrid(int Columns, int Rows, IReadOnlyList<GridItem> Items, bool HasOwnRules = false)
{
    public bool Fits(Footprint cells) => cells.X >= 0 && cells.Y >= 0 && cells.X + cells.Width <= Columns && cells.Y + cells.Height <= Rows;
}

/// <summary>A dragged stack: how many units it holds, its size, the top-left cell it was dropped at, and the cell under the cursor.</summary>
public sealed record StackDrag(long ItemId, int Count, int Width, int Height, Cell DropAt, Cell? Cursor);

public enum Refusal
{
    NoRoom,
    StackFull,
    TooHeavy,
    OtherCondition,
    OwnRules,
}

/// <summary>Where the game put a new stack: the game's add places it elsewhere when the cell is taken, and drops it when the container is full.</summary>
public enum Landing
{
    WhereDropped,
    ElsewhereInBag,
    OnTheGround,
}

public abstract record SplitPlan
{
    private SplitPlan()
    {
    }

    /// <summary>The game carries out the drag as it would without this mod.</summary>
    public sealed record Vanilla : SplitPlan;

    public sealed record Refused(Refusal Reason) : SplitPlan;

    /// <summary>Units leave the dragged stack and start a new one at a free spot.</summary>
    public sealed record NewStack(Cell At, int Units) : SplitPlan;

    /// <summary>Units leave the dragged stack and top up a matching one; all of them merges the two.</summary>
    public sealed record JoinStack(long StackId, int Units) : SplitPlan;

    /// <summary>Ask the player how many, up to Max; carried out by planning again with their answer.</summary>
    public sealed record Choose(int Max, int Suggested) : SplitPlan;
}

public static class Splitting
{
    public static SplitKind KindFor(bool isShiftHeld, bool isCtrlHeld) => (isShiftHeld, isCtrlHeld) switch
    {
        (true, true) => SplitKind.Choose,
        (true, false) => SplitKind.Half,
        (false, true) => SplitKind.One,
        _ => SplitKind.None,
    };

    /// <summary>
    /// A drop that touches nothing starts a new stack, which must leave at least one unit behind; a drop on a matching
    /// stack tops it up, up to the stack limit. Anything else is refused rather than handed to the game, because the
    /// player asked for a split and a plain move would surprise them.
    /// </summary>
    public static SplitPlan Plan(SplitKind kind, StackDrag drag, BagGrid? destination, int stackLimit, int unitsWeightAllows, int? chosenUnits = null)
    {
        if (kind == SplitKind.None || drag.Count < 2 || destination == null) return new SplitPlan.Vanilla();

        if (destination.HasOwnRules) return new SplitPlan.Refused(Refusal.OwnRules);

        var dropCells = new Footprint(drag.DropAt.X, drag.DropAt.Y, drag.Width, drag.Height);

        if (!destination.Fits(dropCells)) return new SplitPlan.Refused(Refusal.NoRoom);

        var touched = destination.Items.Where(item => item.Cells.Overlaps(dropCells)).ToList();

        if (touched.Count == 0)
        {
            return Units(kind, drag.Count, Math.Min(drag.Count - 1, unitsWeightAllows), chosenUnits, units => new SplitPlan.NewStack(drag.DropAt, units));
        }

        return Target(drag, touched) switch
        {
            null or { Match: StackMatch.OtherItem } => new SplitPlan.Refused(Refusal.NoRoom),
            { Match: StackMatch.OtherCondition } => new SplitPlan.Refused(Refusal.OtherCondition),
            var target when target.Count >= stackLimit => new SplitPlan.Refused(Refusal.StackFull),
            var target => Units(kind, drag.Count, Math.Min(Math.Min(drag.Count, stackLimit - target.Count), unitsWeightAllows), chosenUnits,
                units => new SplitPlan.JoinStack(target.Id, units)),
        };
    }

    /// <summary>
    /// The game stacks one item only with the same pollution, map-preset flag and quality values, and never stacks an
    /// item that carries its own weight. Items this mod made stackable must also match in freshness and uses, as the
    /// stack guard demands of every other add.
    /// </summary>
    public static StackMatch MatchOf(StackFacts dragged, StackFacts other, bool isSameQuality, bool isNewlyStackable, float? nowHours)
    {
        if (dragged.ConfigId != other.ConfigId) return StackMatch.OtherItem;

        var isSameCondition = !dragged.HasOwnWeight && !other.HasOwnWeight && isSameQuality
            && dragged.IsPolluted == other.IsPolluted && dragged.IsMapPreset == other.IsMapPreset;

        if (!isSameCondition) return StackMatch.OtherCondition;

        if (!isNewlyStackable) return StackMatch.Same;

        return nowHours is { } hours && StackIdentity.CanShareStack(dragged.Freshness, other.Freshness, hours) ? StackMatch.Same : StackMatch.OtherCondition;
    }

    /// <summary>
    /// How many units a bag can take before it is over its weight limit, by the game's own test (weight after the move
    /// above the limit is refused). Moving within one bag changes nothing.
    /// </summary>
    public static int UnitsWeightAllows(bool isSameBag, bool checksWeight, float maxWeight, float currentWeight, float unitWeight)
    {
        const float Rounding = 1e-4f;

        if (isSameBag || !checksWeight || unitWeight <= 0f) return int.MaxValue;

        var spare = maxWeight - currentWeight;

        if (spare <= 0f) return 0;

        return (int)Math.Min(int.MaxValue - 1, Math.Floor((spare + Rounding) / unitWeight));
    }

    public static string Explain(Refusal reason) => reason switch
    {
        Refusal.NoRoom => "No room to split the stack there",
        Refusal.StackFull => "That stack is already full",
        Refusal.TooHeavy => "That would be too heavy for the container",
        Refusal.OtherCondition => "Those don't stack: freshness or condition differs",
        Refusal.OwnRules => "Stacks can't be split into or out of this container; split them in your bag first",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "every refusal needs words for the player"),
    };

    /// <summary>A stack that left the container it was dropped in went to the ground; the add has nowhere else to put it.</summary>
    public static Landing LandingOf(long ownerId, Cell at, long droppedOwnerId, Cell droppedAt) =>
        ownerId != droppedOwnerId ? Landing.OnTheGround
        : at != droppedAt ? Landing.ElsewhereInBag
        : Landing.WhereDropped;

    /// <summary>What to tell the player about where the split-off units went; nothing when they went where they were dropped.</summary>
    public static string? Explain(Landing landing) => landing switch
    {
        Landing.WhereDropped => null,
        Landing.ElsewhereInBag => "That spot was taken, so the split-off stack went elsewhere in the container",
        Landing.OnTheGround => "No room in the container, so the split-off stack was dropped on the ground",
        _ => throw new ArgumentOutOfRangeException(nameof(landing), landing, "every landing needs words for the player"),
    };

    /// <summary>The stack under the cursor, or else the only stack the drop touches; never the dragged stack itself.</summary>
    private static GridItem? Target(StackDrag drag, List<GridItem> touched)
    {
        var others = touched.Where(item => item.Id != drag.ItemId).ToList();
        var underCursor = drag.Cursor is { } cursor ? others.Find(item => item.Cells.Contains(cursor)) : null;

        if (underCursor != null) return underCursor;

        return others.Count == 1 && touched.Count == 1 ? others[0] : null;
    }

    private static SplitPlan Units(SplitKind kind, int count, int max, int? chosenUnits, Func<int, SplitPlan> carryOut)
    {
        if (max <= 0) return new SplitPlan.Refused(Refusal.TooHeavy);

        if (chosenUnits is { } chosen) return carryOut(Math.Clamp(chosen, 1, max));

        var half = Math.Clamp(count / 2, 1, max);

        return kind switch
        {
            SplitKind.One => carryOut(1),
            SplitKind.Half => carryOut(half),
            SplitKind.Choose when max == 1 => carryOut(1),
            SplitKind.Choose => new SplitPlan.Choose(max, half),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "a drag without a modifier is never planned as a split"),
        };
    }
}
