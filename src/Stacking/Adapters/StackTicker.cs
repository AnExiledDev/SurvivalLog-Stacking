using System;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>Every few real seconds, makes sure the item table carries the chosen limits.</summary>
public sealed class StackTicker : MonoBehaviour
{
    private const float SweepSeconds = 5f;
    private const int MaxLoggedFailures = 3;

    internal static LimitWriter? Writer;
    internal static Action<string> Log = _ => { };

    private float _nextSweepAt;
    private int _failures;

    public StackTicker(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
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
}
