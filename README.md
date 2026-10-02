# browser-host

A browser tab as a web server: no socket, no listener, no port. The tab reaches a tunnel relay over
what a browser can open, the relay gives it a public address, and the tab answers whoever comes.

| | |
| --- | --- |
| [`portal/`](portal/) | The [Portal](https://github.com/gosuda/portal-tunnel) connector SDK compiled to WebAssembly. The page exposes itself on a public relay under a random GUID and answers with hello world and a shared visit count. **Live: https://naratteu.github.io/browser-host/** |
| [`tunnelite/`](tunnelite/) | A Blazor WebAssembly page as a [tunnelite](https://github.com/cristipufu/tunnelite) client, refereeing tic-tac-toe between two other browsers. |
