# A page that serves itself

**One HTML file.** Open it and the tab connects to a [Portal](https://github.com/gosuda/portal-tunnel)
relay and serves **its own source** at the public address the relay hands back. Open that address
somewhere else and you get the same page, which connects to a relay and serves itself too. Each
visitor's tab is another host; the chain lasts only while tabs stay open.

Nothing is installed and nothing persists. A tab hosts only while it is open, every hop is a person
opening a link, and the page a visitor receives is exactly the one on screen — the heading says so.

## Build

```bash
cd selfhost
./build.sh        # -> selfhost.html (one self-contained file, ~6.7 MB)
```

`build.sh` builds the connector for `GOOS=js GOARCH=wasm`, gzips it, and base64-embeds it in
`template.html` next to Go's `wasm_exec.js`. The browser inflates it with `DecompressionStream`.
The `wasm_exec.js` must come from the same Go version that built the wasm.

## Run

Open `selfhost.html` from disk. It picks a relay the way [`../portal`](../portal) does — `?relay=` if
given, else `https://localhost`, else the relays in Portal's
[`registry.json`](https://github.com/gosuda/portal-tunnel/blob/main/registry.json) that serve
`/sdk/certificate-chain`. The public address appears on the page; open it elsewhere to extend the
chain.

A prebuilt `selfhost.html` is attached to the repository's releases.

## How it serves itself

`main.go` is the connector. Its handler answers every request with `window.selfSource()`, which the
page sets to its own `document.documentElement.outerHTML` — wasm and all — so the copy a visitor
receives is a working host in turn. The reverse session rides a WebSocket and tenant TLS terminates
in the tab, like any Portal browser connector.
