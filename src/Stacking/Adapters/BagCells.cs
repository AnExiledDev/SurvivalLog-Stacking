using System;
using System.Collections.Generic;
using GameCore.HotUpdate.Battle.Logic;
using Stacking.Core;
using UnityEngine;
using PollutedCells = Il2CppSystem.Collections.Generic.HashSet<UnityEngine.Vector2Int>;

namespace Stacking.Adapters;

/// <summary>A container's grid as the game lays it out, and the cells an item may not go on.</summary>
internal readonly record struct BagLayout(int Columns, int Rows, IReadOnlyCollection<Cell> Blocked);

/// <summary>
/// Reads where units may be put in a container: its grid, checked against the grid the bag screen draws, and for food
/// the moldy cells, which the game itself keeps food off. A container that cannot be read is not guessed at; the
/// caller puts nothing there.
/// </summary>
internal static class BagCells
{
    /// <summary>The layout for an item of this kind; null, with why, when it cannot be read.</summary>
    public static BagLayout? Read(ItemManager items, long ownerId, int configId, out string why)
    {
        var size = items.GetOwnerBagSize(ownerId);

        if (size.x <= 0 || size.y <= 0)
        {
            why = $"container {ownerId} has no grid ({size.x}x{size.y})";

            return null;
        }

        if (BackpackScreen.DrawnGrid(ownerId) is { } drawn && (drawn.Columns != size.x || drawn.Rows != size.y))
        {
            why = $"container {ownerId} is laid out {size.x}x{size.y} but drawn {drawn.Columns}x{drawn.Rows}";

            return null;
        }

        if (Mold(ownerId, configId, size.x, size.y, out why) is not { } blocked) return null;

        return new BagLayout(size.x, size.y, blocked);
    }

    /// <summary>The moldy cells inside the grid when the item is food, from the table the game packs food around.</summary>
    private static IReadOnlyCollection<Cell>? Mold(long ownerId, int configId, int columns, int rows, out string why)
    {
        why = "";

        if (!FoodPollutionManager.IsFoodItem(configId)) return Array.Empty<Cell>();

        var pollution = GameSingleton.Existing<BattleLogicWorld>()?._FoodPollutionManager;

        if (pollution == null)
        {
            why = $"the mold in container {ownerId} is unknown: the game's mold manager is missing";

            return null;
        }

        var polluted = pollution.GetPollutedCells(ownerId);

        if (polluted == null) return Array.Empty<Cell>();

        if (polluted.TryCast<PollutedCells>() is not { } cells)
        {
            why = $"the mold in container {ownerId} is unknown: the game returned it in a form this version cannot read";

            return null;
        }

        var mold = new List<Cell>();

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                if (cells.Contains(new Vector2Int(x, y))) mold.Add(new Cell(x, y));
            }
        }

        return mold;
    }
}
