using System.Diagnostics;
using Stacking.Core;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>
/// Which modifier keys the player holds as a stack is dropped. The drop reaches the game a frame or two after the
/// mouse button comes up, and players let go of Shift and the button together, so a key seen down in the last
/// moment still counts. Static because the drag hook reads it.
/// </summary>
internal static class SplitKeys
{
    private const long HeldGraceMs = 250;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    private static long _shiftSeenAt = -HeldGraceMs - 1;
    private static long _ctrlSeenAt = -HeldGraceMs - 1;

    /// <summary>Called every frame by the ticker.</summary>
    public static void Sample()
    {
        var now = Clock.ElapsedMilliseconds;

        if (IsShiftDown()) _shiftSeenAt = now;
        if (IsCtrlDown()) _ctrlSeenAt = now;
    }

    public static SplitKind Current()
    {
        var now = Clock.ElapsedMilliseconds;

        return Splitting.KindFor(IsShiftDown() || now - _shiftSeenAt <= HeldGraceMs, IsCtrlDown() || now - _ctrlSeenAt <= HeldGraceMs);
    }

    private static bool IsShiftDown() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    private static bool IsCtrlDown() => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
}
