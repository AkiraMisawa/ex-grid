#!/bin/bash
D=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad/ld13
R=/Users/akira338/github/ex-grid/.claude/worktrees/ld-measure-after/verification/2026-10-07-macos-live-update-costs-after/raw
# Long enough to cross the redraw a compaction of the source lays out afresh (about every 64 batches).
bash $D/run-oom.sh oom-1000x400-80 1000 400 80; echo "oom 400 long: $?"
bash $D/run-browser.sh pivot-wasm-1000x100-1-long wasm EXGRID_MEASURE=pivot-costs EXGRID_COSTS_A=1000 EXGRID_COSTS_B=100 EXGRID_COSTS_BATCH=1 EXGRID_COSTS_RUNS=80 EXGRID_COSTS_PROGRESS=$R/pivot-wasm-progress.jsonl; echo "pivot 100 long: $?"
bash $D/run-browser.sh pivot-wasm-1000x400-1-long wasm EXGRID_MEASURE=pivot-costs EXGRID_COSTS_A=1000 EXGRID_COSTS_B=400 EXGRID_COSTS_BATCH=1 EXGRID_COSTS_RUNS=80 EXGRID_COSTS_PROGRESS=$R/pivot-wasm-progress.jsonl; echo "pivot 400 long: $?"
bash $D/run-core.sh pivot-1000x400-order pivot 1000 400 1000,1 3 15; echo "core order: $?"
echo "browserB done $(date +%T)"
