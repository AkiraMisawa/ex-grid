#!/bin/bash
# One layer-3 harness run against this worktree's published hosts, under the machine-wide layer-3
# lock, after the machine is idle (up to an hour), with checks before, during and after appended to
# raw/contention.log.   run-browser.sh <name> <wasm|server> KEY=VALUE…   (passed as environment)
set -u
S=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad
D=$S/ld13
W=/Users/akira338/github/ex-grid/.claude/worktrees/ld-measure-after
R=$W/verification/2026-10-07-macos-live-update-costs-after/raw
L=$S/layer3.lock
name=$1; host=$2; shift 2
if [ "$host" = server ]; then base=http://localhost:5798; hosting=server; else base=http://localhost:5799; hosting=wasm; fi
echo "=== browser $name: waiting for the layer-3 lock, $(date '+%F %T')" >> $R/contention.log
until mkdir "$L" 2>/dev/null; do sleep 20; done
trap 'rmdir "$L" 2>/dev/null' EXIT INT TERM HUP
export HOLDING_LAYER3_LOCK=1
{
  echo "=== browser $name: lock taken, $(date '+%F %T'), hosts ${HOSTS:-after build}, $*"
  waited=$(bash $D/wait-idle.sh 60); rc=$?; echo "$waited" | tail -2
  if [ $rc -ne 0 ]; then echo "not idle within 60 minutes: run abandoned"; fi
  echo "background:"; ps -axo pcpu,etime,command -r 2>/dev/null | head -6 | cut -c1-150
} >> $R/contention.log 2>&1
tail -4 $R/contention.log | grep -q "run abandoned" && exit 9
for p in 5799 6799 7799 8799 5798 6798 7798 8798; do
  if [ -n "$(lsof -ti tcp:$p)" ]; then echo "port $p in use: run abandoned" >> $R/contention.log; exit 8; fi
done
: > $D/during.txt
( while true; do sleep 20; if ! OURS_RUNNING=1 bash $D/idle-check.sh > $D/during-one.txt 2>&1; then echo "$(date +%T) BUSY: $(grep -v '^load' $D/during-one.txt | head -3 | tr '\n' ' ' | cut -c1-300)" >> $D/during.txt; fi; done ) &
monitor=$!
start=$(date +%s)
cd $W/tests/ExGrid.Browser
env "$@" EXGRID_COSTS_OUT=$R/browser-results.json EXGRID_HOSTS=${HOSTS:-$D/hosts} EXGRID_BASE_URL=$base EXGRID_HOSTING=$hosting EXGRID_HEADLESS=1 \
  nix develop ../..#browser -c npx playwright test ${SPEC:-measure-live-costs.spec.mjs} --project=chrome ${GREP:+--grep "$GREP"} 2>&1 \
  | grep -v -e zoxide -e "_ZO_DOCTOR" -e "Please ensure that zoxide" -e "If the issue persists" -e "github.com/ajeetdsouza" > $R/browser-$name.log
status=${PIPESTATUS[0]}
kill $monitor 2>/dev/null; wait $monitor 2>/dev/null
{
  if [ -s $D/during.txt ]; then echo "CONTENDED during the run:"; cat $D/during.txt; else echo "clean during the run (checked every 20 s)"; fi
  echo "=== browser $name: after, $(date '+%F %T'), $(( $(date +%s) - start )) s, exit $status"
  if bash $D/idle-check.sh > $D/after-check.txt 2>&1; then echo "clean: $(tail -1 $D/after-check.txt)"; else echo "CONTENDED at the end:"; cat $D/after-check.txt; fi
} >> $R/contention.log 2>&1
exit $status
