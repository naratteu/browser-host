//go:build js && wasm

// A Portal connector whose handler serves the page's own source, so each visitor's
// tab becomes another host of the same page. The source comes from JS (selfSource),
// and every visit is reported to JS (onVisit).
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
	js.Global().Set("portalSelfHost", js.FuncOf(func(_ js.Value, args []js.Value) any {
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
				js.Global().Call("onVisit", n, r.URL.Path)
				w.Header().Set("Content-Type", "text/html; charset=utf-8")
				w.Header().Set("Cache-Control", "no-store")
				_, _ = w.Write([]byte(js.Global().Call("selfSource").String()))
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
