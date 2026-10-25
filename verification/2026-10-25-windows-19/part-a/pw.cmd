@echo off
rem %1 = wasm or server; %2 = name; %3.. = playwright args
set PATH=%LOCALAPPDATA%\exgrid-layer3\node-v24.14.1-win-x64;%PATH%
cd /d %LOCALAPPDATA%\exgrid-layer3\run-2026-10-25-19\browser
set EXGRID_HOSTING=%1
set EXGRID_BASE_URL=http://localhost:5317
if "%1"=="wasm" set EXGRID_HOSTING=
if "%1"=="wasm" set EXGRID_BASE_URL=http://localhost:5318
set OUT=%LOCALAPPDATA%\exgrid-layer3\run-2026-10-25-19\out\%2-%1
set PLAYWRIGHT_JSON_OUTPUT_NAME=%OUT%.json
set N=%2
shift
shift
node node_modules\@playwright\test\cli.js test %1 %2 %3 %4 %5 %6 %7 %8 %9 --project=chrome --project=msedge --reporter=list,json > %OUT%.log 2>&1
echo exit=%ERRORLEVEL% >> %OUT%.log
