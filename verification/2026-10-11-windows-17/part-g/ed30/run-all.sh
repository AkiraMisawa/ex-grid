#!/bin/bash
# Part G, ED-30: every configuration in turn, from WSL. Each run's own log and record are in records/.
R=/mnt/c/Users/<user>/AppData/Local/exgrid-layer3/run-2026-10-11-17/ed30
A='k1+k2+k3+k4+k5+k6+k6x+k6y+k8+k8x+k9+k10+k12+k13+k13x+k15+k16+dc47'
for page in sheet mud features features-mud sheets sheets-mud; do
  for label in wasm server server-150; do
    for ch in chrome msedge; do
      case $page in sheet|mud) cases=$A; [ $label = server-150 ] && cases="$A+k7";; features*) cases=k11;; sheets*) cases=k14;; esac
      echo "$(date +%T) start $page $label $ch"
      (cd /mnt/c && cmd.exe /c "%LOCALAPPDATA%\\exgrid-layer3\\run-2026-10-11-17\\ed30\\run-ime.cmd" $page $label $ch "$cases" < /dev/null)
      echo "$(date +%T) end $page $label $ch: $(tail -2 $R/records/$page-$label-$ch.log | tr -d '\r' | tr '\n' ' ' | cut -c1-300)"
    done
  done
done
echo "$(date +%T) all done"
