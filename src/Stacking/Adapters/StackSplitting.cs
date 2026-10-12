using System;
using HarmonyLib;
using Stacking.Core;

namespace Stacking.Adapters;

/// <summary>
/// Splitting a stack by dropping it with a modifier held: Shift moves half, Ctrl moves one, Ctrl+Shift asks how many.
/// The hooks catch the drop, the ticker carries it out next frame through the executor, and the picker asks and
/// answers. Static because the ticker is made by Unity and finds its work here.
/// </summary>
internal static class StackSplitting
{
    private const int MaxLoggedUnknownAnswers = 3;

    private static Action<string> _log = _ => { };
    private static SplitPicker? _picker;
    private static SplitExecutor? _executor;
    private static int _unknownAnswers;

    /// <summary>True when drops can be split; the ticker only needs adding then.</summary>
    public static bool Apply(Harmony harmony, Action<string> log, Action<string> logFileOnly)
    {
        _log = log;

        if (!SplitHooks.Apply(harmony, log))
        {
            log("stacks cannot be split this session: a drop could not be caught, so every drag moves the whole stack");

            return false;
        }

        var canAsk = PickerHooks.Apply(harmony, log);

        if (!canAsk) log("Ctrl+Shift cannot ask how many this session; Shift and Ctrl still split");

        _picker = new SplitPicker(log);
        _executor = new SplitExecutor(log, logFileOnly, _picker, SplitHooks.CanStartStacks, canAsk);
        log("stacks split when dropped with Shift (half), Ctrl (one) or Ctrl+Shift (choose)");

        return true;
    }

    /// <summary>Once per frame, on the main thread.</summary>
    public static void Tick(float now)
    {
        if (_picker == null || _executor == null) return;

        SplitKeys.Sample();

        if (SplitHooks.TakeBackpackClosed()) _picker.Close();

        while (PickerHooks.TryTakeAnswer(out var answer)) Answer(answer, now);

        _picker.Tick(now);

        while (SplitHooks.TryTakeRequest(out var request))
        {
            if (request != null) _executor.Run(request, chosenUnits: null, now);
        }
    }

    private static void Answer(PickerAnswer? answer, float now)
    {
        switch (answer)
        {
            case PickerAnswer.Split split:
                if (_picker?.Answered(split.Token) is { } request) _executor?.Run(request, split.Units, now);
                break;
            case PickerAnswer.Cancel cancel:
                _picker?.Cancelled(cancel.Token);
                break;
            case PickerAnswer.Shown shown:
                _picker?.Shown(shown.Token, now);
                break;
            case PickerAnswer.Gone gone:
                _picker?.Gone(gone.Token);
                break;
            case PickerAnswer.Ready:
                _picker?.Reloaded();
                break;
            case PickerAnswer.Unknown unknown:
                _unknownAnswers++;

                if (_unknownAnswers <= MaxLoggedUnknownAnswers) _log($"the split picker sent something unexpected (logged {_unknownAnswers} of {MaxLoggedUnknownAnswers}): {unknown.Text}");

                break;
        }
    }
}
