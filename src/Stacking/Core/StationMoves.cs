using System;
using System.Collections.Generic;

namespace Stacking.Core;

/// <summary>Where an item is and how many units it holds.</summary>
public readonly record struct Placed(long OwnerId, Cell At, int Count);

/// <summary>What reading an item found.</summary>
public abstract record ItemLookup
{
    private ItemLookup()
    {
    }

    public sealed record Found(Placed Item) : ItemLookup;

    /// <summary>The item no longer exists.</summary>
    public sealed record Gone : ItemLookup;

    /// <summary>The read itself failed, so where the item is and how many units it holds are unknown.</summary>
    public sealed record Unreadable(string Why) : ItemLookup;
}

/// <summary>
/// The game's items as the station steps see them. Any call but Find may throw, and a call that throws may or may not
/// have done its work, so the steps read the item again after every call instead of trusting it.
/// </summary>
public interface IStationStore
{
    /// <summary>Where the item is now. Never throws: a read that fails is Unreadable.</summary>
    ItemLookup Find(long itemId);

    /// <summary>Whether a container is a cooking or crafting station that holds this mod's stacks to one unit a slot.</summary>
    bool IsStation(long ownerId);

    /// <summary>Every stack in a container, by id, with its count.</summary>
    IReadOnlyDictionary<long, int> CountsIn(long ownerId);

    /// <summary>Sets a stack's count; never asked for less than one.</summary>
    void SetCount(long itemId, int count);

    /// <summary>The game's own move of an item into a container at a cell, which the game may refuse.</summary>
    void MoveTo(long itemId, long ownerId, Cell at);

    /// <summary>
    /// Adds the units as a stack, which the game places where it can, on the ground when the container has no room.
    /// Returns the stack it says it put them in, or null. Neither the answer nor a throw proves where the units went,
    /// so the steps judge the add from the container's counts and the ground's (see AddedUnits).
    /// </summary>
    long? AddStack(long ownerId, Cell at, int units);

    /// <summary>Every stack lying on the ground, each a drop of its own, by id, with its count.</summary>
    IReadOnlyDictionary<long, int> CountsOnGround();

    /// <summary>Gives a stack the add made the freshness the moved stack had before it moved.</summary>
    void CopyFreshness(long stackId);

    /// <summary>Removes a stack whole, from a container or the ground; only ever asked for a stack the add made.</summary>
    void Remove(long itemId);

    /// <summary>The free cell nearest a cell where this item fits, mold kept clear for food; null when none is free or the container cannot be read.</summary>
    Cell? FreeCell(long ownerId, long itemId, Cell near);
}

public enum StationOutcome
{
    /// <summary>The stack changed, or the target is no longer a station; nothing was touched.</summary>
    Skipped,

    /// <summary>The game took nothing; the stack is whole where it was.</summary>
    NothingMoved,

    /// <summary>One unit went into the station and the rest stayed where the stack stood.</summary>
    OneUnitIn,

    /// <summary>The rest could not be kept apart from the unit, so the unit came back and the stack is whole where it was.</summary>
    MoveUndone,

    /// <summary>Every unit is accounted for, but some went to another spot in the stack's own container.</summary>
    RestElsewhere,

    /// <summary>The units of a stack found in a station, all but one, went back to the container it came from.</summary>
    SentBack,

    /// <summary>A stack found in a station went back whole to the container it came from.</summary>
    StackMovedBack,

    /// <summary>A stack found in a station could not be taken out of it; no units were lost.</summary>
    StackStays,

    /// <summary>Units could not be put right, or could not be read after something changed; the notes say which and why.</summary>
    CountsWrong,

    /// <summary>The item could not be read before anything changed, so nothing was touched, and the game's move does not run either.</summary>
    Unreadable,
}

/// <summary>What the steps did, and every failure they caught along the way.</summary>
public sealed record StationReport(StationOutcome Outcome, IReadOnlyList<string> Notes)
{
    /// <summary>A failure was caught, or a stack is left where the rule says none may be.</summary>
    public bool NeedsAttention => Notes.Count > 0 || Outcome is StationOutcome.CountsWrong or StationOutcome.StackStays or StationOutcome.Unreadable;
}

