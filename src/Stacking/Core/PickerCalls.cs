using System.Globalization;
using System.Linq;
using ExiledToolkit.Web;

namespace Stacking.Core;

/// <summary>The calls the plugin makes into picker.js, as JavaScript source for the web view to run.</summary>
public static class PickerCalls
{
    private const string Page = "window.__slstacking";

    /// <summary>Opens the picker with its words in the game's language: {title, hint, half, max, cancel, split}, the hint still holding {0}.</summary>
    public static string Open(int token, int max, int suggested, bool isChinese) =>
        $"{Page}.open({Number(token)},{Number(max)},{Number(suggested)},{Words(isChinese)})";


    /// <summary>Safe to send when the script never arrived: there is nothing to close then.</summary>
    public static string Close() => $"{Page}&&{Page}.close()";

    /// <summary>
    /// Asks whether the picker with this token is still on screen; the page answers shown or gone. Sent without the
    /// script, so a page that reloaded and lost it answers gone from here instead of installing a fresh, empty picker.
    /// </summary>
    public static string Check(int token) =>
        $"{Page}?{Page}.check({Number(token)}):window.vuplex&&window.vuplex.postMessage({JsText.Literal($"{PickerAnswer.Prefix}gone|{Number(token)}")})";

    private static string Words(bool isChinese)
    {
        var words = new[]
        {
            ("title", Texts.PickerTitle), ("hint", Texts.PickerHint), ("half", Texts.PickerHalf), ("max", Texts.PickerMax),
            ("cancel", Texts.PickerCancel), ("split", Texts.PickerSplit),
        };

        return "{" + string.Join(",", words.Select(word => JsText.Literal(word.Item1) + ":" + JsText.Literal(word.Item2.Pick(isChinese)))) + "}";
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
