// Stacking's drop watcher for the station views and the trade window. Runs in Root.html, the document every game page iframe sits in.
// The station pages share one bag script, which puts back a stack let go over another item without telling the game,
// so a drop onto a matching stack never reached the plugin there. This hears each station page's mouse-up before
// that script does and, when a dragged item is let go over another item in a bag grid, says
// "slmod|stacking|onto|<dragged id>|<id under it>" through vuplex.postMessage. It changes nothing on the page; the
// plugin checks the keys held and decides whether anything splits. Every few seconds the plugin checks for
// window.__slstackingDrops and sends this script again when a reload of the web UI lost it; it installs itself once.
// The trade page puts a stack let go over another
// back the same way; there the held stack and the stacks in each grid are read from the page's own drag state.
(function () {
  if (window.__slstackingDrops) return;

  window.__slstackingDrops = true;

  var PREFIX = 'slmod|stacking|onto|';
  var STATION_PAGES = /\/(ToolTable|BrewPanel|Cooking|Shredder|Compressor|DyePanel|DroneHub|GreenhouseBuild|AidDepot|RatCage|Heater|FuelGenerator|MaterialRack|TrapPanel)\.html/;
  var TRADE_PAGE = /\/TradeUI\.html/;
  var SCAN_MS = 400;
  var TRADE_CELL = 56;
  var TRADE_DRAG_THRESHOLD = 5;

  function itemId(node) {
    return node && /^[1-9][0-9]*$/.test(node.id) ? node.id : null;
  }

  // The bag script marks the item it drags and moves it to the body once it really moves; a click leaves it in its grid.
  function draggedItem(doc) {
    var held = doc.querySelectorAll('.item[data-no-sync]');

    for (var i = 0; i < held.length; i++) {
      if (held[i].parentElement === doc.body && itemId(held[i])) return held[i];
    }

    return null;
  }

  // The first item under the cursor other than the dragged one, when it sits in a bag grid.
  function itemUnder(doc, dragged, x, y) {
    var stack = doc.elementsFromPoint(x, y);

    for (var i = 0; i < stack.length; i++) {
      var item = stack[i].closest ? stack[i].closest('.item') : null;

      if (!item || item === dragged) continue;

      var grid = item.parentElement;

      return grid && grid.querySelector('.slot') && itemId(item) ? item : null;
    }

    return null;
  }

  function onMouseUp(win, e) {
    if (e.button !== 0) return;

    try {
      var doc = win.document;
      var dragged = draggedItem(doc);
      var target = dragged ? itemUnder(doc, dragged, e.clientX, e.clientY) : null;

      if (target && window.vuplex) window.vuplex.postMessage(PREFIX + dragged.id + '|' + target.id);
    } catch (err) {}
  }

  function tradeState(doc) {
    var app = doc.getElementById('app');
    var component = app && app._vnode && app._vnode.component;

    return component ? component.setupState : null;
  }

  function tradeGrids(doc, st) {
    var bagEl = doc.getElementById('tradeBagGrid');
    var all = doc.querySelectorAll('.grid-container');
    var droneEl = null;

    for (var i = 0; i < all.length && !droneEl; i++) {
      if (all[i] !== bagEl) droneEl = all[i];
    }

    return [{ el: st.bag.isRack ? null : bagEl, grid: st.bag }, { el: droneEl, grid: st.drone }];
  }

  function overlaps(x, y, w, h, it) {
    return x < it.x + it.w && it.x < x + w && y < it.y + it.h && it.y < y + h;
  }

  // The stack the held one would be put down on, found the way the page picks the cell: from where the held item's
  // corner sits, not from the cursor. Nothing when something else is drawn over the grid there.
  function tradeTarget(doc, st, x, y, zoom) {
    var ds = st.dragState;
    var held = ds.item;
    var grids = tradeGrids(doc, st);
    var top = doc.elementFromPoint(x, y);

    for (var i = 0; i < grids.length; i++) {
      var el = grids[i].el;
      var grid = grids[i].grid;

      if (!el) continue;

      var rect = el.getBoundingClientRect();

      if (x < rect.left || x > rect.right || y < rect.top || y > rect.bottom) continue;

      if (!top || !top.closest || top.closest('.grid-container') !== el) return null;

      var cx = Math.max(0, Math.min(Math.round((x - rect.left - ds.offX) / (TRADE_CELL * zoom)), grid.cols - held.w));
      var cy = Math.max(0, Math.min(Math.round((y - rect.top - ds.offY) / (TRADE_CELL * zoom)), grid.rows - held.h));
      var items = grid.items || [];

      for (var j = 0; j < items.length; j++) {
        if (items[j].id !== held.id && overlaps(cx, cy, held.w, held.h, items[j])) return items[j];
      }

      return null;
    }

    return null;
  }

  function onTradeMouseUp(win, e) {
    if (e.button !== 0) return;

    try {
      var doc = win.document;
      var st = tradeState(doc);
      var ds = st && st.dragState;

      if (!ds || !ds.dragging || !ds.item || (ds.fromGrid !== 'bag' && ds.fromGrid !== 'drone')) return;

      var app = doc.getElementById('app');
      var zoom = Number(app && app.style.zoom) || 1;
      var moved = ds.moved || Math.abs(e.clientX - ds.sx) / zoom >= TRADE_DRAG_THRESHOLD || Math.abs(e.clientY - ds.sy) / zoom >= TRADE_DRAG_THRESHOLD;
      var target = moved ? tradeTarget(doc, st, e.clientX, e.clientY, zoom) : null;
      var draggedId = String(ds.item.id);
      var targetId = target ? String(target.id) : null;

      if (targetId && /^[1-9][0-9]*$/.test(draggedId) && /^[1-9][0-9]*$/.test(targetId) && window.vuplex) window.vuplex.postMessage(PREFIX + draggedId + '|' + targetId);
    } catch (err) {}
  }

  // Each loaded station page gets one listener, in the capture phase, so it runs before the bag script's own.
  function watch(frame) {
    try {
      var win = frame.contentWindow;

      if (!win || win.__slstackingDropsWatched) return;

      var url = win.document.URL;
      var handler = TRADE_PAGE.test(url) ? onTradeMouseUp : STATION_PAGES.test(url) ? onMouseUp : null;

      if (!handler) return;

      win.__slstackingDropsWatched = true;
      win.addEventListener('mouseup', function (e) { handler(win, e); }, true);
    } catch (err) {}
  }

  // A live list the browser keeps current, so the poll searches the document for frames only once.
  var frames = document.getElementsByTagName('iframe');

  function scan() {
    for (var i = 0; i < frames.length; i++) watch(frames[i]);
  }

  scan();
  setInterval(scan, SCAN_MS);
})();
