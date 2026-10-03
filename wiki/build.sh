#!/usr/bin/env bash
# Assembles feather-portal.html: the real FeatherWiki with host.js appended, so the tab can
# publish its current state over a Portal relay. The connector wasm is fetched at runtime
# (see host.js), so this file stays small (~80 KB). Output is shared as a gist/release asset.
#
# FeatherWiki is AGPL-3.0 (https://codeberg.org/Alamantus/FeatherWiki); the combined file is too.
set -euo pipefail
cd "$(dirname "$0")"

FW_VERSION="1.9.1"
FW_FILE="FeatherWiki_Goldfinch.html"
FW_URL="https://codeberg.org/Alamantus/FeatherWiki/releases/download/${FW_VERSION}/${FW_FILE}"
FW_SHA256="bf94a3f75604ee3f2a515d03d4dcc8d4bfc268459c43f07bd896cba33bd41f79"

curl -sL -o FeatherWiki.html "$FW_URL"
echo "$FW_SHA256  FeatherWiki.html" | shasum -a 256 -c -

WASM_EXEC="$(go env GOROOT)/lib/wasm/wasm_exec.js"

python3 - "$WASM_EXEC" <<'PY'
import sys
fw = open("FeatherWiki.html").read()
we = open(sys.argv[1]).read()
host = open("host.js").read()
assert "</script" not in we and "</script" not in host, "a payload carries a closing script tag"
O, C = "<" + "script", "<" + "/script>"
block = f"{O} id=portal-wasmexec>{we}{C}\n{O} id=portal-host>{host}{C}\n"
idx = fw.rfind("</body>")  # the document's own close; FeatherWiki's #a script contains "</body>" literals too
assert idx != -1, "FeatherWiki has no </body>"
out = fw[:idx] + block + fw[idx:]
open("feather-portal.html", "w").write(out)
print(f"feather-portal.html: {len(out.encode()):,} bytes")
PY

rm -f FeatherWiki.html
