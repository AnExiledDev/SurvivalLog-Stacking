// Stacking's drop watcher for the station views. Runs in Root.html, the document every game page iframe sits in.
// The station pages share one bag script, which puts back a stack let go over another item without telling the game,
// so a drop onto a matching stack never reached the plugin there. This hears each station page's mouse-up before
// that script does and, when a dragged item is let go over another item in a bag grid, says
// "slmod|stacking|onto|<dragged id>|<id under it>" through vuplex.postMessage. It changes nothing on the page; the
// plugin checks the keys held and decides whether anything splits. The plugin sends this script again every few
// seconds, in case the web UI reloaded, so it installs itself once.
(function () {
  if (window.__slstackingDrops) return;

  window.__slstackingDrops = true;

  var PREFIX = 'slmod|stacking|onto|';
  var STATION_PAGES = /\/(ToolTable|BrewPanel|Cooking|Shredder|Compressor|DyePanel|DroneHub|GreenhouseBuild|AidDepot|RatCage|Heater|FuelGenerator|MaterialRack|TrapPanel)\.html/;
  var SCAN_MS = 400;

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

  // Each loaded station page gets one listener, in the capture phase, so it runs before the bag script's own.
  function watch(frame) {
    try {
      var win = frame.contentWindow;

      if (!win || win.__slstackingDropsWatched || !STATION_PAGES.test(win.document.URL)) return;

      win.__slstackingDropsWatched = true;
      win.addEventListener('mouseup', function (e) { onMouseUp(win, e); }, true);
    } catch (err) {}
  }

  function scan() {
    var frames = document.querySelectorAll('iframe');

    for (var i = 0; i < frames.length; i++) watch(frames[i]);
  }

  scan();
  setInterval(scan, SCAN_MS);
})();
