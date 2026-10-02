// Tries relays in order until one exposes this tab, then logs every visit it answers.
const REGISTRY_URL = "https://raw.githubusercontent.com/gosuda/portal-tunnel/main/registry.json";
const LOCAL_RELAYS = ["https://localhost"];
const READY_TIMEOUT_MS = 30000;

const $ = (id) => document.getElementById(id);
const name = crypto.randomUUID();

function log(text, kind = "") {
  const line = document.createElement("li");
  line.className = kind;
  line.textContent = `${new Date().toLocaleTimeString()}  ${text}`;
  $("log").prepend(line);
}

window.portalHostVisit = (visit) => {
  $("count").textContent = visit.count;
  log(`#${visit.count} ${visit.method} ${visit.path}  ${visit.userAgent}`, "visit");
};

async function fetchWithin(url, ms) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), ms);
  try {
    return await fetch(url, { signal: controller.signal });
  } finally {
    clearTimeout(timer);
  }
}

// A relay that can host a browser serves its public certificate chain, which the
// connector needs because a browser cannot read it off a TLS handshake.
async function supportsBrowsers(relay) {
  try {
    const chain = await fetchWithin(`${relay}/sdk/certificate-chain`, 5000);
    return chain.ok && (await chain.text()).startsWith("-----BEGIN CERTIFICATE-----");
  } catch {
    return false;
  }
}

async function candidates() {
  const requested = new URLSearchParams(location.search).getAll("relay");
  if (requested.length) return requested;
  let listed = [];
  try {
    listed = (await (await fetchWithin(REGISTRY_URL, 5000)).json()).relays;
  } catch {
    log("could not read registry.json", "bad");
  }
  return [...LOCAL_RELAYS, ...listed];
}

async function main() {
  $("name").textContent = name;
  const go = new Go();
  const wasm = await WebAssembly.instantiateStreaming(fetch("portal.wasm"), go.importObject);
  go.run(wasm.instance);

  for (const raw of await candidates()) {
    const relay = raw.replace(/\/+$/, "");
    if (!(await supportsBrowsers(relay))) {
      log(`${relay}: no browser connector support, skipped`);
      continue;
    }
    log(`${relay}: registering ${name}`);
    try {
      const { publicURL } = await window.portalHost.start(relay, name, READY_TIMEOUT_MS);
      $("relay").textContent = relay;
      $("url").innerHTML = "";
      const link = Object.assign(document.createElement("a"), { href: publicURL, target: "_blank", textContent: publicURL });
      $("url").append(link);
      window.portalPublicURL = publicURL;
      log(`${relay}: serving at ${publicURL}`);
      return;
    } catch (error) {
      log(`${relay}: ${error.message}`, "bad");
    }
  }
  $("url").textContent = "no relay could host this tab";
  log("no relay could host this tab", "bad");
}

main().catch((error) => {
  $("url").textContent = "failed to start";
  log(String(error), "bad");
});
