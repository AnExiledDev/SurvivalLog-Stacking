using System;
using System.Collections.Generic;
using System.Linq;

namespace Stacking.Core;

/// <summary>What an add of units as a new stack did to a container, judged from the container itself.</summary>
public abstract record AddResult
{
    private AddResult()
    {
    }

    /// <summary>One new stack holds exactly the units, and no stack already there changed.</summary>
    public sealed record Started(long StackId) : AddResult;

    /// <summary>
    /// Anything else, which is put right: the new stacks are taken away and the stacks already there that grew go back
    /// to their counts. Both are empty when the add put nothing in the container.
    /// </summary>
    /// <param name="NewStacks">Stacks that were not in the container before the add.</param>
    /// <param name="Grown">Stacks already there that hold more after the add, with their counts before it.</param>
    public sealed record Undo(IReadOnlyList<long> NewStacks, IReadOnlyDictionary<long, int> Grown) : AddResult
    {
        public bool DidNothing => NewStacks.Count == 0 && Grown.Count == 0;
    }
}

/// <summary>
/// An add can put units in and then throw (a listener of the game's, or another mod's patch), or return nothing for
/// units it did place, or top up a stack already there and spill the rest into a new one. What it returned or threw
/// therefore proves nothing; the container's counts before and after it do.
/// </summary>
public static class AddedUnits
{
    public static AddResult Judge(IReadOnlyDictionary<long, int> before, IReadOnlyDictionary<long, int> after, int units)
    {
        var newStacks = after.Keys.Where(id => !before.ContainsKey(id)).OrderBy(id => id).ToList();
        var grown = before
            .Where(stack => after.TryGetValue(stack.Key, out var now) && now > stack.Value)
            .ToDictionary(stack => stack.Key, stack => stack.Value);

        if (newStacks.Count == 1 && grown.Count == 0 && after[newStacks[0]] == units) return new AddResult.Started(newStacks[0]);

        return new AddResult.Undo(newStacks, grown);
    }

    /// <summary>
    /// The stacks of exactly the units that were not there before, oldest id first. On the ground every stack the game
    /// drops is a new one, so a single match is the add's own, and none means it dropped nothing there.
    /// </summary>
    public static IReadOnlyList<long> NewStacksOf(IReadOnlyDictionary<long, int> before, IReadOnlyDictionary<long, int> after, int units) =>
        after.Where(stack => !before.ContainsKey(stack.Key) && stack.Value == units).Select(stack => stack.Key).OrderBy(id => id).ToList();

    /// <summary>Units the container holds beyond what it held before, counting only stacks that are new or grew.</summary>
    public static int UnitsTooMany(IReadOnlyDictionary<long, int> before, IReadOnlyDictionary<long, int> after) =>
        after.Sum(stack => before.TryGetValue(stack.Key, out var was) ? Math.Max(0, stack.Value - was) : stack.Value);
}
