#!/bin/bash
D=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad/ld13
R=/Users/akira338/github/ex-grid/.claude/worktrees/ld-measure-after/verification/2026-10-07-macos-live-update-costs-after/raw
P="EXGRID_COSTS_PROGRESS=$R/pivot-wasm-progress.jsonl"
for b in 9 100 400; do
  for k in 1 1000; do
    bash $D/run-browser.sh pivot-wasm-1000x$b-$k wasm EXGRID_MEASURE=pivot-costs EXGRID_COSTS_A=1000 EXGRID_COSTS_B=$b EXGRID_COSTS_BATCH=$k $P; echo "pivot $b $k: $?"
  done
done
for b in 9 100 400; do
  bash $D/run-browser.sh pivot-wasm-probe-1000x$b-1 wasm EXGRID_MEASURE=pivot-costs EXGRID_COSTS_A=1000 EXGRID_COSTS_B=$b EXGRID_COSTS_BATCH=1 EXGRID_COSTS_PROBE=1 $P; echo "pivot probe $b: $?"
done
bash $D/run-browser.sh grid-wasm-1000000-1000 wasm EXGRID_MEASURE=live-costs EXGRID_COSTS_ROWS=1000000 EXGRID_COSTS_BATCH=1000; echo "grid 1e6 1000: $?"
for k in 1 100; do bash $D/run-browser.sh grid-wasm-1000000-$k wasm EXGRID_MEASURE=live-costs EXGRID_COSTS_ROWS=1000000 EXGRID_COSTS_BATCH=$k; echo "grid 1e6 $k: $?"; done
for k in 1 100 1000; do bash $D/run-browser.sh grid-wasm-100000-$k wasm EXGRID_MEASURE=live-costs EXGRID_COSTS_ROWS=100000 EXGRID_COSTS_BATCH=$k; echo "grid 1e5 $k: $?"; done
bash $D/run-browser.sh grid-wasm-probe-1000000-1000 wasm EXGRID_MEASURE=live-costs EXGRID_COSTS_ROWS=1000000 EXGRID_COSTS_BATCH=1000 EXGRID_COSTS_PROBE=1; echo "grid probe: $?"
for k in 1000 1; do
  bash $D/run-browser.sh grid-wasm-pushed-1000000-$k wasm EXGRID_MEASURE=live-costs EXGRID_COSTS_ROWS=1000000 EXGRID_COSTS_BATCH=$k EXGRID_COSTS_PUSH=1; echo "pushed $k: $?"
  bash $D/run-browser.sh grid-wasm-pushed-vouched-1000000-$k wasm EXGRID_MEASURE=live-costs EXGRID_COSTS_ROWS=1000000 EXGRID_COSTS_BATCH=$k EXGRID_COSTS_PUSH=1 EXGRID_COSTS_VOUCH=1; echo "pushed vouched $k: $?"
done
for k in 1 100 1000; do bash $D/run-browser.sh grid-server-bytes-1000000-$k server EXGRID_MEASURE=live-bytes EXGRID_COSTS_ROWS=1000000 EXGRID_COSTS_BATCH=$k; echo "bytes $k: $?"; done
bash $D/run-oom.sh oom-1000x100-20 1000 100 20; echo "oom 100: $?"
bash $D/run-oom.sh oom-1000x400-15 1000 400 15; echo "oom 400: $?"
SPEC=measure-pivot.spec.mjs GREP="1,000 changes folded" bash $D/run-browser.sh pv21-live-after wasm EXGRID_MEASURE=pivot; echo "pv21 after: $?"
SPEC=measure-pivot.spec.mjs GREP="1,000 changes folded" HOSTS=$D/v0hosts bash $D/run-browser.sh before-pv21-live wasm EXGRID_MEASURE=pivot; echo "pv21 before: $?"
for b in 9 100; do HOSTS=$D/v0hosts bash $D/run-browser.sh before-pivot-wasm-1000x$b-1 wasm EXGRID_MEASURE=pivot-costs EXGRID_COSTS_A=1000 EXGRID_COSTS_B=$b EXGRID_COSTS_BATCH=1; echo "before pivot $b: $?"; done
echo "browserA done $(date +%T)"
