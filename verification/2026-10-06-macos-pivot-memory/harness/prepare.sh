#!/usr/bin/env bash
set -eu
# Use only a disposable checkout at bd4b2e45; these sample/runner files are not product changes.
root=$(git rev-parse --show-toplevel)
artifact="$root/verification/2026-10-06-macos-pivot-memory"
for name in oom.spec.mjs oom.config.mjs measure-cost.spec.mjs measure-cost.config.mjs; do
  cp "$artifact/harness/$name.txt" "$root/tests/ExGrid.Browser/$name"
done
cp "$artifact/harness/LiveCostPage.instrumented.razor.txt" "$root/samples/ExGrid.DemoPages/Pages/LiveCostPage.razor"
git add "$root/samples/ExGrid.DemoPages/Pages/LiveCostPage.razor" "$root/tests/ExGrid.Browser/oom.spec.mjs" "$root/tests/ExGrid.Browser/oom.config.mjs" "$root/tests/ExGrid.Browser/measure-cost.spec.mjs" "$root/tests/ExGrid.Browser/measure-cost.config.mjs"
cd "$root"
nix develop .#browser -c bash -c 'cd tests/ExGrid.Browser && npm ci'
nix develop -c dotnet publish samples/ExGrid.DemoHost -c Release -o "$root/.memory-hosts/instrumented/wasm"
