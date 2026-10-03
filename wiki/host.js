// browser-host: publish this FeatherWiki over a Portal relay.
//
// FeatherWiki (https://codeberg.org/Alamantus/FeatherWiki, AGPL-3.0) is a single-file wiki.
// This script, appended to it, lets the tab serve its own current state over a Portal relay
// (https://github.com/gosuda/portal-tunnel). Visitors get the wiki as it stands now in
// FeatherWiki's own published (read-only) form, so editing stays with the owner; a read-only copy
// can host again to pass it on. The connector wasm is fetched at runtime, so the file stays small.
//
// This combined work is AGPL-3.0. Source: https://github.com/naratteu/browser-host (wiki/).
(function () {
  "use strict";
  var WASM_URL = new URLSearchParams(location.search).get("wasm")
    || "https://naratteu.github.io/browser-host/portal-serve.wasm";
  var REGISTRY = "https://raw.githubusercontent.com/gosuda/portal-tunnel/main/registry.json";
  var LOCAL = ["https://localhost"];

  // Captured now, before FeatherWiki may re-render the body, so a served copy can carry them.
  var WASMEXEC_SRC = document.getElementById("portal-wasmexec").textContent;
  var HOST_SRC = document.getElementById("portal-host").textContent;
  var OPEN = "<" + "script", CLOSE = "<" + "/script>";

  function bundle(html) {
    var block = OPEN + " id=portal-wasmexec>" + WASMEXEC_SRC + CLOSE
      + "\n" + OPEN + " id=portal-host>" + HOST_SRC + CLOSE;
    var stripped = html
      .replace(new RegExp(OPEN + " id=portal-wasmexec>[\\s\\S]*?" + CLOSE + "\\s*", "g"), "")
      .replace(new RegExp(OPEN + " id=portal-host>[\\s\\S]*?" + CLOSE + "\\s*", "g"), "");
    var idx = stripped.lastIndexOf("</body>");  // the document close; FeatherWiki's #a script holds "</body>" literals too
    return idx >= 0 ? stripped.slice(0, idx) + block + "\n" + stripped.slice(idx) : stripped + block;
  }

  // --- UI: a small draggable icon that opens a compact panel. Position is remembered. ---
  var POS_KEY = "portal-host-pos";
  function loadPos() { try { return JSON.parse(localStorage.getItem(POS_KEY)); } catch (e) { return null; } }
  function savePos(p) { try { localStorage.setItem(POS_KEY, JSON.stringify(p)); } catch (e) {} }

  var root = document.createElement("div");
  root.setAttribute("style", "position:fixed;z-index:2147483647;font:13px/1.45 ui-sans-serif,system-ui,sans-serif;touch-action:none");
  var ICON = '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"'
    + ' stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="2"/>'
    + '<path d="M16.24 7.76a6 6 0 0 1 0 8.48M7.76 16.24a6 6 0 0 1 0-8.48M19.07 4.93a10 10 0 0 1 0 14.14M4.93 19.07a10 10 0 0 1 0-14.14"/></svg>';
  root.innerHTML =
    '<button id=ph-icon type=button title="포탈로 공유" aria-label="포탈로 공유" style="width:40px;height:40px;border-radius:50%;'
    + 'border:1px solid #2c3532;background:#1a201e;color:#3fc9b5;cursor:grab;display:flex;align-items:center;justify-content:center;'
    + 'box-shadow:0 3px 12px #0006;position:relative;padding:0">' + ICON
    + '<span id=ph-dot hidden style="position:absolute;top:4px;right:4px;width:9px;height:9px;border-radius:50%;background:#3fc9b5;'
    + 'box-shadow:0 0 0 2px #1a201e"></span></button>'
    + '<div id=ph-panel hidden style="position:absolute;width:17rem;max-width:calc(100vw - 24px);background:#1a201e;color:#e4e9e7;'
    + 'border:1px solid #2c3532;border-radius:10px;padding:11px;box-shadow:0 6px 24px #0007">'
    + '<div style="font-weight:700;margin-bottom:5px">포탈로 공유</div>'
    + '<div id=ph-stage style="color:#9aa29d;margin-bottom:8px"></div>'
    + '<button id=ph-go type=button style="font:600 13px ui-sans-serif,system-ui;color:#06211d;background:#3fc9b5;border:0;'
    + 'border-radius:7px;padding:6px 12px;cursor:pointer">호스팅 시작</button>'
    + '<div id=ph-url style="margin-top:8px;word-break:break-all"></div>'
    + '<div id=ph-count style="margin-top:4px;color:#9aa29d"></div></div>';
  document.documentElement.appendChild(root);
  var $ = function (id) { return document.getElementById(id); };
  var icon = $("ph-icon"), panel = $("ph-panel");

  function clamp(p) {
    var w = innerWidth, h = innerHeight;
    return { x: Math.max(4, Math.min(p.x, w - 44)), y: Math.max(4, Math.min(p.y, h - 44)) };
  }
  function place(p) {
    p = clamp(p); root.style.left = p.x + "px"; root.style.top = p.y + "px"; root.style.right = root.style.bottom = "auto";
    // Open the panel toward the side of the screen with more room.
    panel.style.right = p.x > innerWidth / 2 ? "0" : "auto";
    panel.style.left = p.x > innerWidth / 2 ? "auto" : "0";
    panel.style.bottom = p.y > innerHeight / 2 ? "48px" : "auto";
    panel.style.top = p.y > innerHeight / 2 ? "auto" : "48px";
    return p;
  }
  var pos = place(loadPos() || { x: innerWidth - 56, y: innerHeight - 104 });  // clear of FeatherWiki's footer link
  addEventListener("resize", function () { pos = place(pos); });

  // Drag the icon to move; a press without movement toggles the panel.
  var drag = null;
  icon.addEventListener("pointerdown", function (e) {
    drag = { sx: e.clientX, sy: e.clientY, ox: pos.x, oy: pos.y, moved: false };
    icon.setPointerCapture(e.pointerId); icon.style.cursor = "grabbing";
  });
  icon.addEventListener("pointermove", function (e) {
    if (!drag) return;
    var dx = e.clientX - drag.sx, dy = e.clientY - drag.sy;
    if (!drag.moved && Math.abs(dx) + Math.abs(dy) < 5) return;
    drag.moved = true; pos = place({ x: drag.ox + dx, y: drag.oy + dy });
  });
  icon.addEventListener("pointerup", function () {
    if (!drag) return;
    icon.style.cursor = "grab";
    if (drag.moved) savePos(pos); else panel.hidden = !panel.hidden;
    drag = null;
  });
  icon.addEventListener("keydown", function (e) {
    if (e.key === "Enter" || e.key === " ") { e.preventDefault(); panel.hidden = !panel.hidden; }
  });
  function stage(t) { $("ph-stage").textContent = t; }
  window.FW.ready(function () {
    stage(window.FW.state.p.published
      ? "받은 읽기전용 위키를, 탭이 열려 있는 동안 다시 배포합니다."
      : "탭이 열려 있는 동안, 접속하는 사람에게 이 위키의 현재 상태를 읽기전용으로 돌려줍니다. 편집은 이 탭에서만 합니다.");
  });

  // --- Serving ---
  var visits = 0, publicURL = "";
  // Visitors always get FeatherWiki's published (read-only) form, so copies do not fork; only the
  // owner's tab, which holds the editable file, changes the wiki.
  function published() {
    var s = window.FW.state;
    return Object.assign({}, s, { p: Object.assign({}, s.p, { published: true }) });
  }
  window.__portalServe = function () { return bundle(window.FW.gen(published())); };
  window.__portalVisit = function (n) {
    visits = Number(n);
    $("ph-count").textContent = "돌려준 방문: " + visits;
  };

  function within(url, ms) {
    var c = new AbortController(), t = setTimeout(function () { c.abort(); }, ms);
    return fetch(url, { signal: c.signal }).finally(function () { clearTimeout(t); });
  }
  function hostsBrowsers(relay) {
    return within(relay + "/sdk/certificate-chain", 5000)
      .then(function (r) { return r.ok ? r.text() : ""; })
      .then(function (t) { return t.indexOf("-----BEGIN CERTIFICATE-----") === 0; })
      .catch(function () { return false; });
  }
  function candidates() {
    var asked = new URLSearchParams(location.search).getAll("relay");
    if (asked.length) return Promise.resolve(asked);
    return within(REGISTRY, 5000).then(function (r) { return r.json(); })
      .then(function (j) { return LOCAL.concat(j.relays); })
      .catch(function () { return LOCAL.slice(); });
  }

  var started = false;
  $("ph-go").addEventListener("click", function () {
    if (started) return;
    started = true;
    $("ph-go").disabled = true;
    start().catch(function (e) { stage("실패: " + e.message); started = false; $("ph-go").disabled = false; });
  });

  async function start() {
    stage("커넥터(WebAssembly) 받는 중…");
    var buf = await (await fetch(WASM_URL)).arrayBuffer();
    var go = new Go();
    var mod = await WebAssembly.instantiate(buf, go.importObject);
    go.run(mod.instance);
    var name = (crypto.randomUUID && crypto.randomUUID()) || ("wiki-" + Math.random().toString(36).slice(2));
    var relays = await candidates();
    for (var i = 0; i < relays.length; i++) {
      var relay = relays[i].replace(/\/+$/, "");
      stage(relay + " 확인 중…");
      if (!(await hostsBrowsers(relay))) continue;
      stage(relay + " 에 등록 중…");
      try {
        publicURL = await new Promise(function (resolve, reject) {
          window.portalServe(relay, name, resolve, function (e) { reject(new Error(e)); });
        });
        window.portalPublicURL = publicURL;
        stage("호스팅 중 — 이 탭이 서버입니다");
        $("ph-go").hidden = true;
        $("ph-dot").hidden = false;
        $("ph-url").innerHTML = '<a href="' + publicURL + '" target=_blank style="color:#3fc9b5;font-weight:600">' + publicURL + "</a>";
        $("ph-count").textContent = "돌려준 방문: 0";
        return;
      } catch (e) { /* try next relay */ }
    }
    throw new Error("호스팅 가능한 릴레이를 찾지 못함");
  }
})();
