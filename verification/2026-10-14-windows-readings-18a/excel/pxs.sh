#!/bin/bash
# The colours along one line of a picture in xs/shots (px.ps1): pxs.sh <shot> <x0> <y0> <x1> <y1>
cd /mnt/c && powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\amisa\AppData\Local\exgrid-layer3\readings-18a\px.ps1' "C:\\Users\\amisa\\AppData\\Local\\exgrid-layer3\\readings-18a\\xs\\shots\\$1.png" "${@:2}" < /dev/null | tr -d '\r'
echo
