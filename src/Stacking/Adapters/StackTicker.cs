using System;
using System.Diagnostics;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>
/// Every frame, notes the modifier keys and carries out the station rule's moves. Until the item table carries the
/// chosen limits, it tries every few real seconds, and while it writes them, writes a slice each frame; once they are
/// written, it stops until the limits change.
/// </summary>
public sealed class StackTicker : MonoBehaviour
{
    private const float SweepSeconds = 5f;
    private const long SweepMilliseconds = 5000;
    private const int MaxLoggedFailures = 3;

    internal static LimitWriter? Writer;
    internal static StationMover? Mover;
    internal static Action<string> Log = _ => { };

    /// <summary>The sweep's own clock, so a frame with no sweep due reads no game time.</summary>
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _nextSweepAtMs;
    private int _failures;
    private int _moveFailures;
    private int _keyFailures;

    public StackTicker(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
        SampleKeys();
        MoveIntoStations();

        if (Writer == null || Writer.IsWritten) return;

        // A full write runs a slice every frame until it is done; before one starts, the tables are tried every few seconds.
        var isWriting = Writer.IsWriting;

        if (!isWriting)
        {
            if (_clock.ElapsedMilliseconds < _nextSweepAtMs) return;

            _nextSweepAtMs = _clock.ElapsedMilliseconds + SweepMilliseconds;
        }

        try
        {
            if (isWriting) Writer.Continue();
            else Writer.Sweep();
        }
        catch (Exception ex)
        {
            _failures++;

            if (_failures <= MaxLoggedFailures) Log($"writing stack limits failed, tried again in {SweepSeconds:0}s (logged {_failures} of {MaxLoggedFailures}): {ex}");
        }
    }

    /// <summary>For both splitting and item clicks, so a key let go with the mouse button reads the same either way.</summary>
    private void SampleKeys()
    {
        try
        {
            SplitKeys.Sample();
        }
        catch (Exception ex)
        {
            _keyFailures++;

            if (_keyFailures <= MaxLoggedFailures) Log($"reading the modifier keys failed (logged {_keyFailures} of {MaxLoggedFailures}): {ex}");
        }
    }

    private void MoveIntoStations()
    {
        try
        {
            Mover?.Run();
        }
        catch (Exception ex)
        {
            _moveFailures++;

            if (_moveFailures <= MaxLoggedFailures) Log($"a move into a station failed (logged {_moveFailures} of {MaxLoggedFailures}): {ex}");
        }
    }
}
