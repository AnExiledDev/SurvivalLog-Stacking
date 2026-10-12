using System;
using System.Collections.Generic;
using GameCore.HotUpdate.Battle.Logic;

namespace Stacking.Adapters;

/// <summary>Changing a stack's count through the game's own calls, checked, so a step that did not land is noticed and undone.</summary>
internal static class StackCounts
{
    /// <summary>The game's count calls report whether a stack emptied, not whether they worked, so the count itself is checked.</summary>
    public static void Expect(ItemData item, int expected)
    {
        if (item.ItemCount != expected) throw new InvalidOperationException($"the stack holds {item.ItemCount}, expected {expected}");
    }

    /// <summary>Every stack in a container, by id, with its count.</summary>
    public static Dictionary<long, int> In(ItemManager items, long ownerId)
    {
        var counts = new Dictionary<long, int>();
        var inBag = items.GetItemDataList(ownerId);

        for (var i = 0; inBag != null && i < inBag.Count; i++)
        {
            if (inBag[i] is { } item) counts[item.InstanceId] = item.ItemCount;
        }

        return counts;
    }

    public static void Restore(ItemManager items, ItemData item, int count)
    {
        var missing = count - item.ItemCount;

        if (missing > 0) items.IncreaseItemCount(item, missing);
        else if (missing < 0) items.DecreaseItemCount(item, -missing);
    }
}
