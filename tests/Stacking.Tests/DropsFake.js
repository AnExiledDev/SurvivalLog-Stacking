// A fake root page for drops.js: game page iframes whose windows keep their listeners, a vuplex bridge that records
// what it is sent, and timers that run only when the test ticks them.
var window = this;

var timers = [];
function setInterval(fn) { timers.push(fn); return timers.length; }
function clearInterval() {}
function setTimeout() { return 0; }
function clearTimeout() {}

function tick() {
  for (var i = 0; i < timers.length; i++) timers[i]();
}

var sent = [];
window.vuplex = { postMessage: function (message) { sent.push(String(message)); } };

function makePage(url) {
  var win = {
    listeners: [],
    location: { href: url },
    addEventListener: function (type, fn, capture) { this.listeners.push({ type: type, fn: fn, capture: capture === true }); }
  };
  var doc = {
    URL: url,
    defaultView: win,
    body: { tagName: 'BODY' },
    held: [],
    under: [],
    querySelectorAll: function () { return this.held; },
    elementsFromPoint: function () { return this.under; }
  };

  win.document = doc;

  return doc;
}

var frames = [];
var document = { getElementsByTagName: function (tag) { return tag === 'iframe' ? frames : []; } };

function show(frame, url) {
  frame.src = url;
  frame.contentDocument = makePage(url);
  frame.contentWindow = frame.contentDocument.defaultView;
}

function addFrame(url) {
  var frame = { isConnected: true };

  show(frame, url);
  frames.push(frame);

  return frame;
}

function mouseUps(frame) {
  return frame.contentWindow.listeners.filter(function (l) { return l.type === 'mouseup' && l.capture; }).length;
}

// The bag script's drag in a station page: the dragged item moved to the body, another item in a bag grid under the cursor.
function dragOnto(frame, draggedId, targetId) {
  var doc = frame.contentDocument;
  var dragged = { id: String(draggedId), parentElement: doc.body };
  var grid = { querySelector: function (selector) { return selector === '.slot' ? {} : null; } };
  var target = { id: String(targetId), parentElement: grid };

  target.closest = function (selector) { return selector === '.item' ? target : null; };
  doc.held = [dragged];
  doc.under = [target];
}

function letGo(frame) {
  var win = frame.contentWindow;

  win.listeners.forEach(function (l) {
    if (l.type === 'mouseup') l.fn({ button: 0, clientX: 10, clientY: 10 });
  });
}
