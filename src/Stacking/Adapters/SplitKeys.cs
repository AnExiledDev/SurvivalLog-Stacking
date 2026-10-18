using System.Diagnostics;
using Stacking.Core;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>
/// Which modifier keys the player holds as a stack is dropped or an item is clicked. Players let go of the key and the
/// mouse button together, so a key seen down in the last moment still counts. Static because the drag and item click
/// hooks read it.
/// </summary>
internal static class SplitKeys
{
    /// <summary>The drop reaches the game a frame or two after the mouse button comes up.</summary>
    private const long DropGraceMs = 250;

    /// <summary>
    /// A click reaches the game on the frame the button comes up, so a frame or two at a low frame rate is enough. A
    /// longer wait would also catch plain clicks made just after letting go of Ctrl.
    /// </summary>
    private const long ClickGraceMs = 100;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    private static long _shiftSeenAt = -DropGraceMs - 1;
    private static long _ctrlSeenAt = -DropGraceMs - 1;

    /// <summary>Called every frame by the ticker.</summary>
    public static void Sample()
    {
        var now = Clock.ElapsedMilliseconds;

        if (IsShiftDown()) _shiftSeenAt = now;
        if (IsCtrlDown()) _ctrlSeenAt = now;
    }

    /// <summary>The keys held for a drop or a right-click split.</summary>
    public static SplitKind Current() => HeldWithin(DropGraceMs);

    /// <summary>What a drop asks for: a split with Shift or Ctrl held, a join onto a matching stack without.</summary>
    public static SplitKind ForDrop() => Splitting.ForDrop(Current());

    /// <summary>The keys held for a click on an item.</summary>
    public static SplitKind ForClick() => HeldWithin(ClickGraceMs);

    private static SplitKind HeldWithin(long graceMs)
    {
        var now = Clock.ElapsedMilliseconds;

        return Splitting.KindFor(IsShiftDown() || now - _shiftSeenAt <= graceMs, IsCtrlDown() || now - _ctrlSeenAt <= graceMs);
    }

    private static bool IsShiftDown() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    private static bool IsCtrlDown() => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
}
