using System;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>Every frame, carries out the station rule's moves; every few real seconds, makes sure the item table carries the chosen limits.</summary>
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

    public StackTicker(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
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
