#!/bin/bash
cd "$(dirname "$0")"
for h in wasm server; do for c in chrome msedge; do echo "== t3 $c $h $(date +%T)"; python3 t3_ime.py $c $h > logs/t3-$h-$c.log 2>&1; echo "exit=$?"; done; done
