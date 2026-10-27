// A fake root page for picker.js: elements that keep their children and listeners, and a fake Exiled's Toolkit that
// records the panels handed to it.
var window = this;

function setTimeout() { return 1; }
function clearTimeout() {}

class FakeEl {
  constructor(tag) {
    this.tagName = String(tag).toUpperCase();
    this.children = [];
    this.parentNode = null;
    this.style = {};
    this.attributes = {};
    this.listeners = {};
    this.className = '';
    this.id = '';
    this.textContent = '';
  }

  appendChild(child) {
    child.parentNode = this;
    this.children.push(child);

    return child;
  }

  remove() {
    var parent = this.parentNode;

    if (!parent) return;

    parent.children.splice(parent.children.indexOf(this), 1);
    this.parentNode = null;
  }

  contains(node) {
    for (var n = node; n; n = n.parentNode) {
      if (n === this) return true;
    }

    return false;
  }

  setAttribute(name, value) { this.attributes[name] = String(value); }

  getAttribute(name) { return Object.prototype.hasOwnProperty.call(this.attributes, name) ? this.attributes[name] : null; }

  addEventListener(type, fn) { (this.listeners[type] = this.listeners[type] || []).push(fn); }

  querySelector() { return null; }

  focus() {}

  select() {}
}

function findById(node, id) {
  if (node.id === id) return node;

  for (var i = 0; i < node.children.length; i++) {
    var found = findById(node.children[i], id);

    if (found) return found;
  }

  return null;
}

var document = {
  head: new FakeEl('head'),
  body: new FakeEl('body'),
  createElement: function (tag) { return new FakeEl(tag); },
  getElementById: function (id) { return findById(this.head, id) || findById(this.body, id); }
};

window.addEventListener = function () {};

var handed = [];
window.ExiledUi = { size: function (el, options) { handed.push({ el: el, options: options }); } };

function pickerRoot() {
  return document.getElementById('sls-root');
}
