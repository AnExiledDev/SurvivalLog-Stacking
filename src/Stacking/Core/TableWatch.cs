using System.Collections.Generic;
using System.Linq;

namespace Stacking.Core;

/// <summary>
/// Which item and recipe tables the last full write went to: the game replaces a table object when it loads it again.
/// </summary>
public readonly record struct TableStamp(long Items, int ItemCount, long Recipes, int RecipeCount);

/// <summary>A watched item's limit as this mod wrote it, and the limit it carries now.</summary>
public readonly record struct Canary(int Written, int Now);

/// <summary>
/// Decides when the item table needs writing again. A full write reads every item through the game's runtime and
/// costs several milliseconds, so it runs only when a table was replaced, the limits changed, or one of a few
/// watched items lost the limit this mod gave it.
/// </summary>
public static class TableWatch
{
    public const int CanaryCount = 8;

    /// <summary>True when nothing about the tables has changed since the last full write. Runs every few seconds, so it makes nothing.</summary>
    public static bool IsUnchanged(TableStamp? last, TableStamp now, IReadOnlyList<Canary> canaries)
    {
        if (last != now) return false;

        for (var i = 0; i < canaries.Count; i++)
        {
            if (canaries[i].Written != canaries[i].Now) return false;
        }

        return true;
    }

    /// <summary>
    /// Up to <see cref="CanaryCount"/> raised items, spread across the id range, to check between full writes.
    /// Only raised items are watched: a reload puts the game's own limit back, which only shows on an item this mod raised.
    /// </summary>
    public static IReadOnlyList<int> PickCanaries(IEnumerable<int> raisedIds)
    {
        var ids = raisedIds.OrderBy(id => id).ToList();

        if (ids.Count <= CanaryCount) return ids;

        return Enumerable.Range(0, CanaryCount).Select(i => ids[i * (ids.Count - 1) / (CanaryCount - 1)]).ToList();
    }
}
