#!/usr/bin/env bash
set -eu
root=$(git rev-parse --show-toplevel)
artifact="$root/verification/2026-10-06-macos-pivot-memory"
case_name=${1:-instrumented}
export OOM_PROBE=1 OOM_UPDATES=9 OOM_PATH='/live-cost-pivot?b=400'
unset OOM_CSV
case "$case_name" in
  instrumented) ;;
  gc-only) OOM_PATH+='&gc=true' ;;
  no-highlight) OOM_PATH+='&intervention=no-highlight' ;;
  batch-one) OOM_PATH+='&batch=1' ;;
  drop-paints) OOM_PATH+='&intervention=drop-paints'; OOM_UPDATES=20 ;;
  drop-paints-gc) OOM_PATH+='&intervention=drop-paints&gc=true'; OOM_UPDATES=12 ;;
  million-low-cardinality) OOM_PATH='/live-cost-pivot?b=10&outer=100&records=1000000'; OOM_UPDATES=70 ;;
  million-csv)
    export OOM_CSV="$root/.memory-inputs/one-million-trades.csv"
    python3 "$artifact/harness/make-csv.py" "$OOM_CSV"
    ;;
  *) echo "Unknown scenario: $case_name" >&2; exit 2 ;;
esac
export OOM_OUTPUT=${OOM_OUTPUT:-"$root/.memory-rerun/$case_name"}
export EXGRID_HOSTS="$root/.memory-hosts/instrumented" EXGRID_BASE_URL=http://localhost:5499
mkdir -p "$OOM_OUTPUT"
cd "$root"
# The runner starts and stops its own static host. A current host is never reused.
nix develop .#browser -c bash -c 'cd tests/ExGrid.Browser && npx playwright test --config oom.config.mjs --project=chrome'
