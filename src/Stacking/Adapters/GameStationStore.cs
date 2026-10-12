using System;
using System.Collections.Generic;
using GameCore.HotUpdate.Battle.Logic;
using Stacking.Core;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>
/// The game's items as the station steps see them, for one stack: its units are added back with the freshness it had
/// before it moved. Every call goes through the game's own item calls, and every check is the core's.
/// </summary>
internal sealed class GameStationStore : IStationStore
{
    private readonly ItemManager _items;
    private readonly StackCopy _stack;
    private readonly Action<string> _log;

    public GameStationStore(ItemManager items, StackCopy stack, Action<string> log)
    {
        _items = items;
        _stack = stack;
        _log = log;
    }

    public ItemLookup Find(long itemId)
    {
        try
        {
            return _items.GetItemData(itemId) is { } item
                ? new ItemLookup.Found(new Placed(item.OwnerId, new Cell(item.BagPos.x, item.BagPos.y), item.ItemCount))
                : new ItemLookup.Gone();
        }
        catch (Exception ex)
        {
            return new ItemLookup.Unreadable(ex.ToString());
        }
    }

    public bool IsStation(long ownerId) => Stations.IsStation(_items, ownerId);

    public IReadOnlyDictionary<long, int> CountsIn(long ownerId) => StackCounts.In(_items, ownerId);

    public void SetCount(long itemId, int count)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "a stack never holds less than one unit");

        var item = Existing(itemId);

        StackCounts.Restore(_items, item, count);
        StackCounts.Expect(item, count);
    }

    public void MoveTo(long itemId, long ownerId, Cell at) =>
        _items.OnMoveItem(Existing(itemId).OwnerId, itemId, ownerId, new Vector2Int(at.X, at.Y));

    /// <summary>A throw from the add is passed on as it is: the steps judge from the container whether the units went in anyway.</summary>
    public long? AddStack(long ownerId, Cell at, int units)
    {
        if (_stack.AddNewStack(_items, ownerId, at, units) is not { } added) return null;

        try
        {
            _stack.CopyFreshnessTo(added);
        }
        catch (Exception ex)
        {
            _log($"copying freshness onto {units} units sent to container {ownerId} failed, they keep the freshness the game gave them: {ex}");
        }

        return added.InstanceId;
    }

    public void Remove(long itemId) => _items.ForceRemoveItem(itemId);

    public Cell? FreeCell(long ownerId, long itemId, Cell near)
    {
        if (BagCells.Read(_items, ownerId, _stack.ConfigId, out var why) is not { } layout)
        {
            _log($"no free cell looked for: {why}");

            return null;
        }

        var taken = new List<GridItem>();
        var inBag = _items.GetItemDataList(ownerId);

        for (var i = 0; inBag != null && i < inBag.Count; i++)
        {
            if (inBag[i] is { } item) taken.Add(new GridItem(item.InstanceId, new Footprint(item.BagPos.x, item.BagPos.y, item.ItemSize.x, item.ItemSize.y), item.ItemCount, StackMatch.OtherItem));
        }

        var size = _items.GetItemData(itemId)?.ItemSize ?? _stack.Size;

        return Splitting.NearestFreeCell(new BagGrid(layout.Columns, layout.Rows, taken, Blocked: layout.Blocked), size.x, size.y, near);
    }

    private ItemData Existing(long itemId) => _items.GetItemData(itemId) ?? throw new InvalidOperationException($"item {itemId} is gone");
}
