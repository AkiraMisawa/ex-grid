#!/bin/bash
# One layer-3 harness run against the published hosts, with the contention checks around it.
#   run-browser.sh <name> <wasm|server> KEY=VALUE…   (the KEY=VALUEs are passed as environment)
S=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad
W=/Users/akira338/github/ex-grid/.claude/worktrees/live-data-next-cc
R=$W/verification/2026-10-06-macos-live-update-costs-cc/raw
name=$1; host=$2; shift 2
if [ "$host" = server ]; then base=http://localhost:5598; hosting=server; else base=http://localhost:5599; hosting=wasm; fi
{
  echo "=== browser $name: before, $(date '+%F %T')"
  bash $S/wait-quiet.sh 5 | tail -1
  waited=$(bash $S/wait-idle.sh 60); rc=$?; echo "$waited" | tail -1
  if [ $rc -ne 0 ]; then echo "the other track did not go idle within 60 minutes: run abandoned"; exit 9; fi
  bash $S/idle-check.sh
  echo "background:"; ps -axo pcpu,etime,command -r 2>/dev/null | head -6 | cut -c1-150
} >> $R/contention.log 2>&1
cd $W/tests/ExGrid.Browser
env "$@" EXGRID_COSTS_OUT=$R/browser-results.json EXGRID_HOSTS=$S/hosts EXGRID_BASE_URL=$base EXGRID_HOSTING=$hosting EXGRID_HEADLESS=1 \
  nix develop ../..#browser -c npx playwright test measure-live-costs.spec.mjs --project=chrome 2>&1 \
  | grep -v -e zoxide -e "_ZO_DOCTOR" -e "Please ensure that zoxide" -e "If the issue persists" -e "github.com/ajeetdsouza" > $R/browser-$name.log
status=${PIPESTATUS[0]}
{
  echo "=== browser $name: after, $(date '+%F %T'), exit $status"
  bash $S/idle-check.sh
} >> $R/contention.log 2>&1
exit $status
