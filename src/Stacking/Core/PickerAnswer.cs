using System;
using System.Globalization;

namespace Stacking.Core;

/// <summary>
/// What the split picker on the page answers. It sends "slmod|stacking|split|token|units" or "slmod|stacking|cancel|token";
/// the token ties the answer to the drag that opened the picker, so a late answer to an older picker is told apart.
/// Anything else parses to Unknown so it can be logged rather than acted on.
/// </summary>
public abstract record PickerAnswer
{
    public const string Prefix = "slmod|stacking|";

    private PickerAnswer()
    {
    }

    public sealed record Split(int Token, int Units) : PickerAnswer;

    public sealed record Cancel(int Token) : PickerAnswer;

    public sealed record Unknown(string Text) : PickerAnswer;

    public static bool IsOurs(string? message) => message is not null && message.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Parses a whole message, prefix included. A split of no units is no answer at all.</summary>
    public static PickerAnswer Parse(string message)
    {
        if (!IsOurs(message)) return new Unknown(message);

        var parts = message.Substring(Prefix.Length).Split('|');

        return (parts[0], parts.Length) switch
        {
            ("split", 3) when TryInt(parts[1], out var token) && TryInt(parts[2], out var units) && units > 0 => new Split(token, units),
            ("cancel", 2) when TryInt(parts[1], out var token) => new Cancel(token),
            _ => new Unknown(message),
        };
    }

    private static bool TryInt(string text, out int value) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
