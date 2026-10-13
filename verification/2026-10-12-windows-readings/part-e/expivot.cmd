@echo off
set PATH=%LOCALAPPDATA%\exgrid-layer3\node-v24.14.1-win-x64;%PATH%
cd /d %LOCALAPPDATA%\exgrid-layer3\run-2026-10-11-17\browser
set R=%LOCALAPPDATA%\exgrid-layer3\readings-2026-10-12\part-e
node pivot-probe.mjs http://localhost:5299 %R%\items-r3.json %R% %R%\expivot-r3.json > %R%\expivot.log 2>&1
echo exit=%ERRORLEVEL% >> %R%\expivot.log
