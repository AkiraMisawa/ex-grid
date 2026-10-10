#!/bin/bash
set -u
S=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad
D=$S/ld13; W=/Users/akira338/github/ex-grid/.claude/worktrees/ld-measure-after; L=$S/layer3.lock
until mkdir "$L" 2>/dev/null; do sleep 20; done
trap 'rmdir "$L" 2>/dev/null' EXIT INT TERM HUP
cd $W/tests/ExGrid.Browser
run() { env "$@" EXGRID_COSTS_OUT=$D/smoke-browser.json EXGRID_COSTS_RUNS=2 EXGRID_COSTS_WARMUP=1 EXGRID_HOSTS=$D/hosts EXGRID_BASE_URL=http://localhost:5799 EXGRID_HEADLESS=1 nix develop ../..#browser -c npx playwright test measure-live-costs.spec.mjs --project=chrome 2>&1 | grep -v -i zoxide | tail -25; }
run EXGRID_MEASURE=pivot-costs EXGRID_COSTS_A=100 EXGRID_COSTS_B=9 EXGRID_COSTS_BATCH=1 EXGRID_COSTS_PROBE=1
run EXGRID_MEASURE=live-costs EXGRID_COSTS_ROWS=20000 EXGRID_COSTS_BATCH=100 EXGRID_COSTS_PUSH=1 EXGRID_COSTS_VOUCH=1 EXGRID_COSTS_PROBE=1
