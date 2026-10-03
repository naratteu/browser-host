// browser-host: publish this FeatherWiki over a Portal relay.
//
// FeatherWiki (https://codeberg.org/Alamantus/FeatherWiki, AGPL-3.0) is a single-file wiki.
// This script, appended to it, lets the tab serve its own current state over a Portal relay
// (https://github.com/gosuda/portal-tunnel): a visitor to the public address gets the wiki as
// it stands now and can edit, download, and host their own copy. The connector wasm is fetched
// at runtime, so the file stays small.
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

  var visits = 0, publicURL = "";
  var ui = document.createElement("div");
  ui.setAttribute("style",
    "position:fixed;right:12px;bottom:12px;z-index:2147483647;width:19rem;max-width:calc(100vw - 24px);"
    + "font:13px/1.5 ui-sans-serif,system-ui,sans-serif;background:#1a201e;color:#e4e9e7;"
    + "border:1px solid #2c3532;border-radius:10px;padding:12px;box-shadow:0 6px 24px #0007");
  ui.innerHTML =
    '<div style="font-weight:700;margin-bottom:6px">이 위키를 호스팅</div>'
    + '<div id=ph-stage style="color:#9aa29d;margin-bottom:8px">탭이 열려 있는 동안, 접속하는 사람에게 이 위키의 현재 상태를 돌려줍니다.</div>'
    + '<button id=ph-go style="font:600 13px ui-sans-serif,system-ui;color:#06211d;background:#3fc9b5;border:0;border-radius:7px;padding:6px 12px;cursor:pointer">호스팅 시작</button>'
    + '<div id=ph-url style="margin-top:8px;word-break:break-all"></div>'
    + '<div id=ph-count style="margin-top:6px;color:#9aa29d"></div>';
  document.documentElement.appendChild(ui);
  var $ = function (id) { return document.getElementById(id); };
  function stage(t) { $("ph-stage").textContent = t; }

  window.__portalServe = function () { return bundle(window.FW.gen(window.FW.state)); };
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
        $("ph-url").innerHTML = '<a href="' + publicURL + '" target=_blank style="color:#3fc9b5;font-weight:600">' + publicURL + "</a>";
        $("ph-count").textContent = "돌려준 방문: 0";
        return;
      } catch (e) { /* try next relay */ }
    }
    throw new Error("호스팅 가능한 릴레이를 찾지 못함");
  }
})();
