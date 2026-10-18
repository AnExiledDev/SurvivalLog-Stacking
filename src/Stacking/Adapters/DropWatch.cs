using System;
using System.IO;
using GameCore.HotUpdate.ReduxUI;
using Stacking.Core;

namespace Stacking.Adapters;

/// <summary>
/// Hears a stack let go over another item in a station view, which the page puts back without telling the game:
/// drops.js, in the game's web UI, says which item was dropped onto which, and with Shift or Ctrl held that drop is
/// a split onto the item under it, and without them a join when it is the same item. The web UI can reload and lose the script at any time, so it is sent again every
/// few seconds; it installs itself once.
/// </summary>
internal sealed class DropWatch
{
    private const string ScriptResource = "Stacking.drops.js";
    private const float ResendSeconds = 3f;

    private readonly Action<string> _log;
    private string? _script;
    private float _sentAt = float.NegativeInfinity;
    private bool _hasReportedRunFailure;

    public DropWatch(Action<string> log)
    {
        _log = log;
    }

    /// <summary>Each frame: makes sure the station pages are watched.</summary>
    public void Tick(float now)
    {
        if (now - _sentAt < ResendSeconds) return;

        _sentAt = now;
        Install();
    }

    /// <summary>The page put the stack back, so a drop the core hands to the game leaves it where it was.</summary>
    public static void DroppedOnto(PickerAnswer.Onto drop)
    {
        var kind = SplitKeys.ForDrop();
        var items = SplitScene.LiveItems();
        var dragged = items?.GetItemData(drop.ItemId);
        var target = items?.GetItemData(drop.TargetId);

        if (dragged == null || target == null || dragged.ItemCount < Splitting.SmallestStack(kind) || dragged.InstanceWeight != 0) return;

        var at = new Cell(target.BagPos.x, target.BagPos.y);

        SplitHooks.RunsOriginal(new SplitRequest(dragged.InstanceId, dragged.OwnerId, target.OwnerId, kind, at, Cursor: at));
    }

    private void Install()
    {
        var webView = GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;

        if (webView == null) return;

        try
        {
            webView.ExecuteJavaScript(_script ??= ReadScript(), null);
        }
        catch (Exception ex)
        {
            if (!_hasReportedRunFailure) _log("could not watch drops in the station views (reported once): " + ex.Message);

            _hasReportedRunFailure = true;
        }
    }

    private static string ReadScript()
    {
        using var stream = typeof(DropWatch).Assembly.GetManifestResourceStream(ScriptResource) ?? throw new InvalidOperationException(ScriptResource + " is not embedded");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
