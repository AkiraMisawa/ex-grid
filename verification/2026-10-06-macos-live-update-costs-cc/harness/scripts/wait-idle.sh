#!/bin/bash
# Waits, polling every 30 s for at most $1 minutes (default 30), until the other track has been idle
# at two consecutive checks. Exit 0 when idle, 1 on timeout.
S=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad
limit=$(( ${1:-30} * 2 ))
ok=0
for i in $(seq 1 $limit); do
  if bash $S/idle-check.sh > $S/idle-last.txt 2>&1; then ok=$((ok+1)); else ok=0; fi
  echo "$(date +%H:%M:%S) check $i: ok=$ok $(tail -1 $S/idle-last.txt)"
  [ $ok -ge 2 ] && { echo "idle"; exit 0; }
  sleep 30
done
exit 1