/// <summary>
/// The steps that keep a stack of an item this mod made stackable out of a cooking or crafting station, so that a
/// station never holds more than one unit of it in a slot and no unit is ever made or lost on the way. Each step is
/// checked by reading the item again, and a step that did not land is put right before the next one runs. When the
/// item cannot be read at all the steps stop where they are: guessing its count could only make or lose units.
/// </summary>
public static class StationMoves
{
    /// <summary>
    /// The stack is cut to one unit and the game's own move runs again, so the game places the unit, refuses it, or
    /// shows why. The rest then starts a stack of its own where the stack stood. If the rest cannot be kept, the unit
    /// comes back and the stack is whole again; if even that fails, the rest goes anywhere in the bag that takes it.
    /// </summary>
    public static StationReport TakeOne(IStationStore store, long itemId, long fromOwnerId, long toOwnerId, Action replay)
    {
        var steps = new Steps(store, itemId);

        return steps.Guarded(() => steps.TakeOne(fromOwnerId, toOwnerId, replay));
    }

    /// <summary>
    /// A stack found in a station keeps one unit there and the rest goes back to the container it came from, near the
    /// cell it stood on. If the stack cannot be cut, it goes back whole.
    /// </summary>
    public static StationReport SendBack(IStationStore store, long itemId, long stationOwnerId, long toOwnerId, Cell near)
    {
        var steps = new Steps(store, itemId);

        return steps.Guarded(() => steps.SendBack(stationOwnerId, toOwnerId, near));
    }

    /// <summary>An item or container the steps could not read; it ends the steps where they are.</summary>
    private sealed class Blind : Exception
    {
        public Blind(string what) : base(what)
        {
        }
    }

    private sealed class Steps
    {
        private readonly IStationStore _store;
        private readonly long _itemId;
        private readonly List<string> _notes = new();
        private bool _hasWritten;

        public Steps(IStationStore store, long itemId)
        {
            _store = store;
            _itemId = itemId;
        }

        public StationReport Guarded(Func<StationReport> steps)
        {
            try
            {
                return steps();
            }
            catch (Blind blind)
            {
                _notes.Add($"{blind.Message}, so the steps stopped there and changed nothing more");

                return Done(_hasWritten ? StationOutcome.CountsWrong : StationOutcome.Unreadable);
            }
        }

        public StationReport TakeOne(long fromOwnerId, long toOwnerId, Action replay)
        {
            if (Find(_itemId) is not { Count: >= 2 } before || before.OwnerId != fromOwnerId) return Done(StationOutcome.Skipped);

            if (!Ask("checking the target is still a station", () => _store.IsStation(toOwnerId), fallback: false)) return Done(StationOutcome.Skipped);

            Write("cutting the stack to one unit", () => _store.SetCount(_itemId, 1));

            if (!IsIn(fromOwnerId, count: 1)) return Recover(before, StationOutcome.NothingMoved);

            Write("the game's move of one unit into the station", replay);

            if (IsIn(fromOwnerId)) return Recover(before, StationOutcome.NothingMoved);

            if (AddStack(fromOwnerId, before.At, before.Count - 1) is { } rest) return Done(Find(rest)?.OwnerId == fromOwnerId ? StationOutcome.OneUnitIn : StationOutcome.RestElsewhere);

            Write("moving the unit back out of the station", () => _store.MoveTo(_itemId, fromOwnerId, before.At));

            return Recover(before, StationOutcome.MoveUndone);
        }

        public StationReport SendBack(long stationOwnerId, long toOwnerId, Cell near)
        {
            if (Find(_itemId) is not { Count: >= 2 } before || before.OwnerId != stationOwnerId) return Done(StationOutcome.Skipped);

            if (Ask("checking where the stack goes back to", () => _store.IsStation(toOwnerId), fallback: false))
            {
                _notes.Add($"a stack of {before.Count} stays in station {stationOwnerId}: it came from another station ({toOwnerId}), so there is nowhere to send it");

                return Done(StationOutcome.StackStays);
            }

            var cell = Ask<Cell?>("finding a free cell", () => _store.FreeCell(toOwnerId, _itemId, near), fallback: null) ?? near;

            if (AddStack(toOwnerId, cell, before.Count - 1) is { } sent)
            {
                Write("leaving one unit in the station", () => _store.SetCount(_itemId, 1));

                if (Find(_itemId)?.Count == 1) return Done(StationOutcome.SentBack);

                if (!TakeBack(sent, before)) return Done(StationOutcome.CountsWrong);
            }

            Write("moving the stack back whole", () => _store.MoveTo(_itemId, toOwnerId, cell));

            return Done(IsIn(stationOwnerId) ? StationOutcome.StackStays : StationOutcome.StackMovedBack);
        }

