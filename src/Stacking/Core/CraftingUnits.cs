using System;
using System.Collections.Generic;

namespace Stacking.Core;

/// <summary>A unit a recipe's fill put on the bench, and the container it came from.</summary>
public readonly record struct BenchUnit(long UnitId, long FromOwnerId);

/// <summary>How taking a fill's units off their stacks went.</summary>
public abstract record UnitsTaken
{
    private UnitsTaken()
    {
    }

    /// <summary>
    /// Every unit landed. Most are on the bench already; a stack worn down to its last unit is left where it is as a
    /// whole pick, for the game's own move to carry onto the bench with the rest of its picks.
    /// </summary>
    public sealed record Landed(IReadOnlyList<BenchUnit> OnBench, IReadOnlyList<WholePick> Whole) : UnitsTaken;

    /// <summary>A unit did not land, so every unit already on the bench was sent back; StillOnBench names any that stayed.</summary>
    public sealed record PutBack(IReadOnlyList<string> Notes, IReadOnlyList<BenchUnit> StillOnBench) : UnitsTaken;
}

/// <summary>
/// Takes a fill's units onto the crafting bench one at a time, each off the stack the last one left behind, by the
/// station rule's own move of one unit. A fill is all or nothing: when a unit does not land, every unit already on the
/// bench goes back to the container it came from, so the containers are left as the fill found them.
/// </summary>
public static class CraftingUnits
{
    /// <param name="find">Where an item is now.</param>
    /// <param name="takeOne">The station rule's move of one unit off a stack (stack, its container) onto the bench.</param>
    /// <param name="moveBack">The game's move of a unit (unit, container) off the bench; true when the game says it moved.</param>
    public static UnitsTaken Take(
        Func<long, ItemLookup> find,
        IReadOnlyList<UnitTake> takes,
        long benchOwnerId,
        Func<long, long, StationReport> takeOne,
        Func<long, long, bool> moveBack)
    {
        var onBench = new List<BenchUnit>();
        var whole = new List<WholePick>();

        foreach (var take in takes)
        {
            if (TakeAll(find, take, benchOwnerId, takeOne, onBench, whole) is { } why) return PutBack(find, benchOwnerId, onBench, moveBack, why);
        }

        return new UnitsTaken.Landed(onBench, whole);
    }

    /// <summary>Sends every unit still on the bench back to its container, and says which stayed and why the fill stopped.</summary>
    public static UnitsTaken.PutBack PutBack(
        Func<long, ItemLookup> find,
        long benchOwnerId,
        IReadOnlyList<BenchUnit> onBench,
        Func<long, long, bool> moveBack,
        string why)
    {
        var notes = new List<string> { why };
        var stayed = new List<BenchUnit>();

        foreach (var unit in onBench)
        {
            switch (find(unit.UnitId))
            {
                case ItemLookup.Found { Item.OwnerId: var ownerId } when ownerId == benchOwnerId:
                    if (!SentBack(find, benchOwnerId, unit, moveBack, notes)) stayed.Add(unit);
                    break;
                case ItemLookup.Unreadable unreadable:
                    notes.Add($"unit {unit.UnitId} could not be read ({unreadable.Why}), so it may still be on the bench");
                    stayed.Add(unit);
                    break;
            }
        }

        return new UnitsTaken.PutBack(notes, stayed);
    }

    /// <summary>The game's answer and a fresh read must agree; a unit that cannot be read after the move counts as still on the bench.</summary>
    private static bool SentBack(Func<long, ItemLookup> find, long bench, BenchUnit unit, Func<long, long, bool> moveBack, List<string> notes)
    {
        var hasMoved = Try(() => moveBack(unit.UnitId, unit.FromOwnerId), $"moving unit {unit.UnitId} back to container {unit.FromOwnerId}", notes);

        return hasMoved && find(unit.UnitId) is ItemLookup.Found found && found.Item.OwnerId != bench;
    }

    /// <summary>Null when every unit of the take landed; otherwise why one did not. Units that reached the bench are noted either way.</summary>
    private static string? TakeAll(
        Func<long, ItemLookup> find,
        UnitTake take,
        long bench,
        Func<long, long, StationReport> takeOne,
        List<BenchUnit> onBench,
        List<WholePick> whole)
    {
        var stackId = take.StackId;

        for (var unit = 1; unit <= take.Units; unit++)
        {
            if (Where(find, stackId) is not { } stack || stack.OwnerId != take.OwnerId) return $"stack {stackId} is no longer in container {take.OwnerId}";

            if (stack.Count <= 1)
            {
                whole.Add(new WholePick(stackId, take.OwnerId));

                return unit == take.Units ? null : $"stack {stackId} ran out after {unit} of {take.Units} units";
            }

            var notes = new List<string>();
            var report = Try(() => takeOne(stackId, take.OwnerId), $"taking one unit off stack {stackId}", notes);
            var isOnBench = Where(find, stackId)?.OwnerId == bench;

            if (isOnBench) onBench.Add(new BenchUnit(stackId, take.OwnerId));

            if (!isOnBench || report is not { Outcome: StationOutcome.OneUnitIn, RestId: { } rest })
            {
                return notes.Count > 0 ? notes[0] : $"one unit of stack {stackId} did not land on bench {bench} ({report?.Outcome})";
            }

            stackId = rest;
        }

        return null;
    }

    private static Placed? Where(Func<long, ItemLookup> find, long itemId) => find(itemId) is ItemLookup.Found found ? found.Item : null;

    private static T? Try<T>(Func<T> call, string step, List<string> notes)
    {
        try
        {
            return call();
        }
        catch (Exception ex)
        {
            notes.Add($"{step} failed: {ex}");

            return default;
        }
    }
}
