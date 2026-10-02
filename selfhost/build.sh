#!/usr/bin/env bash
# Builds selfhost.html: the Portal connector (GOOS=js) gzipped and base64-embedded into
# template.html, alongside the Go runtime glue. The page serves its own source, so each
# visitor's tab becomes another host. Output is a single self-contained file.
set -euo pipefail
cd "$(dirname "$0")"

GOOS=js GOARCH=wasm go build -trimpath -ldflags "-s -w" -o self.wasm .
gzip -9 -c self.wasm > self.wasm.gz
WASM_EXEC="$(go env GOROOT)/lib/wasm/wasm_exec.js"

python3 - "$WASM_EXEC" <<'PY'
import base64, sys
tmpl = open("template.html").read()
we = open(sys.argv[1]).read()
assert "</script" not in we, "wasm_exec.js carries a closing script tag"
b64 = base64.b64encode(open("self.wasm.gz", "rb").read()).decode()
html = tmpl.replace("/*__WASM_EXEC__*/", we).replace("__WASM_GZ_B64__", b64)
open("selfhost.html", "w").write(html)
print(f"selfhost.html: {len(html.encode()):,} bytes")
PY

rm -f self.wasm self.wasm.gz