        /// <summary>
        /// Puts the stack's count right in its own container. Units that cannot go back on it start a stack of their
        /// own anywhere the container takes them; only when that fails too are units lost, and the notes say so.
        /// </summary>
        private StationReport Recover(Placed before, StationOutcome outcome)
        {
            var now = Find(_itemId);

            if (now is { } placed && placed.OwnerId == before.OwnerId && placed.Count != before.Count)
            {
                Write("putting the stack's count back", () => _store.SetCount(_itemId, before.Count));

                now = Find(_itemId);
            }

            // A unit the game took and no longer shows was used by it, so it counts as placed.
            var missing = before.Count - (now?.Count ?? 1);

            if (missing <= 0) return Done(outcome);

            var cell = Ask<Cell?>("finding a free cell", () => _store.FreeCell(before.OwnerId, _itemId, before.At), fallback: null) ?? before.At;

            if (AddStack(before.OwnerId, cell, missing) != null) return Done(StationOutcome.RestElsewhere);

            _notes.Add($"{missing} of {before.Count} units could not be put back in container {before.OwnerId}, and are lost");

            return Done(StationOutcome.CountsWrong);
        }

        /// <summary>The stack now holding the units, given the moved stack's freshness; null when the units went nowhere.</summary>
        private long? AddStack(long ownerId, Cell at, int units)
        {
            var stack = AddJudged(ownerId, at, units);

            if (stack is { } holding) Write("copying the moved stack's freshness onto the new stack", () => _store.CopyFreshness(holding));

            return stack;
        }

        /// <summary>
        /// The new stack for the units, judged from the container's counts before and after the add, whatever the add
        /// returned or threw. Anything but one new stack of exactly the units is put right: new stacks are taken away,
        /// and a stack that was already there and grew is put back to its own count, never removed. A stack the add
        /// made outside the container is taken away too; only one that cannot be is kept as where the units went.
        /// </summary>
        private long? AddJudged(long ownerId, Cell at, int units)
        {
            if (Ask<IReadOnlyDictionary<long, int>?>("reading the container before adding", () => _store.CountsIn(ownerId), fallback: null) is not { } before) return null;

            var groundBefore = Ask<IReadOnlyDictionary<long, int>?>("reading the ground before adding", () => _store.CountsOnGround(), fallback: null);
            long? returned = null;
            var hasThrown = !Write("adding the units as a stack", () => returned = _store.AddStack(ownerId, at, units));
            var after = Ask<IReadOnlyDictionary<long, int>?>("reading the container after adding", () => _store.CountsIn(ownerId), fallback: null)
                ?? throw new Blind($"container {ownerId} could not be read after {units} units were added to it, so whether they are there is unknown");
            var kept = returned is { } id && !after.ContainsKey(id) ? TakeAwayStray(id, ownerId, units) : null;

            switch (AddedUnits.Judge(before, after, units))
            {
                case AddResult.Started started:
                    if (returned != started.StackId) _notes.Add($"the add of {units} units did not say it made stack {started.StackId}, which holds them, so that stack is kept");

                    return started.StackId;
                case AddResult.Undo { DidNothing: true }:
                    return kept ?? (hasThrown ? TakeAwayDropped(ownerId, units, groundBefore) : null);
                case AddResult.Undo undo:
                    PutRight(ownerId, units, before, undo);

                    return kept;
                default:
                    throw new InvalidOperationException("every add needs judging: " + ownerId);
            }
        }

        /// <summary>
        /// The add threw and put nothing in the container, but the game may have dropped the units on the ground before
        /// it threw: a container with no room drops them there, each stack as a drop of its own. One new stack of exactly
        /// the units on the ground is the add's, and several stop the steps. A ground that cannot be read is taken as
        /// holding none, so the units are put back where they came from rather than lost; the notes say to check it.
        /// </summary>
        private long? TakeAwayDropped(long ownerId, int units, IReadOnlyDictionary<long, int>? groundBefore)
        {
            var groundAfter = Ask<IReadOnlyDictionary<long, int>?>("reading the ground after adding", () => _store.CountsOnGround(), fallback: null);

            if (groundBefore == null || groundAfter == null)
            {
                _notes.Add($"the add of {units} units to container {ownerId} threw and the ground could not be read, so it is taken as having dropped nothing; check the ground near the player for a stack of {units}");

                return null;
            }

            var dropped = AddedUnits.NewStacksOf(groundBefore, groundAfter, units);

            return dropped.Count switch
            {
                0 => null,
                1 => TakeAwayStray(dropped[0], ownerId, units),
                _ => throw new Blind($"the add of {units} units to container {ownerId} threw and {dropped.Count} new stacks of {units} lie on the ground, so which one is the add's is unknown"),
            };
        }

