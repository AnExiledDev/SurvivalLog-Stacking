using System;
using ExiledToolkit.Web;
using Stacking.Core;

namespace Stacking.Adapters;

/// <summary>
/// Hears a stack let go over another item in a station view or the trade window, which the page puts back without telling the game:
/// drops.js, in the game's web UI, says which item was dropped onto which, and with Shift or Ctrl held that drop is
/// a split onto the item under it, and without them a join when it is the same item. The web UI can reload and lose the script at any time, so a short check
/// runs on the page every few seconds, and the script is sent again only when the page says it lost it; it installs itself once.
/// </summary>
internal sealed class DropWatch
{
    /// <summary>drops.js sets it as it installs, so a page that has it says nothing to the check.</summary>
    private const string Global = "window.__slstackingDrops";

    private readonly WebPage _page;

    public DropWatch(Action<string> log)
    {
        _page = new WebPage(typeof(DropWatch).Assembly, "Stacking.drops.js", Global, PickerAnswer.Prefix + "drops", log) { Name = "drop watcher", CheckSeconds = 3 };
    }

    /// <summary>Each frame: makes sure the station pages are watched. Between checks it does nothing.</summary>
    public void Tick(float now) => _page.KeepInstalled(now);

    /// <summary>The page's check found drops.js gone, so it goes again now.</summary>
    public void Missing() => _page.Install();

    /// <summary>The page put the stack back, so a drop the core hands to the game leaves it where it was.</summary>
    public static void DroppedOnto(PickerAnswer.Onto drop)
    {
        var kind = SplitKeys.ForDrop();
        var items = SplitScene.LiveItems();
        var dragged = items?.GetItemData(drop.ItemId);
        var target = items?.GetItemData(drop.TargetId);

        if (dragged == null || target == null || dragged.ItemCount < Splitting.SmallestStack(kind) || dragged.InstanceWeight != 0) return;

        var at = new Cell(target.BagPos.x, target.BagPos.y);
        var request = new SplitRequest(dragged.InstanceId, dragged.OwnerId, target.OwnerId, kind, at, Cursor: at);

        if (!TradeSplit.DroppedOnto(request, dragged.ItemCount)) SplitHooks.RunsOriginal(request);
    }
}
