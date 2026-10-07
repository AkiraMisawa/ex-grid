#!/bin/bash
S=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad
R=/Users/akira338/github/ex-grid/.claude/worktrees/live-data-next-cc/verification/2026-10-06-macos-live-update-costs-cc/raw
B=$S/run-browser.sh
for rows in 100000 1000000; do for k in 1 100 1000; do
  bash $B grid-server-bytes-$rows-$k server EXGRID_MEASURE=live-bytes EXGRID_COSTS_ROWS=$rows EXGRID_COSTS_BATCH=$k EXGRID_COSTS_RUNS=15 EXGRID_COSTS_WARMUP=3; echo "grid-server-bytes-$rows-$k exit $?"
done; done
P=$R/pivot-wasm-progress.jsonl
bash $B pivot-wasm-1000x200-1 wasm EXGRID_MEASURE=pivot-costs EXGRID_COSTS_A=1000 EXGRID_COSTS_B=200 EXGRID_COSTS_BATCH=1 EXGRID_COSTS_RUNS=15 EXGRID_COSTS_WARMUP=3 EXGRID_COSTS_PROGRESS=$P; echo "pivot-wasm-1000x200-1 exit $?"
for k in 1 1000; do
  bash $B pivot-wasm-1000x400-$k wasm EXGRID_MEASURE=pivot-costs EXGRID_COSTS_A=1000 EXGRID_COSTS_B=400 EXGRID_COSTS_BATCH=$k EXGRID_COSTS_RUNS=15 EXGRID_COSTS_WARMUP=3 EXGRID_COSTS_PROGRESS=$P; echo "pivot-wasm-1000x400-$k exit $?"
done
bash $B pivot-wasm-probe-1000x400-1 wasm EXGRID_MEASURE=pivot-costs EXGRID_COSTS_A=1000 EXGRID_COSTS_B=400 EXGRID_COSTS_BATCH=1 EXGRID_COSTS_RUNS=15 EXGRID_COSTS_WARMUP=3 EXGRID_COSTS_PROBE=1 EXGRID_COSTS_PROGRESS=$P; echo "pivot-wasm-probe-1000x400 exit $?"
echo BATCH4 DONE
