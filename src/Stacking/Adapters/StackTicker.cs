using System;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>
/// Every frame, notes the modifier keys and carries out the station rule's moves; every few real seconds, makes sure
/// the item table carries the chosen limits.
/// </summary>
public sealed class StackTicker : MonoBehaviour
{
    private const float SweepSeconds = 5f;
    private const int MaxLoggedFailures = 3;

    internal static LimitWriter? Writer;
    internal static StationMover? Mover;
    internal static Action<string> Log = _ => { };

    private float _nextSweepAt;
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

        if (Writer == null || Time.unscaledTime < _nextSweepAt) return;

        _nextSweepAt = Time.unscaledTime + SweepSeconds;

        try
        {
            Writer.Sweep();
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
