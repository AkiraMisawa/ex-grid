#!/bin/bash
# Waits, polling every 30 s for at most $1 minutes (default 60), until two consecutive idle checks.
# Exit 0 when idle, 1 on timeout. Each check's last line is printed.
D=$(dirname "$0")
limit=$(( ${1:-60} * 2 ))
ok=0
for i in $(seq 1 $limit); do
  if bash $D/idle-check.sh > $D/idle-last.txt 2>&1; then ok=$((ok+1)); else ok=0; fi
  echo "$(date +%H:%M:%S) check $i: ok=$ok $(tail -1 $D/idle-last.txt) $( [ $ok -eq 0 ] && head -1 $D/idle-last.txt )"
  [ $ok -ge 2 ] && { echo "idle"; exit 0; }
  sleep 30
done
echo "timeout"; exit 1
