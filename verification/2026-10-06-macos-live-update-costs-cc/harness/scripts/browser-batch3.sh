#!/bin/bash
S=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad
W=/Users/akira338/github/ex-grid/.claude/worktrees/live-data-next-cc
R=$W/verification/2026-10-06-macos-live-update-costs-cc/raw
B=$S/run-browser.sh
# 4x10^5: one redraw, its trace kept (the spec's failure keeps it).
bash $B pivot-wasm-1000x400-diagnostic wasm EXGRID_MEASURE=pivot-costs EXGRID_COSTS_A=1000 EXGRID_COSTS_B=400 EXGRID_COSTS_BATCH=1 EXGRID_COSTS_RUNS=1 EXGRID_COSTS_WARMUP=0; echo "pivot-wasm-1000x400-diagnostic exit $?"
mkdir -p $R/trace-pivot-wasm-1000x400; find $W/tests/ExGrid.Browser/test-results -name '*.zip' -exec cp {} $R/trace-pivot-wasm-1000x400/ \; ; find $W/tests/ExGrid.Browser/test-results -name 'error-context.md' -exec cp {} $R/trace-pivot-wasm-1000x400/ \;
bash $B pivot-wasm-1000x200-1 wasm EXGRID_MEASURE=pivot-costs EXGRID_COSTS_A=1000 EXGRID_COSTS_B=200 EXGRID_COSTS_BATCH=1 EXGRID_COSTS_RUNS=15 EXGRID_COSTS_WARMUP=3; echo "pivot-wasm-1000x200-1 exit $?"
for b in 9 100; do
  bash $B pivot-wasm-probe-1000x$b-1 wasm EXGRID_MEASURE=pivot-costs EXGRID_COSTS_A=1000 EXGRID_COSTS_B=$b EXGRID_COSTS_BATCH=1 EXGRID_COSTS_RUNS=15 EXGRID_COSTS_WARMUP=3 EXGRID_COSTS_PROBE=1; echo "pivot-wasm-probe-1000x$b exit $?"
done
for k in 1 100 1000; do
  bash $B grid-wasm-probe-1000000-$k wasm EXGRID_MEASURE=live-costs EXGRID_COSTS_ROWS=1000000 EXGRID_COSTS_BATCH=$k EXGRID_COSTS_RUNS=15 EXGRID_COSTS_WARMUP=3 EXGRID_COSTS_PROBE=1; echo "grid-wasm-probe-1000000-$k exit $?"
done
for rows in 100000 1000000; do
  bash $B grid-wasm-first-$rows-1 wasm EXGRID_MEASURE=live-costs EXGRID_COSTS_ROWS=$rows EXGRID_COSTS_BATCH=1 EXGRID_COSTS_RUNS=30 EXGRID_COSTS_WARMUP=3; echo "grid-wasm-first-$rows-1 exit $?"
done
for rows in 100000 1000000; do for k in 1 100 1000; do
  bash $B grid-server-bytes-$rows-$k server EXGRID_MEASURE=live-bytes EXGRID_COSTS_ROWS=$rows EXGRID_COSTS_BATCH=$k EXGRID_COSTS_RUNS=15 EXGRID_COSTS_WARMUP=3; echo "grid-server-bytes-$rows-$k exit $?"
done; done
echo BATCH3 DONE
