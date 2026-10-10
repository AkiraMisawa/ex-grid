#!/bin/bash
# One browser run against a published host of one variant on port 5799, with the contention checks.
#   run-browser.sh <name> <v0|v1> <script.mjs> <args after base, out and label…>
S=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad/paint
W=/Users/akira338/github/ex-grid/.claude/worktrees/live-data-paint-text
R=$W/verification/2026-10-06-macos-paint-text-cost/raw
name=$1; variant=$2; script=$3; shift 3
PORT=5799
if lsof -ti tcp:$PORT >/dev/null; then echo "port $PORT is taken: $(lsof -ti tcp:$PORT)"; exit 8; fi
node $W/tests/ExGrid.Browser/static-host.mjs $S/hosts/$variant/wwwroot $PORT > $S/host-$name.log 2>&1 &
host=$!
until curl -sf http://localhost:$PORT/ >/dev/null; do sleep 0.3; kill -0 $host 2>/dev/null || { echo "host died"; exit 7; }; done
contended=no
{
  echo "=== browser $name ($variant): before, $(date '+%F %T')"
  waited=$(bash $S/scripts/wait-idle.sh 60); rc=$?; echo "$waited" | tail -1
  if [ $rc -ne 0 ]; then echo "the other agents did not go idle within 60 minutes: run anyway, CONTENDED"; fi
  bash $S/scripts/idle-check.sh
} >> $R/contention.log 2>&1
grep -q "CONTENDED" <(tail -4 $R/contention.log) && contended=yes
OURS_RUNNING=1 node $S/scripts/$script http://localhost:$PORT $R/browser-$(echo $script | sed 's/browser-//; s/\.mjs//').json "$name ($variant, contended $contended)" "$@" > $R/browser-$name.log 2>&1
status=$?
kill $host; wait $host 2>/dev/null
{
  echo "=== browser $name ($variant): after, $(date '+%F %T'), exit $status, contended $contended"
  bash $S/scripts/idle-check.sh; after=$?
  [ $after -ne 0 ] && echo "AFTER-CHECK BUSY: the other agents were working when this run ended"
} >> $R/contention.log 2>&1
grep -q "AFTER-CHECK BUSY" <(tail -40 $R/contention.log | sed -n "/=== .* $name .*after/,\$p") && echo "$name $(date '+%F %T') busy at the after-check" >> $R/contended-runs.txt
exit $status
