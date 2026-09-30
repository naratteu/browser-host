# A browser tab as a web server

A Blazor WebAssembly page that acts as a [tunnelite](https://github.com/cristipufu/tunnelite)
client, so whatever it serves gets a public URL. No socket, no listener, no port.

The CLI forwards each tunneled request to a socket on localhost. A browser cannot open one — but it
does not need to, because the tunnel is already the transport. The hub hands you the request, so the
page answers it in process and posts the response back.

```
  another browser                tunnel server                 this browser tab
  ───────────────                ─────────────                 ────────────────
  GET http://<sub>/     ──────►  holds the request
                                 "NewHttpConnection"  ──────►  hub callback
                                 GET  /tunnelite/…    ◄──────  collect the request
                                 POST /tunnelite/…    ◄──────  IBrowserApp answers
  200 text/html         ◄──────  releases the request
```

## What it serves

Tic-tac-toe, and the rules. Two other browsers open the public URL, get the page from this tab, and
play against each other through it. The host tab is the referee: it decides whose turn it is, which
cells are free and when someone has won, and a move out of turn comes back
`409 {"error":"it is X's turn"}`.

The referee is not infrastructure sitting somewhere the players cannot reach. It is a peer, another
tab, with no more privilege than theirs — it just happens to hold the rules.

## Running it

```bash
dotnet run --project src        # http://localhost:5400
```

Open it, point it at a tunnel server, press **Open the tunnel**, then open the address it shows in
two other browser windows.

## What the tunnel server has to allow

A page served from one origin drives a tunnel server on another, so the server needs CORS on
`/tunnelite/tunnel`, `/tunnelite/request/{id}` and `/wsshttptunnel/negotiate`, plus
`Access-Control-Expose-Headers` — the client reads the tunneled request's own headers off the
response and their names are not known ahead of time.

tunnelite.com does not, and won't
([cristipufu/tunnelite#25](https://github.com/cristipufu/tunnelite/issues/25)): allowing any origin
would let any web page turn its visitors into anonymous tunnel hosts, and every tunnel there shares
one domain's reputation.

So this runs against a relay you host yourself, where you are the only tenant. The policy:

```csharp
builder.Services.AddCors(options => options.AddPolicy("browser-tunnel-client", policy => policy
    .SetIsOriginAllowed(_ => true)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("*")));

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/tunnelite")
            || context.Request.Path.StartsWithSegments("/wsshttptunnel"),
    branch => branch.UseCors("browser-tunnel-client"));
```

Scoped to the control plane rather than applied globally: those headers on tunneled responses would
relax every tunneled app's own policy behind its author's back.

It also needs [cristipufu/tunnelite#24](https://github.com/cristipufu/tunnelite/pull/24) — the
server matches the `X-TR-`/`X-TC-` prefixes case-sensitively, and browsers lowercase header names,
so a browser client's response headers are dropped, `Content-Type` included.

## Limits

- **HTTP only.** WebSocket and SSE tunneling need SignalR streaming, which is possible here but
  unimplemented; those requests are failed fast rather than left to time out. TCP needs a raw socket
  and cannot work in a browser.
- The game polls rather than pushes, for the same reason.
- Game state lives in the tab. Close it and the game is gone, which is the honest behaviour for a
  peer-hosted server.
- Hub negotiation is required, not optional: a server fronted by Azure SignalR answers negotiate
  with a redirect to the service endpoint. `SkipNegotiation = true` connects a WebSocket straight at
  the app, reports `HubConnection started` and then receives nothing.
