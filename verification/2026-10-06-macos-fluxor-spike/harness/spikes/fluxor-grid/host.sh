#!/usr/bin/env bash
# Starts one of the spike's hosts in the background and prints the PID listening on its port:
#   ./host.sh wasm|server [Debug|Release] [log file]
# Stop it with `kill <pid>` — that PID only, never a pattern (AGENTS.md, the pkill trap).
set -euo pipefail
cd "$(dirname "$0")"
which=$1
config=${2:-Debug}
case $which in
  wasm) project=FluxorGrid.Wasm; port=5899 ;;
  server) project=FluxorGrid.Server; port=5898 ;;
  *) echo "wasm or server" >&2; exit 2 ;;
esac
log=${3:-/dev/null}
if lsof -nP -iTCP:$port -sTCP:LISTEN >/dev/null 2>&1; then
  echo "port $port is taken" >&2
  exit 1
fi
nix develop ../..# -c dotnet run --project $project -c "$config" --no-build --launch-profile http >"$log" 2>&1 &
for _ in $(seq 1 120); do
  pid=$(lsof -nP -t -iTCP:$port -sTCP:LISTEN 2>/dev/null | head -1 || true)
  if [ -n "$pid" ]; then echo "$pid"; exit 0; fi
  sleep 0.5
done
echo "the host did not listen on $port" >&2
exit 1
