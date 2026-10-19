using System.Globalization;
using System.Text.Json;

namespace Stacking.Core;

/// <summary>
/// Offering part of a stack in the trade window. The game only ever offers a whole stack, so the units are split off
/// into a new stack in the bag first, and that stack is offered the way the player offered the old one: dropped on the
/// drone's grid where they dropped it, or right-clicked onto the drone's first free spot.
/// </summary>
public static class TradeOffers
{
    /// <summary>The trade page's names for the player's side and the drone's side of the window.</summary>
    public const string BagGrid = "bag";
    public const string DroneGrid = "drone";

    /// <summary>The bag shows the new stack a frame or two after the split; past this it is not coming.</summary>
    public const float ShowWaitSeconds = 1f;

    /// <summary>The game takes or refuses an offer as it is made; past this it was refused.</summary>
    public const float AnswerWaitSeconds = 0.5f;

    /// <summary>A drag from the bag onto the drone with Shift or Ctrl held offers part of the stack. A plain drag is the game's.</summary>
    public static bool SplitsDrag(string? fromGrid, string? toGrid, SplitKind heldKeys) =>
        fromGrid == BagGrid && toGrid == DroneGrid && IsSplit(heldKeys);

    /// <summary>
    /// A right-click on a stack in the bag with Shift or Ctrl held offers part of it. A plain right-click, and a left
    /// click with Shift, are the game's quick offer of the whole stack.
    /// </summary>
    public static bool SplitsClick(string? fromGrid, bool isRightClick, SplitKind heldKeys) =>
        fromGrid == BagGrid && isRightClick && IsSplit(heldKeys);

    /// <summary>What to do next with a stack split off to be offered.</summary>
    public static OfferStep Next(PendingOffer offer, float now, bool isInTradeBag, bool isOffered)
    {
        if (offer.SentAt is not { } sentAt)
        {
            if (isInTradeBag) return OfferStep.Send;

            return now - offer.SplitAt > ShowWaitSeconds ? OfferStep.GiveUp : OfferStep.Wait;
        }

        if (isOffered) return OfferStep.Done;

        return now - sentAt > AnswerWaitSeconds ? OfferStep.Rejoin : OfferStep.Wait;
    }

    /// <summary>
    /// Whether the trade window's list of offered items, [{"id":12,"x":0,"y":0,"o":5}, ...], holds this stack. Ids come
    /// as numbers or as text. Anything unreadable holds nothing.
    /// </summary>
    public static bool IsOffered(string? offeredJson, long stackId)
    {
        if (string.IsNullOrWhiteSpace(offeredJson)) return false;

        try
        {
            using var document = JsonDocument.Parse(offeredJson);

            if (document.RootElement.ValueKind != JsonValueKind.Array) return false;

            foreach (var entry in document.RootElement.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("id", out var id) && IdOf(id) == stackId) return true;
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsSplit(SplitKind heldKeys) => heldKeys is SplitKind.Half or SplitKind.One or SplitKind.Choose;

    private static long? IdOf(JsonElement id)
    {
        if (id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var number)) return number;
        if (id.ValueKind == JsonValueKind.String && long.TryParse(id.GetString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var text)) return text;

        return null;
    }
}

/// <summary>Where the split-off stack goes on the drone: the cell it was dropped on, or the first free spot (At is null).</summary>
public readonly record struct OfferTarget(Cell? At);

/// <summary>A stack split off to be offered: when it was split, and when it was offered, if it has been.</summary>
public sealed record PendingOffer(long StackId, long SourceId, OfferTarget Target, float SplitAt, float? SentAt = null);

public enum OfferStep
{
    /// <summary>Nothing yet: the bag does not show the stack, or the game has not answered.</summary>
    Wait,

    /// <summary>The bag shows the stack: offer it.</summary>
    Send,

    /// <summary>The game took the offer.</summary>
    Done,

    /// <summary>The game refused the offer: put the units back on the stack they came from.</summary>
    Rejoin,

    /// <summary>The bag never showed the stack (the window closed, or the bag tab changed): it stays where it is, split.</summary>
    GiveUp,
}
