#!/bin/bash
# One timed CoreCLR configuration of the Costs harness, after the machine is idle (up to an hour),
# with the checks before and after appended to raw/contention.log; a run whose after-check is busy
# is marked contended there.  run-core.sh <name> <harness args...>   (the output JSON is raw/<name>.json)
D=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad/ld13
W=/Users/akira338/github/ex-grid/.claude/worktrees/ld-measure-after
R=$W/verification/2026-10-07-macos-live-update-costs-after/raw
name=$1; mode=$2; shift 2
{
  echo "=== core $name: before, $(date '+%F %T'), ${DLL:-after build}"
  waited=$(bash $D/wait-idle.sh 60); rc=$?; echo "$waited" | tail -2
  if [ $rc -ne 0 ]; then echo "not idle within 60 minutes: run abandoned"; fi
  echo "background:"; ps -axo pcpu,etime,command -r 2>/dev/null | head -6 | cut -c1-150
} >> $R/contention.log 2>&1
if ! tail -3 $R/contention.log | grep -q '^idle$'; then grep -q "run abandoned" <(tail -3 $R/contention.log) && exit 9; fi
cd $W
start=$(date +%s)
: > $D/during.txt
( while true; do sleep 20; if ! bash $D/idle-check.sh > $D/during-one.txt 2>&1; then echo "$(date +%T) BUSY: $(grep -v '^load' $D/during-one.txt | head -3 | tr '
' ' ' | cut -c1-300)" >> $D/during.txt; fi; done ) &
monitor=$!
DOTNET_TieredCompilation=0 nix develop -c dotnet ${DLL:-$W/spikes/live-update/Costs/bin/Release/net10.0/Costs.dll} $mode $R/$name.json "$@" 2>&1 \
  | grep -v -e zoxide -e "_ZO_DOCTOR" -e "Please ensure that zoxide" -e "If the issue persists" -e "github.com/ajeetdsouza" -e "^$" > $R/$name.log
status=${PIPESTATUS[0]}
kill $monitor 2>/dev/null; wait $monitor 2>/dev/null
{
  if [ -s $D/during.txt ]; then echo "CONTENDED during the run:"; cat $D/during.txt; else echo "clean during the run (checked every 20 s)"; fi
  echo "=== core $name: after, $(date '+%F %T'), $(( $(date +%s) - start )) s, exit $status"
  if bash $D/idle-check.sh > $D/after-check.txt 2>&1; then echo "clean: $(tail -1 $D/after-check.txt)"; else echo "CONTENDED at the end:"; cat $D/after-check.txt; fi
} >> $R/contention.log 2>&1
exit $status
