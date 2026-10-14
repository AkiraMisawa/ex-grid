#!/bin/bash
# Format Cells' (or another dialog's) named controls with their boxes, through UI Automation: dlg.sh [pattern] [title]
cd /mnt/c && powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\amisa\AppData\Local\exgrid-layer3\readings-18a\uia-dlg.ps1' -Title "${2:-Format Cells}" -Pattern "${1:-.}" < /dev/null | tr -d '\r'
