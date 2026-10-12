using System;
using System.Collections.Generic;
using GameCore.HotUpdate.Battle.Logic;
using InstanceIds = Il2CppSystem.Collections.Generic.List<long>;

namespace Stacking.Adapters;

/// <summary>
/// The stacks lying on the ground. An add whose cell is taken and whose bag has no room drops the units there
/// (DropItemManager.TryDropOverflowItemData), each stack as a drop of its own whose id becomes the stack's owner.
/// </summary>
internal static class GroundDrops
{
    /// <summary>Every stack on the ground, by item id, with its count. Throws when the world is not there to read.</summary>
    public static IReadOnlyDictionary<long, int> Counts()
    {
        var drops = Manager()?._dropItems ?? throw new InvalidOperationException("the ground could not be read: no world or no drop list");
        var counts = new Dictionary<long, int>();

        for (var i = 0; i < drops.Count; i++)
        {
            if (drops[i] is { HasItem: true } drop) counts[drop.Item.InstanceId] = drop.Item.ItemCount;
        }

        return counts;
    }

    /// <summary>Removes a stack whole, wherever it is: one on the ground with its drop, any other by the game's item removal.</summary>
    public static void RemoveStack(ItemManager items, long itemId)
    {
        if (!TryCollect(itemId)) items.ForceRemoveItem(itemId);
    }

    /// <summary>
    /// Takes a stack off the ground the way the game collects a drop, or false when the item lies in no drop. The
    /// game's item removal alone would leave the drop behind holding a pooled item, so the drop is collected whole:
    /// RemoveWaveLoot removes the item, clears the drop, takes it off the list, pools it and tells the world it was
    /// collected, for each drop it is given that is marked wave loot.
    /// </summary>
    private static bool TryCollect(long itemId)
    {
        var manager = Manager();
        var drop = manager == null ? null : Holding(manager, itemId);

        if (manager == null || drop == null) return false;

        var ids = new InstanceIds();
        var wasWaveLoot = drop.IsWaveLoot;
        drop.IsWaveLoot = true;

        ids.Add(drop.InstanceId);

        try
        {
            manager.RemoveWaveLoot(ids);
        }
        finally
        {
            // A drop still lying there gets its own flag back: the ground pick skips wave loot (OnGroundDockPickTo,
            // OnGroundDockPick) and selecting it goes another way (OnSelectModel).
            if (IsListed(manager, drop)) drop.IsWaveLoot = wasWaveLoot;
        }

        return true;
    }

    private static bool IsListed(DropItemManager manager, DropItem drop)
    {
        var drops = manager._dropItems;

        for (var i = 0; drops != null && i < drops.Count; i++)
        {
            if (drops[i] is { } listed && listed.Pointer == drop.Pointer) return true;
        }

        return false;
    }

    private static DropItemManager? Manager() => GameSingleton.Existing<BattleLogicWorld>()?._DropItemManager;

    private static DropItem? Holding(DropItemManager manager, long itemId)
    {
        var drops = manager._dropItems;

        for (var i = 0; drops != null && i < drops.Count; i++)
        {
            if (drops[i] is { HasItem: true } drop && drop.Item.InstanceId == itemId) return drop;
        }

        return null;
    }
}
