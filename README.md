# browser-host

A browser tab as a web server: no socket, no listener, no port. The tab reaches a tunnel relay over
what a browser can open, the relay gives it a public address, and the tab answers whoever comes.

| | |
| --- | --- |
| [`portal/`](portal/) | The [Portal](https://github.com/gosuda/portal-tunnel) connector SDK compiled to WebAssembly. The page exposes itself on a public relay under a random GUID and answers with hello world and a shared visit count. **Live: https://naratteu.github.io/browser-host/** |
| [`wiki/`](wiki/) | The real [FeatherWiki](https://codeberg.org/Alamantus/FeatherWiki) with one script appended, so the single-file wiki publishes its current state over Portal and visitors can clone and re-host it. Connector wasm fetched at runtime; file stays ~80 KB. |
| [`selfhost/`](selfhost/) | One HTML file that serves its own source over Portal, so every visitor's tab becomes another host. The build is attached to [releases](https://github.com/naratteu/browser-host/releases). |
| [`tunnelite/`](tunnelite/) | A Blazor WebAssembly page as a [tunnelite](https://github.com/cristipufu/tunnelite) client, refereeing tic-tac-toe between two other browsers. |
