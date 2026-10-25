@echo off
rem verify-on-windows-18.md, Part A, ED-30: one run of ime19-probe.mjs (run 17's).  run-ime.cmd <page> <wasm|server|server-150> <chrome|msedge> <cases> [record name]
set PATH=%LOCALAPPDATA%\exgrid-layer3\node-v24.14.1-win-x64;%PATH%
set D=%LOCALAPPDATA%\exgrid-layer3\run-2026-10-25-19
cd /d %D%\browser
set PAGE=%1
set LABEL=%2
set EXGRID_CHANNEL=%3
set CASES=%~4
rem cmd splits its arguments at commas: the cases come joined by +, and are joined by commas here.
set CASES=%CASES:+=,%
set P=%1
if not "%~5"=="" set P=%~5
set KEYBOARD=japanese
set INPUT=%D%\input-server.ps1
set OUT=%D%\ed30\records\%P%-%2-%3.json
set SHOTS=%D%\ed30\shots
set BASE=http://localhost:5318
set RTT=
set CONTROL=
if "%2"=="server" (set BASE=http://localhost:5317& set RTT=0& set CONTROL=http://localhost:7317)
if "%2"=="server-150" (set BASE=http://localhost:5317& set RTT=150& set CONTROL=http://localhost:7317)
node ime19-probe.mjs %BASE% > %D%\ed30\records\%P%-%2-%3.log 2>&1
echo exit=%ERRORLEVEL% >> %D%\ed30\records\%P%-%2-%3.log
