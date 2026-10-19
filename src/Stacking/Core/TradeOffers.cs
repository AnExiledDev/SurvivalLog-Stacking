using System.Globalization;
using System.Text.Json;

namespace Stacking.Core;

/// <summary>
/// Offering part of a stack in the trade window, and taking part of an offer back. The game only ever offers or takes
/// back a whole stack, so the units are split off into a new stack in the bag first. To offer, that stack is offered
/// the way the player offered the old one: dropped on the drone's grid where they dropped it, or right-clicked onto
/// the drone's first free spot. To take back, the split-off stack stays in the bag, and the smaller offer is put down
/// again where it lies so the window counts it afresh.
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

    /// <summary>
    /// A drag between the bag and the drone with Shift or Ctrl held offers part of the stack, or takes part of an offer
    /// back. A plain drag is the game's.
    /// </summary>
    public static bool SplitsDrag(string? fromGrid, string? toGrid, SplitKind heldKeys) =>
        ((fromGrid == BagGrid && toGrid == DroneGrid) || (fromGrid == DroneGrid && toGrid == BagGrid)) && IsSplit(heldKeys);

    /// <summary>
    /// A right-click on a stack in the bag or on the drone with Shift or Ctrl held offers part of it, or takes part of
    /// it back. A plain right-click, and a left click with Shift, are the game's quick move of the whole stack.
    /// </summary>
    public static bool SplitsClick(string? fromGrid, bool isRightClick, SplitKind heldKeys) =>
        (fromGrid == BagGrid || fromGrid == DroneGrid) && isRightClick && IsSplit(heldKeys);

    /// <summary>A split that starts on the drone takes units back off an offer; one that starts in the bag offers them.</summary>
    public static bool TakesBack(string? fromGrid) => fromGrid == DroneGrid;

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
    /// What to do next with units taken back off an offer: once the bag shows them, put the smaller offer down again
    /// (Send). If the bag never shows them they stay where they are (GiveUp).
    /// </summary>
    public static OfferStep Next(PendingTakeBack takeBack, float now, bool isInTradeBag)
    {
        if (isInTradeBag) return OfferStep.Send;

        return now - takeBack.SplitAt > ShowWaitSeconds ? OfferStep.GiveUp : OfferStep.Wait;
    }

    public static bool IsOffered(string? offeredJson, long stackId) => OfferedAt(offeredJson, stackId) is not null;

    /// <summary>
    /// Where the trade window's list of offered items, [{"id":12,"x":0,"y":0,"o":5}, ...], has this stack on the drone,
    /// or null when it is not offered. Ids come as numbers or as text. Anything unreadable holds nothing.
    /// </summary>
    public static Cell? OfferedAt(string? offeredJson, long stackId)
    {
        if (string.IsNullOrWhiteSpace(offeredJson)) return null;

        try
        {
            using var document = JsonDocument.Parse(offeredJson);

            if (document.RootElement.ValueKind != JsonValueKind.Array) return null;

            foreach (var entry in document.RootElement.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("id", out var id) && IdOf(id) == stackId) return CellOf(entry);
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsSplit(SplitKind heldKeys) => heldKeys is SplitKind.Half or SplitKind.One or SplitKind.Choose;

    private static Cell CellOf(JsonElement entry) => new(IntOf(entry, "x"), IntOf(entry, "y"));

    private static int IntOf(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;

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

/// <summary>Units taken back off an offer: the new stack in the bag, and the offered stack they came off with where it lies on the drone.</summary>
public sealed record PendingTakeBack(long StackId, long OfferedId, Cell OfferedAt, float SplitAt);

public enum OfferStep
{
    /// <summary>Nothing yet: the bag does not show the stack, or the game has not answered.</summary>
    Wait,

    /// <summary>The bag shows the stack: offer it, or, for units taken back, put the smaller offer down again.</summary>
    Send,

    /// <summary>The game took the offer.</summary>
    Done,

    /// <summary>The game refused the offer: put the units back on the stack they came from.</summary>
    Rejoin,

    /// <summary>The bag never showed the stack (the window closed, or the bag tab changed): it stays where it is, split.</summary>
    GiveUp,
}
