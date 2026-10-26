using System;
using System.Collections.Generic;
using System.Text;

namespace Stacking.Core;

/// <summary>
/// Log lines waiting for the file. Opening and appending to the log file once per line on the game's main thread cost
/// a recipe fill or a station move a file write per unit moved, so lines are only queued where they happen and written
/// together by a worker. Each line keeps the time it was added; the time is turned into local time and text when the
/// batch is taken. Any thread may add; one worker takes.
/// </summary>
public sealed class LogLines
{
    private readonly object _gate = new();
    private List<(DateTime At, string Text)> _pending = new();
    private List<(DateTime At, string Text)> _taking = new();
    private readonly StringBuilder _batch = new();

    /// <summary>Queues a line. True when it is the first line since the last batch was taken, so the writer needs waking.</summary>
    public bool Add(DateTime at, string text)
    {
        lock (_gate)
        {
            _pending.Add((at, text));

            return _pending.Count == 1;
        }
    }

    /// <summary>Every line queued so far, in order, as the text to append to the file; null when none are waiting. One taker at a time.</summary>
    public string? TakeBatch()
    {
        lock (_gate)
        {
            if (_pending.Count == 0) return null;

            (_pending, _taking) = (_taking, _pending);
        }

        _batch.Clear();

        foreach (var (at, text) in _taking) _batch.Append($"{at.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff} {text}{Environment.NewLine}");

        _taking.Clear();

        return _batch.ToString();
    }
}
