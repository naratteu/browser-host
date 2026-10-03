# A self-hosting wiki

[FeatherWiki](https://codeberg.org/Alamantus/FeatherWiki) is a wiki that lives in one HTML file —
you edit it and download the file to save. This adds one thing: the tab can **publish its own current
state** over a [Portal](https://github.com/gosuda/portal-tunnel) relay. Press *호스팅 시작* and the tab
gets a public address; whoever opens it receives the wiki as it stands now, and can edit, download, and
host their own copy from there.

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
`FW.gen(FW.state)` — FeatherWiki's own "download in current state" serializer — with the two scripts
re-appended, so every served copy is both up to date and hostable.

## License

FeatherWiki is **AGPL-3.0**. The assembled `feather-portal.html` combines it with `host.js`, so the
combined file is AGPL-3.0 as well. `host.js` carries that notice, FeatherWiki's own "Powered by Feather
Wiki" link to its source is kept, and the source of our part is this directory.
