#!/bin/bash
S=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad
B=$S/run-browser.sh
for b in 9 100 400; do for k in 1 1000; do
  bash $B pivot-wasm-1000x$b-$k wasm EXGRID_MEASURE=pivot-costs EXGRID_COSTS_A=1000 EXGRID_COSTS_B=$b EXGRID_COSTS_BATCH=$k EXGRID_COSTS_RUNS=15 EXGRID_COSTS_WARMUP=3; echo "pivot-wasm-1000x$b-$k exit $?"
done; done
for b in 9 100 400; do
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
echo BATCH2 DONE
