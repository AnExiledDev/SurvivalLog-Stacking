using System;
using System.IO;
using GameCore.HotUpdate.ReduxUI;
using Stacking.Core;

namespace Stacking.Adapters;

/// <summary>
/// Hears a stack let go over another item in a station view or the trade window, which the page puts back without telling the game:
/// drops.js, in the game's web UI, says which item was dropped onto which, and with Shift or Ctrl held that drop is
/// a split onto the item under it, and without them a join when it is the same item. The web UI can reload and lose the script at any time, so a short check
/// runs on the page every few seconds, and the script is sent again only when the page says it lost it (see DropScriptSchedule); it installs itself once.
/// </summary>
internal sealed class DropWatch
{
    private const string ScriptResource = "Stacking.drops.js";

    private readonly Action<string> _log;
    private readonly DropScriptSchedule _schedule = new();
    private string? _script;
    private bool _hasReportedRunFailure;

    public DropWatch(Action<string> log)
    {
        _log = log;
    }

    /// <summary>Each frame: makes sure the station pages are watched. Between checks it does nothing.</summary>
    public void Tick(float now)
    {
        var send = _schedule.Next(now);

        if (send == DropScriptSend.Nothing) return;

        if (!TrySend(send == DropScriptSend.Script)) _schedule.NotSent();
    }

    /// <summary>The page's check found drops.js gone, so it goes again at the next frame.</summary>
    public void Missing() => _schedule.Missing();

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

    /// <summary>False when nothing reached a page: no web UI yet, or the call failed.</summary>
    private bool TrySend(bool isScript)
    {
        var webView = GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;

        if (webView == null) return false;

        try
        {
            webView.ExecuteJavaScript(isScript ? _script ??= ReadScript() : DropScriptSchedule.Check, null);

            return true;
        }
        catch (Exception ex)
        {
            if (!_hasReportedRunFailure) _log("could not watch drops in the station views (reported once): " + ex.Message);

            _hasReportedRunFailure = true;

            return false;
        }
    }

    private static string ReadScript()
    {
        using var stream = typeof(DropWatch).Assembly.GetManifestResourceStream(ScriptResource) ?? throw new InvalidOperationException(ScriptResource + " is not embedded");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
