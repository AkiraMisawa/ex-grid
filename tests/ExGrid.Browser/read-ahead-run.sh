#!/usr/bin/env bash
# LAB ONLY, never merged (lab/remote-read-ahead): ticket 14's measurement, end to end.
#
# Starts the published demo API server, latency-proxy.mjs in front of it at the port the pages look
# for it on (theirs plus 3000), and runs measure-read-ahead.spec.mjs against the published
# WebAssembly DemoHost, which the suite's config serves from EXGRID_HOSTS (static-host.mjs). The
# proxy holds every byte between the browser and the server for half the round trip each way, as
# ADR-0151's experiment did; the spec sets it to EXGRID_MEASURE_RTT (150 ms by default).
#
#   EXGRID_HOSTS=/path/to/hosts [EXGRID_PUBLISH=1] [EXGRID_PORT=5392] bash read-ahead-run.sh [playwright arguments]
#
# EXGRID_PUBLISH=1 publishes the hosts first, in Release, as CI's hosts job does. EXGRID_CONFIG names
# the Playwright config (a local one launching Playwright's own Chromium, say). EXGRID_FLAKE names the
# flake whose shells it runs in, when this checkout's own cannot be read (a git worktree, for nix).
# The trades live in EXGRID_DEMO_DATA, the hosts' data/ by default, never another run's directory.
# Ports: the page's EXGRID_PORT, the proxy at +3000, the server itself at +4000, the proxy's control
# at +2000. EXGRID_MEASURE_* narrow the run (measure-read-ahead.spec.mjs).
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
flake="${EXGRID_FLAKE:-$repo}"
hosts="$(cd "${EXGRID_HOSTS:?EXGRID_HOSTS names the published hosts: wasm and api}" && pwd)"
port="${EXGRID_PORT:-5392}"
proxy_port=$((port + 3000))
api_port=$((port + 4000))
control_port=$((port + 2000))
trades="${EXGRID_DEMO_TRADES:-20000}"
data="${EXGRID_DEMO_DATA:-$hosts/data}"

if [ "${EXGRID_PUBLISH:-0}" = 1 ]; then
    nix develop "$flake" -c dotnet publish "$repo/samples/ExGrid.DemoHost" -c Release -o "$hosts/wasm"
    nix develop "$flake" -c dotnet publish "$repo/samples/ExGrid.DemoApi" -c Release -o "$hosts/api"
fi

(cd "$hosts/api" && ASPNETCORE_ENVIRONMENT=Development EXGRID_DEMO_TRADES="$trades" EXGRID_DEMO_DATA="$data" \
    exec nix develop "$flake" -c dotnet ExGrid.DemoApi.dll --urls "http://localhost:$api_port") > "$hosts/api.log" 2>&1 &
(cd "$here" && exec nix develop "$flake#browser" -c node latency-proxy.mjs "$proxy_port" "$api_port" "$control_port") \
    > "$hosts/proxy.log" 2>&1 &

# Stopped by port: the server with SIGTERM, so it deletes the copy of the trades it served.
stop() {
    fuser -s -k -TERM "$api_port/tcp" || true
    fuser -s -k -TERM "$proxy_port/tcp" "$control_port/tcp" || true
}
trap stop EXIT

# The server answers 503 until its trades are ready; curl retries both that and a refused connection.
curl --silent --show-error --fail --retry 600 --retry-connrefused --retry-delay 1 --retry-max-time 900 \
    "http://localhost:$api_port/api/status" > /dev/null
curl --silent --show-error --fail --retry 60 --retry-connrefused --retry-delay 1 \
    "http://localhost:$proxy_port/api/status" > /dev/null

cd "$here"
EXGRID_BASE_URL="http://localhost:$port" EXGRID_HOSTS="$hosts" EXGRID_MEASURE=read-ahead \
EXGRID_MEASURE_API="http://localhost:$api_port" \
EXGRID_MEASURE_COMMIT="$(git -C "$repo" rev-parse HEAD)" \
    nix develop "$flake#browser" -c xvfb-run --auto-servernum --server-args="-screen 0 2560x1600x24" \
    npx playwright test measure-read-ahead.spec.mjs --project=chrome --config="${EXGRID_CONFIG:-playwright.config.mjs}" "$@"
