using ExiledToolkit.Game;
using ExiledToolkit.Web;
using Stacking.Core;

namespace Stacking.Adapters;

/// <summary>
/// The game-side half of the split picker. Exiled's Toolkit takes the picker's answers before the game's web layer sees
/// them (it would log them as unknown) and, while the picker is open, keeps the game's hotkeys quiet, so typing "5"
/// does not also pick a hotbar slot, and sends every click to the web UI, which the game only does where its own pages
/// report a button.
/// </summary>
internal static class PickerHooks
{
    private const string Window = "Stacking.picker";

    private static PageMailbox? _answers;

    /// <summary>False when the picker's answers cannot be heard, so it must not be opened.</summary>
    public static bool Apply()
    {
        _answers = PageInbox.Listen(PickerAnswer.Prefix);

        return PageInbox.IsListening;
    }

    public static bool TryTakeAnswer(out PickerAnswer? answer)
    {
        if (_answers != null && _answers.TryTake(out var message))
        {
            answer = PickerAnswer.Parse(message);

            return true;
        }

        answer = null;

        return false;
    }

    public static void Opened() => InputGate.Opened(Window);

    /// <summary>Keeps hotkeys quiet through the next frame, so the Esc that closed the picker does not also open the game's menu.</summary>
    public static void Closed() => InputGate.Closed(Window);
}
