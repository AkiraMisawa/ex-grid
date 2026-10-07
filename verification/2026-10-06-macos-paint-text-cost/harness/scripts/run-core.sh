#!/bin/bash
# One timed CoreCLR batch, with the contention checks around it.
#   run-core.sh <name> <V0|V1> <mode> <args after the output path…>
S=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad/paint
W=/Users/akira338/github/ex-grid/.claude/worktrees/live-data-paint-text
R=$W/verification/2026-10-06-macos-paint-text-cost/raw
name=$1; variant=$2; mode=$3; shift 3
if [ "$variant" = V0 ]; then dll=$S/v0/spikes/paint-text/PaintCosts/bin/Release/net10.0/PaintCosts.dll; else dll=$W/spikes/paint-text/PaintCosts/bin/Release/net10.0/PaintCosts.dll; fi
contended=no
{
  echo "=== core $name ($variant): before, $(date '+%F %T')"
  waited=$(bash $S/scripts/wait-idle.sh 60); rc=$?; echo "$waited" | tail -1
  if [ $rc -ne 0 ]; then echo "the other agents did not go idle within 60 minutes: run anyway, CONTENDED"; fi
  bash $S/scripts/idle-check.sh
} >> $R/contention.log 2>&1
grep -q "CONTENDED" <(tail -3 $R/contention.log) && contended=yes
cd $W
DOTNET_TieredCompilation=0 PAINT_VARIANT=$variant nix develop -c dotnet $dll $mode $R/core-$name.json "$@" 2>&1 \
  | grep -v -e zoxide -e "_ZO_DOCTOR" -e "Please ensure that zoxide" -e "If the issue persists" -e "github.com/ajeetdsouza" -e '^$' > $R/core-$name.log
status=${PIPESTATUS[0]}
{
  echo "=== core $name ($variant): after, $(date '+%F %T'), exit $status, contended $contended"
  bash $S/scripts/idle-check.sh; after=$?
  [ $after -ne 0 ] && echo "AFTER-CHECK BUSY: the other agents were working when this run ended"
} >> $R/contention.log 2>&1
grep -q "AFTER-CHECK BUSY" <(tail -40 $R/contention.log | sed -n "/=== .* $name .*after/,\$p") && echo "$name $(date '+%F %T') busy at the after-check" >> $R/contended-runs.txt
exit $status
