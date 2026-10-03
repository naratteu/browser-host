# A self-hosting wiki

[FeatherWiki](https://codeberg.org/Alamantus/FeatherWiki) is a wiki that lives in one HTML file —
you edit it and download the file to save. This adds one thing: the tab can **publish its own current
state** over a [Portal](https://github.com/gosuda/portal-tunnel) relay. Open the share icon, press
*호스팅 시작*, and the tab gets a public address; whoever opens it receives the wiki as it stands now.

Visitors get FeatherWiki's own **published** (read-only) form: no Save, New Page, or Wiki Settings. So
the wiki does not fork at every hop — the owner's tab, which holds the editable file, is the only place
it changes. A read-only copy can host again to pass the wiki on. Published mode is FeatherWiki's own
convention, not a lock: a reader can still unset it in the settings page, as with any published
FeatherWiki.

The connector WebAssembly is fetched at runtime from
[GitHub Pages](https://naratteu.github.io/browser-host/portal-serve.wasm), so the wiki file stays about
80 KB — small enough for a gist.

## Build

```bash
cd wiki
./build.sh        # -> feather-portal.html  (downloads FeatherWiki, appends host.js + wasm_exec.js)
```

`build.sh` downloads a pinned FeatherWiki release (checksum-verified) and appends two scripts before the
closing `</body>`: Go's `wasm_exec.js` and [`host.js`](host.js). `host.js` captures both at load, so the
copy it serves carries them too — that is what lets a visitor's tab host in turn. The serve connector is
[`serve/`](serve/), published to Pages by [`../.github/workflows/pages.yml`](../.github/workflows/pages.yml).

A prebuilt `feather-portal.html` is on the repository's [releases](https://github.com/naratteu/browser-host/releases).

## Run

Open `feather-portal.html` in a browser (from disk is fine). It serves over a relay that supports the
browser connector — pass `?relay=` to force one, else it tries `https://localhost` and the relays in
Portal's [`registry.json`](https://github.com/gosuda/portal-tunnel/blob/main/registry.json) that serve
`/sdk/certificate-chain`. `?wasm=` overrides where the connector is fetched from.

How it serves current state: `host.js` sets the connector's handler to return
`FW.gen(...)` — FeatherWiki's own "download in current state" serializer — with `published` set and the
two scripts re-appended, so every served copy is up to date, read-only, and hostable. A copy is a
snapshot of the moment it was loaded; reload it to catch up.

## License

FeatherWiki is **AGPL-3.0**. The assembled `feather-portal.html` combines it with `host.js`, so the
combined file is AGPL-3.0 as well. `host.js` carries that notice, FeatherWiki's own "Powered by Feather
Wiki" link to its source is kept, and the source of our part is this directory.
