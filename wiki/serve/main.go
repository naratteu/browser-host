//go:build js && wasm

// A Portal connector whose handler serves whatever window.__portalServe() returns, so a
// page can publish its current, live state: every request gets the body produced at that
// moment. Visits are reported to window.__portalVisit.
package main

import (
	"context"
	"net/http"
	"sync/atomic"
	"syscall/js"

	"github.com/gosuda/portal-tunnel/v2/portal/identity"
	"github.com/gosuda/portal-tunnel/v2/sdk"
)

func main() {
	var visits atomic.Int64
	js.Global().Set("portalServe", js.FuncOf(func(_ js.Value, args []js.Value) any {
		relayURL, name, onReady, onError := args[0].String(), args[1].String(), args[2], args[3]
		go func() {
			id, err := identity.Generate(name)
			if err != nil {
				onError.Invoke(err.Error())
				return
			}
			ctx := context.Background()
			exposure, err := sdk.Expose(ctx, id, []string{relayURL})
			if err != nil {
				onError.Invoke(err.Error())
				return
			}
			go sdk.RunHTTP(ctx, exposure, http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
				if r.URL.Path == "/favicon.ico" {
					http.NotFound(w, r)
					return
				}
				n := visits.Add(1)
				js.Global().Call("__portalVisit", n, r.URL.Path)
				body := js.Global().Call("__portalServe", r.URL.Path)
				w.Header().Set("Content-Type", "text/html; charset=utf-8")
				w.Header().Set("Cache-Control", "no-store")
				_, _ = w.Write([]byte(body.String()))
			}), "")
			ready, err := exposure.WaitReady(ctx)
			if err != nil || len(ready) == 0 || ready[0].PublicURL == "" {
				onError.Invoke("relay did not become ready")
				return
			}
			onReady.Invoke(ready[0].PublicURL)
		}()
		return nil
	}))
	select {}
}
