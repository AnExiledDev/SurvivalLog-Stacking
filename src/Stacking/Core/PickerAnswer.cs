using System;
using System.Globalization;

namespace Stacking.Core;

/// <summary>
/// What the split picker on the page says, each message prefixed "slmod|stacking|":
/// "split|token|units" and "cancel|token" answer the question; "shown|token" and "gone|token" say whether the picker
/// with that token is on screen, after it opens and when the plugin checks; "ready" says the script has just installed
/// itself, which after the picker opened means the page reloaded. The token ties a message to the drag that opened the
/// picker, so one about an older picker is told apart. Anything else parses to Unknown so it can be logged rather than acted on.
/// </summary>
public abstract record PickerAnswer
{
    public const string Prefix = "slmod|stacking|";

    private PickerAnswer()
    {
    }

    public sealed record Split(int Token, int Units) : PickerAnswer;

    public sealed record Cancel(int Token) : PickerAnswer;

    public sealed record Shown(int Token) : PickerAnswer;

    public sealed record Gone(int Token) : PickerAnswer;

    public sealed record Ready : PickerAnswer;

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
            ("shown", 2) when TryInt(parts[1], out var token) => new Shown(token),
            ("gone", 2) when TryInt(parts[1], out var token) => new Gone(token),
            ("ready", 1) => new Ready(),
            _ => new Unknown(message),
        };
    }

    private static bool TryInt(string text, out int value) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
