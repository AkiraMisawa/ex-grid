#!/bin/bash
# The out-of-memory loop (scratchpad/oom/loop.mjs) against a published host of one variant on 5799.
#   run-oom.sh <name> <v0|v1> <a> <b> <redraws>
S=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad/paint
LOOP=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad/oom/loop.mjs
W=/Users/akira338/github/ex-grid/.claude/worktrees/live-data-paint-text
R=$W/verification/2026-10-06-macos-paint-text-cost/raw
name=$1; variant=$2; a=$3; b=$4; n=$5
PORT=5799
if lsof -ti tcp:$PORT >/dev/null; then echo "port $PORT is taken"; exit 8; fi
node $W/tests/ExGrid.Browser/static-host.mjs $S/hosts/$variant/wwwroot $PORT > $S/host-$name.log 2>&1 &
host=$!
until curl -sf http://localhost:$PORT/ >/dev/null; do sleep 0.3; kill -0 $host 2>/dev/null || { echo "host died"; exit 7; }; done
{
  echo "=== oom $name ($variant): before, $(date '+%F %T')"
  bash $S/scripts/wait-idle.sh 60 | tail -1
  bash $S/scripts/idle-check.sh
} >> $R/contention.log 2>&1
echo "# node loop.mjs http://localhost:$PORT $a $b $n \"\" 1   ($variant, $(date '+%F %T'))" > $R/oom-$name.log
OURS_RUNNING=1 node $LOOP http://localhost:$PORT $a $b $n "" 1 >> $R/oom-$name.log 2>&1
status=$?
kill $host; wait $host 2>/dev/null
echo "=== oom $name ($variant): after, $(date '+%F %T'), exit $status" >> $R/contention.log
exit $status
