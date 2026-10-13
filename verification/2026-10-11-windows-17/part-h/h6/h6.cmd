@echo off
rem Part H, h6: the clipboard probe on one browser and one host.  h6.cmd <chrome|msedge> <wasm|server>
set PATH=%LOCALAPPDATA%\exgrid-layer3\node-v24.14.1-win-x64;%PATH%
set D=%LOCALAPPDATA%\exgrid-layer3\run-2026-10-11-17
cd /d %D%\browser
set EXGRID_CHANNEL=%1
set EXGRID_BASE_URL=http://localhost:5299
if "%2"=="server" set EXGRID_BASE_URL=http://localhost:5298
set EXCEL_DRIVER=%D%\excel-driver.ps1
node clipboard17-probe.mjs > %D%\h6-%1-%2.json 2> %D%\h6-%1-%2.err
echo exit=%ERRORLEVEL% >> %D%\h6-%1-%2.err
powershell -NoProfile -ExecutionPolicy Bypass -File %D%\quit-scratch-excel.ps1 >> %D%\h6-%1-%2.err
