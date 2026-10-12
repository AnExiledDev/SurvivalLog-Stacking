// Stacking's split picker. Runs in Root.html, the document every game page iframe sits in, and draws over all of
// them in the game's own colours and fonts, as Storage Hub's panel does. Asks how many units of a dropped stack to
// move and answers through vuplex.postMessage("slmod|stacking|split|<token>|<units>" or "slmod|stacking|cancel|<token>").
// Driven by window.__slstacking. The plugin sends this whole script ahead of every call, so it installs itself once.
(function () {
  if (window.__slstacking) return;

  var PREFIX = 'slmod|stacking|';
  var TOAST_MS = 2600;

  var ui = null;
  var asked = null;
  var toastTimer = 0;

  function send(message) {
    try {
      if (window.vuplex) window.vuplex.postMessage(PREFIX + message);
    } catch (e) {}
  }

  function el(tag, className, text) {
    var node = document.createElement(tag);

    if (className) node.className = className;
    if (text != null) node.textContent = text;

    return node;
  }

  function button(text, action, extraClass) {
    var node = el('button', 'sls-btn' + (extraClass ? ' ' + extraClass : ''), text);
    node.setAttribute('data-act', action);

    return node;
  }

  var CSS = [
    '#sls-root{position:fixed;inset:0;z-index:2147483002;display:none;align-items:center;justify-content:center;background:rgba(0,0,0,.25);font-family:"Microsoft YaHei","PingFang SC",sans-serif;color:#eee;user-select:none}',
    '#sls-root *{box-sizing:border-box}',
    '.sls-plate{width:340px;display:flex;flex-direction:column;gap:12px;padding:16px 18px;border-radius:10px;border:1px solid rgba(255,255,255,.14);background:linear-gradient(180deg,rgba(30,30,33,.92),rgba(18,18,20,.94));box-shadow:0 10px 40px rgba(0,0,0,.6)}',
    '.sls-title{font-size:16px;font-weight:600;letter-spacing:1px;text-shadow:0 2px 6px rgba(0,0,0,.6)}',
    '.sls-hint{font-size:13px;color:rgba(238,238,238,.6)}',
    '.sls-row{display:flex;align-items:center;gap:10px}',
    '.sls-num{width:76px;height:34px;padding:0 10px;font:inherit;font-size:15px;font-weight:600;text-align:center;color:#EBA308;background:rgba(10,10,12,.72);border:1px solid rgba(255,255,255,.14);border-radius:8px;outline:none;user-select:text}',
    '.sls-num:focus{border-color:rgba(255,193,7,.75)}',
    '.sls-num::-webkit-inner-spin-button,.sls-num::-webkit-outer-spin-button{-webkit-appearance:none;margin:0}',
    '.sls-range{flex:1;accent-color:#EBA308;cursor:pointer}',
    '.sls-quick,.sls-foot{display:flex;gap:8px}',
    '.sls-quick .sls-btn{flex:1}',
    '.sls-foot{justify-content:flex-end}',
    '.sls-btn{font:inherit;font-size:13px;color:#dde2f0;padding:7px 14px;border-radius:7px;border:1px solid rgba(255,255,255,.14);background:linear-gradient(180deg,rgba(58,58,60,.9),rgba(38,38,40,.92));cursor:pointer;white-space:nowrap}',
    '.sls-btn:hover{background:linear-gradient(180deg,rgba(72,66,48,.92),rgba(50,46,32,.95));border-color:rgba(255,193,7,.75)}',
    '.sls-btn.sls-go{color:#EBA308;border-color:rgba(255,193,7,.75)}',
    '#sls-toast{position:fixed;left:50%;bottom:170px;transform:translateX(-50%);z-index:2147483003;pointer-events:none;display:none;padding:10px 18px;border-radius:8px;border:1px solid rgba(255,193,7,.75);background:linear-gradient(180deg,rgba(30,30,33,.94),rgba(18,18,20,.96));color:#eee;font-family:"Microsoft YaHei","PingFang SC",sans-serif;font-size:14px;box-shadow:0 6px 20px rgba(0,0,0,.6)}'
  ].join('\n');

  function build() {
    if (ui && document.body.contains(ui.root)) return;

    addStyle();

    var root = el('div');
    root.id = 'sls-root';
    var plate = el('div', 'sls-plate');
    var hint = el('div', 'sls-hint');

    var row = el('div', 'sls-row');
    var number = el('input', 'sls-num');
    number.type = 'number';
    number.min = '1';
    var slider = el('input', 'sls-range');
    slider.type = 'range';
    slider.min = '1';
    row.appendChild(number);
    row.appendChild(slider);

    var quick = el('div', 'sls-quick');
    quick.appendChild(button('1', 'one'));
    quick.appendChild(button('Half', 'half'));
    quick.appendChild(button('Max', 'max'));

    var foot = el('div', 'sls-foot');
    foot.appendChild(button('Cancel', 'cancel'));
    foot.appendChild(button('Split', 'split', 'sls-go'));

    plate.appendChild(el('div', 'sls-title', 'Split stack'));
    plate.appendChild(hint);
    plate.appendChild(row);
    plate.appendChild(quick);
    plate.appendChild(foot);
    root.appendChild(plate);

    var toast = el('div');
    toast.id = 'sls-toast';

    document.body.appendChild(root);
    document.body.appendChild(toast);

    // Nothing under the picker may see its clicks or keys; a press on the dimmed area around it cancels.
    root.addEventListener('mousedown', function (e) {
      e.stopPropagation();

      if (e.target === root) cancel();
    });
    root.addEventListener('click', onClick);
    root.addEventListener('keydown', onKey);
    number.addEventListener('input', function () {
      var units = parseInt(number.value, 10);

      if (!isNaN(units)) slider.value = String(clamp(units));
    });
    slider.addEventListener('input', function () { number.value = slider.value; });

    ui = { root: root, hint: hint, number: number, slider: slider, toast: toast };
  }

  // The style outlives a lost root, so a rebuild must not stack another copy of it.
  function addStyle() {
    if (document.getElementById('sls-style')) return;

    var style = el('style');
    style.id = 'sls-style';
    style.textContent = CSS;
    document.head.appendChild(style);
  }

  function onClick(e) {
    var target = e.target.closest('[data-act]');

    if (!target || !asked) return;

    e.stopPropagation();
    var act = target.getAttribute('data-act');

    if (act === 'one') setUnits(1);
    else if (act === 'half') setUnits(asked.suggested);
    else if (act === 'max') setUnits(asked.max);
    else if (act === 'cancel') cancel();
    else if (act === 'split') commit();
  }

  function onKey(e) {
    e.stopPropagation();

    if (e.key === 'Enter') commit();
    else if (e.key === 'Escape') cancel();
  }

  function clamp(units) {
    return Math.min(asked.max, Math.max(1, units));
  }

  function setUnits(units) {
    var value = String(clamp(units));
    ui.number.value = value;
    ui.slider.value = value;
    ui.number.focus();
    ui.number.select();
  }

  function commit() {
    if (!asked) return;

    var units = parseInt(ui.number.value, 10);

    if (isNaN(units)) {
      setUnits(asked.suggested);

      return;
    }

    var token = asked.token;
    hide();
    send('split|' + token + '|' + clamp(units));
  }

  function cancel() {
    if (!asked) return;

    var token = asked.token;
    hide();
    send('cancel|' + token);
  }

  function hide() {
    asked = null;

    if (ui) ui.root.style.display = 'none';
  }

  function open(token, max, suggested) {
    build();
    asked = { token: token, max: max, suggested: suggested };
    ui.hint.textContent = 'How many to move: 1 to ' + max;
    ui.number.max = String(max);
    ui.slider.max = String(max);
    ui.root.style.display = 'flex';
    setUnits(suggested);
  }

  // Closed by the plugin (the bag closed, Esc, or no answer in time): no answer is sent.
  function close() {
    hide();
  }

  function toast(text) {
    build();
    ui.toast.textContent = text;
    ui.toast.style.display = 'block';
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { ui.toast.style.display = 'none'; }, TOAST_MS);
  }

  window.__slstacking = { open: open, close: close, toast: toast };
})();
