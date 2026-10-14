@echo off
set PATH=%LOCALAPPDATA%\exgrid-layer3\node-v24.14.1-win-x64;%PATH%
cd /d %LOCALAPPDATA%\exgrid-layer3\run-2026-10-11-17\browser
set EXGRID_HOSTING=%1
if "%1"=="wasm" set EXGRID_HOSTING=
set PLAYWRIGHT_JSON_OUTPUT_NAME=%LOCALAPPDATA%\exgrid-layer3\run-2026-10-11-17\fn12e-%1.json
node node_modules\@playwright\test\cli.js test sizing.spec.mjs part-c.spec.mjs --grep "FN-12e" --project=chrome --project=msedge --reporter=list,json > %LOCALAPPDATA%\exgrid-layer3\run-2026-10-11-17\fn12e-%1.log 2>&1
echo exit=%ERRORLEVEL% >> %LOCALAPPDATA%\exgrid-layer3\run-2026-10-11-17\fn12e-%1.log
