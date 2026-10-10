#!/bin/bash
# Waits (at most $1 minutes, polling every 20 s) until no single process other than ours takes more
# than 60% of a core and the other track is idle. Prints the reason each time.
S=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad
for i in $(seq 1 $(( ${1:-15} * 3 ))); do
  hog=$(ps -axo pcpu,comm -r 2>/dev/null | sed -n 2p)
  pct=$(echo "$hog" | awk '{print int($1)}')
  if [ "$pct" -lt 60 ] && bash $S/idle-check.sh >/dev/null 2>&1; then echo "$(date +%T) quiet: top $hog; $(sysctl -n vm.loadavg)"; exit 0; fi
  echo "$(date +%T) busy: top $hog; $(sysctl -n vm.loadavg)"
  sleep 20
done
exit 1
