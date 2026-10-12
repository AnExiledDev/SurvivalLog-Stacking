using System;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>Drives stack splitting once a frame: watches the modifier keys, hears the picker, carries out split drops.</summary>
public sealed class SplitTicker : MonoBehaviour
{
    private const int MaxLoggedFailures = 3;

    internal static Action<string> Log = _ => { };

    private int _failures;

    public SplitTicker(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
        try
        {
            StackSplitting.Tick(Time.unscaledTime);
        }
        catch (Exception ex)
        {
            _failures++;

            if (_failures <= MaxLoggedFailures) Log($"a stack split failed and nothing moved (logged {_failures} of {MaxLoggedFailures}): {ex}");
        }
    }
}
