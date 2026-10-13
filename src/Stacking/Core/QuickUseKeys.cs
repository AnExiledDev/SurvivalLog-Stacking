using System;

namespace Stacking.Core;

/// <summary>
/// The key that does what the game does with Ctrl held in the bag: a click uses the item at once, and the bag lights
/// up the items that can be used that way.
/// </summary>
public abstract record QuickUseKey
{
    private QuickUseKey()
    {
    }

    /// <summary>The game keeps Ctrl, so nothing needs hooking.</summary>
    public sealed record GameCtrl : QuickUseKey;

    /// <summary>No key: a click never uses an item at once, and the highlight never shows.</summary>
    public sealed record Off : QuickUseKey;

    /// <summary>The key with this Unity key number does it instead of Ctrl.</summary>
    public sealed record Key(int Code) : QuickUseKey;
}

/// <summary>The key the setting asks for; Problem says why the setting could not be used as written, and the default was used.</summary>
public readonly record struct QuickUseKeySetting(QuickUseKey Key, string? Problem);

/// <summary>What one of the game's Ctrl reads in the bag answers.</summary>
public abstract record KeyRead
{
    private KeyRead()
    {
    }

    /// <summary>The key the game asked about.</summary>
    public sealed record AsAsked : KeyRead;

    /// <summary>Not held, whatever the keyboard says.</summary>
    public sealed record NotHeld : KeyRead;

    /// <summary>Whether the key with this Unity key number is held.</summary>
    public sealed record Instead(int Code) : KeyRead;
}

public static class QuickUseKeys
{
    public const string DefaultName = "LeftAlt";

    /// <summary>The setting value that leaves quick use on the game's Ctrl.</summary>
    public const string GameKeyName = "Ctrl";

    // Unity's KeyCode numbers.
    private const int NoKey = 0;
    private const int RightShift = 303;
    private const int LeftShift = 304;
    private const int RightControl = 305;
    private const int LeftControl = 306;
    private const int FirstMouseButton = 323;
    private const int LastMouseButton = 329;

    /// <summary>
    /// Reads the QuickUseKey setting. keyCodeOf gives the Unity key number for a key name, or null when Unity has no
    /// key by that name. Either Ctrl gives the key back to the game. Shift is refused because it splits stacks and is
    /// the game's quick move, and a mouse button because it is the click itself; those, and names Unity does not know,
    /// fall back to the default key.
    /// </summary>
    public static QuickUseKeySetting Parse(string? text, Func<string, int?> keyCodeOf)
    {
        var name = (text ?? "").Trim();

        if (name.Length == 0) return new QuickUseKeySetting(new QuickUseKey.Off(), null);

        if (name.Equals(GameKeyName, StringComparison.OrdinalIgnoreCase)) return new QuickUseKeySetting(new QuickUseKey.GameCtrl(), null);

        if (keyCodeOf(name) is not { } code) return Fallback($"'{name}' is not a Unity key name", keyCodeOf);

        return code switch
        {
            NoKey => new QuickUseKeySetting(new QuickUseKey.Off(), null),
            LeftControl or RightControl => new QuickUseKeySetting(new QuickUseKey.GameCtrl(), null),
            LeftShift or RightShift => Fallback($"'{name}' is Shift, which splits stacks and is the game's quick move", keyCodeOf),
            >= FirstMouseButton and <= LastMouseButton => Fallback($"'{name}' is a mouse button, which is the click itself", keyCodeOf),
            _ => new QuickUseKeySetting(new QuickUseKey.Key(code), null),
        };
    }

    public static bool IsCtrl(int code) => code is LeftControl or RightControl;

    /// <summary>
    /// What a key read made by the game's bag code answers: a read of either Ctrl answers for the quick-use key, and
    /// every other key is left as asked. The quick-use key is never Ctrl, so reading it never comes back here as a Ctrl read.
    /// </summary>
    public static KeyRead ReadFor(QuickUseKey key, int askedCode)
    {
        if (!IsCtrl(askedCode)) return new KeyRead.AsAsked();

        return key switch
        {
            QuickUseKey.GameCtrl => new KeyRead.AsAsked(),
            QuickUseKey.Off => new KeyRead.NotHeld(),
            QuickUseKey.Key chosen => new KeyRead.Instead(chosen.Code),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "every quick-use key needs an answer for a Ctrl read"),
        };
    }

    private static QuickUseKeySetting Fallback(string problem, Func<string, int?> keyCodeOf)
    {
        QuickUseKey fallback = keyCodeOf(DefaultName) is { } code ? new QuickUseKey.Key(code) : new QuickUseKey.GameCtrl();

        return new QuickUseKeySetting(fallback, $"{problem}, so {DefaultName} is used");
    }
}
