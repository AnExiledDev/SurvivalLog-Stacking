using System;
using System.Collections.Concurrent;
using GameCore.HotUpdate.Battle.Logic;
using Stacking.Core;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>
/// Carries out the station rule through the core's steps (see StationMoves), which check every step and put right any
/// that did not land: one unit goes into a station by the game's own move, so every check the game runs still runs,
/// and the rest stays where it was. A stack found in a station keeps one unit there and the rest goes back.
/// </summary>
internal sealed class StationMover
{
    private readonly Action<string> _log;
    private readonly Action<string> _logFileOnly;

    /// <summary>Bags to redraw on the next frame: a move carried out inside the game's quick move must not send the game more actions from there.</summary>
    private readonly ConcurrentQueue<(long From, long To)> _redraws = new();

    public StationMover(Action<string> log, Action<string> logFileOnly)
    {
        _log = log;
        _logFileOnly = logFileOnly;
    }

    /// <summary>Once per frame, on the main thread.</summary>
    public void Run()
    {
        StationGuard.ForgetLeaving();

        while (StationGuard.TryTake(out var work))
        {
            var items = SplitScene.LiveItems();

            if (items == null)
            {
                _logFileOnly("the game's items were gone before a move into a station, so nothing moved");

                continue;
            }

            switch (work)
            {
                case StationWork.TakeOne move:
                    TakeOne(items, move);
                    Redraw(items, move.FromOwnerId, move.ToOwnerId);
                    break;
                case StationWork.SendBack back:
                    SendBack(items, back);
                    Redraw(items, back.FromOwnerId, back.StationOwnerId);
                    break;
                case null:
                    break;
                default:
                    throw new InvalidOperationException("every station move needs carrying out: " + work);
            }
        }

        while (_redraws.TryDequeue(out var bags))
        {
            if (SplitScene.LiveItems() is { } items) Redraw(items, bags.From, bags.To);
        }
    }

    /// <summary>
    /// For the game's quick move, whose callers must learn at once whether the item moved: true when it left its bag,
    /// false when it stayed, null when the stack was not the core's to move and the game's own move should run.
    /// </summary>
    public bool? TakeOneNow(ItemManager items, StationWork.TakeOne move)
    {
        StationReport report;

        try
        {
            report = TakeOne(items, move);
        }
        catch (Exception ex)
        {
            _log($"a quick move of one unit into station {move.ToOwnerId} could not start, the game's own move runs: {ex}");

            return null;
        }

        if (report.Outcome == StationOutcome.Skipped) return null;

        _redraws.Enqueue((move.FromOwnerId, move.ToOwnerId));

        // Once the steps have run, the game's own move must not run as well, so an item that cannot be read stayed.
        return report.Outcome != StationOutcome.Unreadable && HasLeft(items, move);
    }

    private bool HasLeft(ItemManager items, StationWork.TakeOne move)
    {
        try
        {
            return items.GetItemData(move.ItemId) is not { } item || item.OwnerId != move.FromOwnerId;
        }
        catch (Exception ex)
        {
            _log($"whether one unit went into station {move.ToOwnerId} could not be read, so it counts as not moved: {ex}");

            return false;
        }
    }

    private StationReport TakeOne(ItemManager items, StationWork.TakeOne move)
    {
        if (items.GetItemData(move.ItemId) is not { } item)
        {
            _logFileOnly($"the stack was gone before one unit of it could go into station {move.ToOwnerId}, so nothing moved");

            return new StationReport(StationOutcome.Skipped, Array.Empty<string>());
        }

        var stack = StackCopy.Of(item);
        var store = new GameStationStore(items, stack, _log);
        var report = StationMoves.TakeOne(store, move.ItemId, move.FromOwnerId, move.ToOwnerId, () => Replay(items, move));

        Report(report, $"a stack of {item.ItemCount} (item {stack.ConfigId}) moved into station {move.ToOwnerId}");

        return report;
    }

    /// <summary>Each stack found in a station sends all but one unit back to the bag it came from, near the cell it stood on.</summary>
    private void SendBack(ItemManager items, StationWork.SendBack back)
    {
        foreach (var (itemId, before) in back.Stacks)
        {
            var store = new GameStationStore(items, before, _log);
            var report = StationMoves.SendBack(store, itemId, back.StationOwnerId, back.FromOwnerId, before.StoodAt);

            Report(report, $"a stack (item {before.ConfigId}) found in station {back.StationOwnerId}, from container {back.FromOwnerId}");
        }
    }

    private void Report(StationReport report, string what)
    {
        var line = report.Notes.Count == 0 ? $"{what}: {report.Outcome}" : $"{what}: {report.Outcome}; {string.Join("; ", report.Notes)}";

        if (report.NeedsAttention) _log(line);
        else _logFileOnly(line);
    }

    private void Redraw(ItemManager items, long fromOwnerId, long toOwnerId)
    {
        try
        {
            items.SyncBothBags(fromOwnerId, toOwnerId);
        }
        catch (Exception ex)
        {
            _log($"redrawing containers {fromOwnerId} and {toOwnerId} after a move into a station failed; they show right once reopened: {ex}");
        }
    }

    private static void Replay(ItemManager items, StationWork.TakeOne move)
    {
        var bagPos = new Vector2Int(move.BagPos.X, move.BagPos.Y);

        switch (move.Path)
        {
            case StationPath.Move:
                items.OnMoveItem(move.FromOwnerId, move.ItemId, move.ToOwnerId, bagPos);
                break;
            case StationPath.Quick:
                items.TryQuickMoveItem(move.ItemId, move.FromOwnerId, move.ToOwnerId, move.IsSilent, move.AvoidsMold);
                break;
            case StationPath.CookingDrag:
                items.OnCookingDragMove(move.ItemId, move.FromOwnerId, move.ToOwnerId, bagPos);
                break;
            case StationPath.CookingQuick:
                items.OnCookingQuickMove(move.ItemId, move.FromOwnerId, move.ToOwnerId);
                break;
            default:
                throw new InvalidOperationException("every station move needs replaying: " + move.Path);
        }
    }
}
