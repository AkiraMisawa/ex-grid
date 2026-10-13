#!/bin/bash
# The fill handle's squares in a picture of xs/shots (handle.ps1), optionally inside a box: hd.sh <shot> [x0 y0 x1 y1]
cd /mnt/c && powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\amisa\AppData\Local\exgrid-layer3\readings-18a\handle.ps1' "C:\\Users\\amisa\\AppData\\Local\\exgrid-layer3\\readings-18a\\xs\\shots\\$1.png" "${@:2}" < /dev/null | tr -d '\r'
