using System.Globalization;
using System.Text.Json;

namespace Stacking.Core;

/// <summary>An item click a station view sends the game: which item, and whether the right button made it.</summary>
public readonly record struct ViewClick(long ItemId, bool IsRightClick);

public static class ItemClicks
{
    /// <summary>
    /// Whether a click on an item, with these keys held, may open the item details popup. Ctrl asks for one unit of a
    /// stack, here and in the game's quick use, so a Ctrl-click the window has no use for must not fall through to a
    /// popup the player never asked for. A plain or Shift click opens it as in the base game.
    /// </summary>
    public static bool OpensDetails(SplitKind heldKeys) => heldKeys is SplitKind.None or SplitKind.Half;

    /// <summary>
    /// Whether a click in a station view splits the stack instead of doing what the view does with it. As in the bag,
    /// only a right-click with Shift or Ctrl splits, and only a stack in the player's own bag: a left click with Shift
    /// is the crafting table's quick move, and what the station or a storage tab holds is the view's to handle.
    /// </summary>
    public static bool SplitsInView(ViewClick click, SplitKind heldKeys, bool isInPlayersBag) =>
        click.IsRightClick && heldKeys != SplitKind.None && isInPlayersBag;

    /// <summary>
    /// Reads what a station page sends with an item click, {"itemId": 12, "rightClick": true}; pages send the id as a
    /// number or as text, and leave rightClick out of a left click. Null for anything that names no item.
    /// </summary>
    public static ViewClick? ParseViewClick(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("itemId", out var id) || ItemIdOf(id) is not { } itemId) return null;

            var isRightClick = root.TryGetProperty("rightClick", out var rightClick) && rightClick.ValueKind == JsonValueKind.True;

            return new ViewClick(itemId, isRightClick);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static long? ItemIdOf(JsonElement id)
    {
        if (id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var number)) return Positive(number);
        if (id.ValueKind == JsonValueKind.String && long.TryParse(id.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var text)) return Positive(text);

        return null;
    }

    private static long? Positive(long id) => id > 0 ? id : null;
}
