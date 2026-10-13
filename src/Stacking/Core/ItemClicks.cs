namespace Stacking.Core;

public static class ItemClicks
{
    /// <summary>
    /// Whether a click on an item, with these keys held, may open the item details popup. Ctrl asks for one unit of a
    /// stack, here and in the game's quick use, so a Ctrl-click the window has no use for must not fall through to a
    /// popup the player never asked for. A plain or Shift click opens it as in the base game.
    /// </summary>
    public static bool OpensDetails(SplitKind heldKeys) => heldKeys is SplitKind.None or SplitKind.Half;
}
