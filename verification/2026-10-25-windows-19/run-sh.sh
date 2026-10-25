#!/bin/bash
# SH-51 and SH-52 (part_d_sheet, part_d_extra, sh52_speed), then VZ-16 (part_g_vz16), one after another.
cd "$(dirname "$0")"
mkdir -p records shots logs
for h in wasm server; do for c in chrome msedge; do
  echo "== part_d_sheet $c $h $(date +%T)"; python3 part_d_sheet.py $c $h > logs/part-d-sheet-$h-$c.log 2>&1; echo "exit=$?"
done; done
echo "== part_d_extra $(date +%T)"; python3 part_d_extra.py > logs/part-d-extra.log 2>&1; echo "exit=$?"
for h in wasm server-80; do for c in chrome msedge; do
  echo "== sh52_speed $c $h $(date +%T)"; python3 sh52_speed.py $c $h > logs/sh52-speed-$h-$c.log 2>&1; echo "exit=$?"
done; done
for c in chrome msedge; do
  echo "== part_g_vz16 $c $(date +%T)"; python3 part_g_vz16.py $c > logs/part-g-vz16-$c.log 2>&1; echo "exit=$?"
done
python3 part-d-summary.py > part-d-summary.md 2> logs/part-d-summary.err
echo "== done $(date +%T)"
