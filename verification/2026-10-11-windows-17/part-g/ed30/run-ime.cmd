@echo off
rem verify-on-windows-17.md, Part G, ED-30: one run of ime17-probe.mjs.  run-ime.cmd <page> <wasm|server|server-150> <chrome|msedge> <cases>
set PATH=%LOCALAPPDATA%\exgrid-layer3\node-v24.14.1-win-x64;%PATH%
set D=%LOCALAPPDATA%\exgrid-layer3\run-2026-10-11-17
cd /d %D%\browser
set PAGE=%1
set LABEL=%2
set EXGRID_CHANNEL=%3
set CASES=%~4
rem cmd splits its arguments at commas: the cases come joined by +, and are joined by commas here.
set CASES=%CASES:+=,%
set KEYBOARD=japanese
set INPUT=%D%\input-server.ps1
set OUT=%D%\ed30\records\%1-%2-%3.json
set SHOTS=%D%\ed30\shots
set BASE=http://localhost:5299
set RTT=
set CONTROL=
if "%2"=="server" (set BASE=http://localhost:5298& set RTT=0& set CONTROL=http://localhost:7298)
if "%2"=="server-150" (set BASE=http://localhost:5298& set RTT=150& set CONTROL=http://localhost:7298)
node ime17-probe.mjs %BASE% > %D%\ed30\records\%1-%2-%3.log 2>&1
echo exit=%ERRORLEVEL% >> %D%\ed30\records\%1-%2-%3.log
