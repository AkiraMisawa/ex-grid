#!/bin/bash
# Starts this record's xl-server.ps1 on Windows, fed by `tail -f cmd.txt`; refuses a second one.
D=/mnt/c/Users/amisa/AppData/Local/exgrid-layer3/pivot-en-us-2026-10-12/xs
HERE=$(dirname "$(readlink -f "$0")")
if pgrep -f "[p]ivot-en-us-2026-10-12.xs.xl-server" > /dev/null; then echo "already running"; exit 1; fi
mkdir -p $D/shots; : > $D/cmd.txt; : > $D/out.txt
cp "$HERE/xl-server.ps1" $D/xl-server.ps1
cd /mnt/c
nohup bash -c "tail -n +1 -f $D/cmd.txt | powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\\Users\\amisa\\AppData\\Local\\exgrid-layer3\\pivot-en-us-2026-10-12\\xs\\xl-server.ps1' 'C:\\Users\\amisa\\AppData\\Local\\exgrid-layer3\\pivot-en-us-2026-10-12\\xs\\shots' >> $D/out.txt 2>&1" > /dev/null 2>&1 &
for i in $(seq 1 100); do grep -q ready $D/out.txt && { tr -d '\r' < $D/out.txt; exit 0; }; sleep 0.2; done; echo "no ready"; cat $D/out.txt
