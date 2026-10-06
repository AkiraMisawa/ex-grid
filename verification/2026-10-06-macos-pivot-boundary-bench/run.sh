#!/usr/bin/env bash
set -euo pipefail
# Run from the repository root, after reserving the exclusive browser/timing slot.
bench_dir="verification/2026-10-06-macos-pivot-boundary-bench"
bench_abs="$(pwd)/$bench_dir"
mkdir -p "$bench_dir/raw" .boundary-publish
git add "$bench_dir"
nix develop -c dotnet run -c Release --project "$bench_dir/Oracle" > "$bench_dir/raw/oracle.log" 2>&1
nix develop -c dotnet publish -c Release "$bench_dir/Client" -o .boundary-publish/client > "$bench_dir/raw/publish-client.log" 2>&1
nix develop -c dotnet publish -c Release "$bench_dir/Server" -o .boundary-publish/server > "$bench_dir/raw/publish-server.log" 2>&1
nix develop .#browser -c npm ci --prefix tests/ExGrid.Browser > "$bench_dir/raw/npm.log" 2>&1
ln -sfn ../../tests/ExGrid.Browser/node_modules "$bench_dir/node_modules"
export BOUNDARY_WWWROOT="$(pwd)/.boundary-publish/client/wwwroot"
export DOTNET_TieredCompilation=0
nix develop -c dotnet .boundary-publish/server/Server.dll --urls http://127.0.0.1:5894 > "$bench_dir/raw/server.log" 2>&1 &
server_pid=$!
nix develop .#browser -c node tests/ExGrid.Browser/latency-proxy.mjs 5895 5894 5896 > "$bench_dir/raw/proxy.log" 2>&1 &
proxy_pid=$!
trap 'kill "$proxy_pid" "$server_pid" 2>/dev/null || true' EXIT
for attempt in {1..100}; do
  if curl --silent --fail http://127.0.0.1:5895/ >/dev/null; then break; fi
  sleep 0.2
done
nix develop .#browser -c node "$bench_dir/measure.mjs" > "$bench_dir/raw/runner.log" 2>&1
python3 "$bench_dir/encoding_sizes.py" > "$bench_dir/raw/encoding-sizes.log"
python3 "$bench_dir/summarize.py"
