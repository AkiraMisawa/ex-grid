@echo off
set PATH=%LOCALAPPDATA%\exgrid-layer3\node-v24.14.1-win-x64;%PATH%
cd /d %LOCALAPPDATA%\exgrid-layer3\run-18a\browser
set EXGRID_HOSTING=%1
if "%1"=="wasm" set EXGRID_HOSTING=
set EXGRID_BASE_URL=%2
set PLAYWRIGHT_JSON_OUTPUT_NAME=%LOCALAPPDATA%\exgrid-layer3\run-18a\ed2-%1.json
node node_modules\@playwright\test\cli.js test features.spec.mjs --grep "ED-2" --project=chrome --project=msedge --repeat-each=3 --reporter=list,json > %LOCALAPPDATA%\exgrid-layer3\run-18a\ed2-%1.log 2>&1
echo exit=%ERRORLEVEL% >> %LOCALAPPDATA%\exgrid-layer3\run-18a\ed2-%1.log
