# A browser tab as a web server, over Portal

**Live:** https://naratteu.github.io/browser-host/

The [Portal](https://github.com/gosuda/portal-tunnel) connector SDK, compiled to WebAssembly. Open the
page and the tab registers itself on a relay under a random name, which gives it a public HTTPS
address. Anyone who opens that address is answered by the tab:

```json
{"message":"hello world","count":3,"from":"a browser tab","at":"2026-10-02T03:35:19Z"}
```

`count` is shared across all visitors, and the page logs every visit as it answers it.

## Which relay

The page tries, in order:

1. the relays given as `?relay=` query parameters, if any — and only those;
2. a relay on `https://localhost`;
3. the relays in Portal's [`registry.json`](https://github.com/gosuda/portal-tunnel/blob/main/registry.json).

It skips any relay that does not serve `/sdk/certificate-chain`. A browser cannot read the relay's
certificate chain off a TLS handshake, and the connector needs it for tenant TLS. Relays serve it
from the release that added the browser connector
([gosuda/portal-tunnel#544](https://github.com/gosuda/portal-tunnel/pull/544)), so a public relay
works here once it runs that release.

A local relay with a self-signed certificate only works once the browser trusts it, for example by
opening `https://localhost` and accepting the warning first. Browsers may also ask before a public
page reaches `localhost`.

## How

`main.go` is the whole connector. It calls `sdk.Expose` with one relay, waits until the relay
reports it ready, and runs an `http.Handler` with `sdk.RunHTTP`. The handler counts visits, reports
each one to the page through `window.portalHostVisit`, and answers with JSON. The reverse session
rides a WebSocket, and tenant TLS terminates in the tab.

`web/` is the page. The Pages workflow builds `portal.wasm` into it and copies `wasm_exec.js` from
the Go toolchain.

```bash
cd portal
GOOS=js GOARCH=wasm go build -o web/portal.wasm .
cp "$(go env GOROOT)/lib/wasm/wasm_exec.js" web/
python3 -m http.server -d web 8080   # http://localhost:8080/?relay=https://your-relay
```
