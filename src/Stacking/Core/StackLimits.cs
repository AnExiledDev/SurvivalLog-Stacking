using System;
using System.Collections.Generic;
using System.Linq;

namespace Stacking.Core;

/// <summary>
/// One item as the game's table defines it: its own stack limit, how many uses one item holds, and its shelf life,
/// which is 0 or below for an item that never spoils.
/// </summary>
public readonly record struct ItemRow(int Id, int Category, int StackLimit, int Uses, int Life = 1);

/// <summary>The limit to write per item id, and which of those items the game never stacked before.</summary>
public sealed record StackPlan(IReadOnlyDictionary<int, int> Limits, IReadOnlySet<int> NewlyStackable);

public static class StackLimits
{
    public const int Highest = 999;

    /// <summary>The game's ItemCategory.Seed.</summary>
    private const int SeedCategory = 10;

    public static int Clamp(int wanted) => Math.Clamp(wanted, 0, Highest);

    /// <summary>
    /// Seeds whose kind never spoils. Their freshness values still differ by how they were picked up, which split one
    /// kind of seed into several stacks; with nothing to lose, every seed of one kind may share a stack. Food keeps the
    /// freshness rule even when it never spoils.
    /// </summary>
    public static IReadOnlySet<int> NeverSpoiling(IEnumerable<ItemRow> gameRows) =>
        gameRows.Where(row => row.Category == SeedCategory && row.Life <= 0).Select(row => row.Id).ToHashSet();

    /// <summary>
    /// Raises each item to its category's limit. An item the game never stacked is only raised when the freshness
    /// guard is in place, because the game's own stack search ignores freshness and would merge old food into new.
    /// </summary>
    public static StackPlan Plan(IEnumerable<ItemRow> gameRows, IReadOnlyDictionary<int, int> wantedByCategory, bool canGuardFreshness)
    {
        var limits = new Dictionary<int, int>();
        var newlyStackable = new HashSet<int>();

        foreach (var row in gameRows)
        {
            if (!HasSingleUse(row) || !wantedByCategory.TryGetValue(row.Category, out var wanted) || wanted <= row.StackLimit) continue;

            var isNew = row.StackLimit <= 1;

            if (isNew && !canGuardFreshness) continue;

            limits[row.Id] = wanted;

            if (isNew) newlyStackable.Add(row.Id);
        }

        return new StackPlan(limits, newlyStackable);
    }

    /// <summary>One line per category, so a player can see the game's own limits before choosing theirs.</summary>
    public static IEnumerable<string> Describe(IEnumerable<ItemRow> gameRows, StackPlan plan, Func<int, string> categoryName)
    {
        foreach (var category in gameRows.GroupBy(row => row.Category).OrderBy(group => group.Key))
        {
            var lowest = category.Min(row => row.StackLimit);
            var highest = category.Max(row => row.StackLimit);
            var raised = category.Count(row => plan.Limits.ContainsKey(row.Id));
            var newlyStackable = category.Count(row => plan.NewlyStackable.Contains(row.Id));
            var kept = category.Count(row => !HasSingleUse(row));
            var gameLimit = lowest == highest ? $"{lowest}" : $"{lowest}-{highest}";

            yield return $"{categoryName(category.Key)}: {category.Count()} items, game limit {gameLimit}, {raised} raised ({newlyStackable} newly stackable), {kept} kept for having uses";
        }
    }

    /// <summary>
    /// Items with portions or uses keep their limit: the game merges their portions itself, and only while the limit is 1.
    /// </summary>
    private static bool HasSingleUse(ItemRow row) => row.Uses is 0 or 1;
}
