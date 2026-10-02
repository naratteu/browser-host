//go:build js && wasm

// Command portal serves HTTP from a browser tab through a Portal relay.
//
// It is the Portal SDK compiled to WebAssembly: the tab registers a lease, the relay
// gives it a public URL, and every request to that URL is answered here. The page
// calls portalHost.start(relayURL, name, timeoutMs) for one relay at a time, and each
// request this tab answers is reported to window.portalHostVisit.
package main

import (
	"context"
	"encoding/json"
	"errors"
	"net/http"
	"sync"
	"sync/atomic"
	"syscall/js"
	"time"

	"github.com/gosuda/portal-tunnel/v2/portal/identity"
	"github.com/gosuda/portal-tunnel/v2/sdk"
)

type host struct {
	visits atomic.Int64

	mu       sync.Mutex
	cancel   context.CancelFunc
	exposure *sdk.Exposure
}

func main() {
	h := &host{}
	js.Global().Set("portalHost", map[string]any{
		"start": js.FuncOf(h.start),
		"stop":  js.FuncOf(h.stop),
	})
	select {}
}

// start exposes this tab on relayURL under name and resolves with the public URL, or
// rejects if the relay does not become ready within timeoutMs.
func (h *host) start(_ js.Value, args []js.Value) any {
	relayURL, name, timeout := args[0].String(), args[1].String(), time.Duration(args[2].Int())*time.Millisecond
	return promise(func() (any, error) {
		h.close()

		id, err := identity.Generate(name)
		if err != nil {
			return nil, err
		}
		ctx, cancel := context.WithCancel(context.Background())
		exposure, err := sdk.Expose(ctx, id, []string{relayURL})
		if err != nil {
			cancel()
			return nil, err
		}
		h.mu.Lock()
		h.cancel, h.exposure = cancel, exposure
		h.mu.Unlock()
		go func() { _ = sdk.RunHTTP(ctx, exposure, http.HandlerFunc(h.serve), "") }()

		waitCtx, waitCancel := context.WithTimeout(ctx, timeout)
		defer waitCancel()
		ready, err := exposure.WaitReady(waitCtx)
		if err != nil || len(ready) == 0 || ready[0].PublicURL == "" {
			h.close()
			return nil, errors.Join(errors.New("relay did not become ready"), err)
		}
		return map[string]any{"publicURL": ready[0].PublicURL}, nil
	})
}

func (h *host) stop(js.Value, []js.Value) any {
	return promise(func() (any, error) {
		h.close()
		return nil, nil
	})
}

func (h *host) close() {
	h.mu.Lock()
	cancel, exposure := h.cancel, h.exposure
	h.cancel, h.exposure = nil, nil
	h.mu.Unlock()
	if cancel != nil {
		cancel()
	}
	if exposure != nil {
		_ = exposure.Close()
	}
}

// serve answers every visitor with hello world and the tab's running visit count.
func (h *host) serve(w http.ResponseWriter, r *http.Request) {
	if r.URL.Path == "/favicon.ico" {
		http.NotFound(w, r)
		return
	}
	count := h.visits.Add(1)
	at := time.Now().UTC().Format(time.RFC3339)
	js.Global().Call("portalHostVisit", map[string]any{
		"count":     count,
		"method":    r.Method,
		"path":      r.URL.Path,
		"userAgent": r.UserAgent(),
		"at":        at,
	})
	w.Header().Set("Content-Type", "application/json")
	w.Header().Set("Access-Control-Allow-Origin", "*")
	_ = json.NewEncoder(w).Encode(map[string]any{
		"message": "hello world",
		"count":   count,
		"from":    "a browser tab",
		"at":      at,
	})
}

func promise(run func() (any, error)) js.Value {
	executor := js.FuncOf(func(_ js.Value, args []js.Value) any {
		resolve, reject := args[0], args[1]
		go func() {
			value, err := run()
			if err != nil {
				reject.Invoke(js.Global().Get("Error").New(err.Error()))
				return
			}
			resolve.Invoke(js.ValueOf(value))
		}()
		return nil
	})
	defer executor.Release()
	return js.Global().Get("Promise").New(executor)
}
