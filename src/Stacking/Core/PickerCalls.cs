using System.Globalization;
using System.Text;

namespace Stacking.Core;

/// <summary>The calls the plugin makes into picker.js, as JavaScript source for the web view to run.</summary>
public static class PickerCalls
{
    private const string Page = "window.__slstacking";

    public static string Open(int token, int max, int suggested) => $"{Page}.open({Number(token)},{Number(max)},{Number(suggested)})";

    public static string Toast(string text) => $"{Page}.toast({Quote(text)})";

    /// <summary>Safe to send when the script never arrived: there is nothing to close then.</summary>
    public static string Close() => $"{Page}&&{Page}.close()";

    /// <summary>
    /// Asks whether the picker with this token is still on screen; the page answers shown or gone. Sent without the
    /// script, so a page that reloaded and lost it answers gone from here instead of installing a fresh, empty picker.
    /// </summary>
    public static string Check(int token) =>
        $"{Page}?{Page}.check({Number(token)}):window.vuplex&&window.vuplex.postMessage({Quote($"{PickerAnswer.Prefix}gone|{Number(token)}")})";

    /// <summary>A JavaScript string literal that no quote, backslash or line break in the text can end early.</summary>
    public static string Quote(string text)
    {
        var literal = new StringBuilder(text.Length + 2).Append('"');

        foreach (var c in text)
        {
            switch (c)
            {
                case '"':
                    literal.Append("\\\"");
                    break;
                case '\\':
                    literal.Append("\\\\");
                    break;
                case < ' ' or '\u2028' or '\u2029':
                    literal.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    break;
                default:
                    literal.Append(c);
                    break;
            }
        }

        return literal.Append('"').ToString();
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
