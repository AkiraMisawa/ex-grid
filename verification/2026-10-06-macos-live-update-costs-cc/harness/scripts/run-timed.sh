#!/bin/bash
# Runs one timed CoreCLR configuration of the Costs harness, after the other track is idle, with
# the contention checks before and after it appended to raw/contention.log.
#   run-timed.sh <name> <harness args...>
S=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad
W=/Users/akira338/github/ex-grid/.claude/worktrees/live-data-next-cc
R=$W/verification/2026-10-06-macos-live-update-costs-cc/raw
name=$1; shift
{
  echo "=== $name: before, $(date '+%F %T')"
  bash $S/wait-idle.sh 30 | tail -3
  bash $S/idle-check.sh
  echo "background:"; ps -axo pcpu,etime,command -r 2>/dev/null | head -8 | cut -c1-150
} >> $R/contention.log 2>&1
cd $W
DOTNET_TieredCompilation=0 nix develop -c dotnet spikes/live-update/Costs/bin/Release/net10.0/Costs.dll "$@" 2>&1 | grep -v -e zoxide -e "_ZO_DOCTOR" -e "Please ensure that zoxide" -e "If the issue persists" -e "github.com/ajeetdsouza" > $R/$name.log
status=${PIPESTATUS[0]}
{
  echo "=== $name: after, $(date '+%F %T'), exit $status"
  bash $S/idle-check.sh
} >> $R/contention.log 2>&1
exit $status