        /// <summary>
        /// The add made a stack outside the container. Only a stack of exactly the units can be the add's own, and it is
        /// taken away; one that cannot be is returned, because the units are there and putting them back again would
        /// count them twice.
        /// </summary>
        private long? TakeAwayStray(long strayId, long ownerId, int units)
        {
            if (Find(strayId) is not { } stray) return null;

            if (stray.Count != units)
            {
                _notes.Add($"the add of {units} units meant for container {ownerId} named stack {strayId} in container {stray.OwnerId}, which holds {stray.Count}; it is left alone");

                return null;
            }

            Write("taking away a stack the add made outside the container", () => _store.Remove(strayId));

            if (Find(strayId) is not { } left) return null;

            _notes.Add($"a stack of {left.Count} the add made in container {left.OwnerId}, meant for {ownerId}, could not be taken away, so the units stay there");

            return strayId;
        }

        private void PutRight(long ownerId, int units, IReadOnlyDictionary<long, int> before, AddResult.Undo undo)
        {
            _notes.Add($"the add of {units} units to container {ownerId} did not start one stack of them ({undo.NewStacks.Count} new stacks, {undo.Grown.Count} stacks already there grew), so it is undone");

            foreach (var (stackId, countBefore) in undo.Grown) Write($"taking the units back off stack {stackId}", () => _store.SetCount(stackId, countBefore));

            foreach (var stackId in undo.NewStacks) Write($"taking away new stack {stackId}", () => _store.Remove(stackId));

            var now = Ask<IReadOnlyDictionary<long, int>?>("reading the container after undoing the add", () => _store.CountsIn(ownerId), fallback: null);

            if (now == null) _notes.Add($"container {ownerId} could not be read after the add was undone, so it may keep up to {units} units too many");
            else if (AddedUnits.UnitsTooMany(before, now) is > 0 and var tooMany) _notes.Add($"container {ownerId} keeps {tooMany} units too many");
        }

        /// <summary>Takes away a stack sent out of a station and puts the station's stack back whole; false when the units no longer add up.</summary>
        private bool TakeBack(long sent, Placed before)
        {
            Write("taking back the stack sent out", () => _store.Remove(sent));

            if (Find(sent) is { } stillThere)
            {
                _notes.Add($"{stillThere.Count} units were sent back out of the station but stayed in it too, so they are counted twice");

                return false;
            }

            if (Find(_itemId) is { } now && now.Count != before.Count) Write("putting the station stack's count back", () => _store.SetCount(_itemId, before.Count));

            if (Find(_itemId)?.Count == before.Count) return true;

            _notes.Add($"the stack in the station no longer holds its {before.Count} units");

            return false;
        }

        private bool IsIn(long ownerId, int? count = null) =>
            Find(_itemId) is { } placed && placed.OwnerId == ownerId && (count == null || placed.Count == count);

        /// <summary>Where an item is; null when it is gone. An item that cannot be read ends the steps (see Guarded).</summary>
        private Placed? Find(long itemId) =>
            _store.Find(itemId) switch
            {
                ItemLookup.Found found => found.Item,
                ItemLookup.Gone => null,
                ItemLookup.Unreadable unreadable => throw new Blind($"item {itemId} could not be read ({unreadable.Why})"),
                var other => throw new InvalidOperationException("every item lookup needs handling: " + other),
            };

        /// <summary>True when the call did not throw, which proves nothing about what it did.</summary>
        private bool Write(string step, Action call) =>
            Write<bool>(step, () =>
            {
                call();

                return true;
            }, fallback: false);

        /// <summary>A call that may change the game; once one has run, a failed read can no longer mean nothing changed.</summary>
        private T Write<T>(string step, Func<T> call, T fallback)
        {
            _hasWritten = true;

            return Ask(step, call, fallback);
        }

        private T Ask<T>(string step, Func<T> call, T fallback)
        {
            try
            {
                return call();
            }
            catch (Exception ex) when (ex is not Blind)
            {
                _notes.Add($"{step} failed: {ex}");

                return fallback;
            }
        }

        private StationReport Done(StationOutcome outcome) => new(outcome, _notes);
    }
}
