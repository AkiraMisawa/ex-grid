#!/bin/bash
# Part A, ED-30 (verify-on-windows-18.md): every configuration in turn, from WSL. Each run's own log and record are
# in records/. run-all.sh <pass>: "main" runs every case but k8 and k8x at the user's Predictive input setting;
# "k8" runs k8 and k8x alone on /sheet and /sheet?chrome=mud, with Predictive input off.
R=/mnt/c/Users/amisa/AppData/Local/exgrid-layer3/run-2026-10-18-18/ed30
PASS=${1:-main}
A='k1+k2+k3+k4+k5+k6+k6x+k6y+k9+k10+k12+k13+k13x+k15+k16+dc47'
pages='sheet mud features features-mud sheets sheets-mud'
[ "$PASS" = k8 ] && { A='k8+k8x'; pages='sheet mud'; }
for page in $pages; do
  for label in wasm server server-150; do
    for ch in chrome msedge; do
      case $page in sheet|mud) cases=$A; [ $PASS = main ] && [ $label = server-150 ] && cases="$A+k7";; features*) cases=k11;; sheets*) cases=k14;; esac
      tag=$page; [ "$PASS" = k8 ] && tag=k8-$page
      echo "$(date +%T) start $tag $label $ch"
      (cd /mnt/c && cmd.exe /c "%LOCALAPPDATA%\exgrid-layer3\run-2026-10-18-18\ed30\run-ime.cmd" $page $label $ch "$cases" $tag < /dev/null)
      echo "$(date +%T) end $tag $label $ch: $(tail -2 $R/records/$tag-$label-$ch.log | tr -d '\r' | tr '\n' ' ' | cut -c1-300)"
    done
  done
done
echo "$(date +%T) all done"
