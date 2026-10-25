@echo off
rem SH-42: sh42-probe.mjs <wasm|server> <chrome|msedge>
set PATH=%LOCALAPPDATA%\exgrid-layer3\node-v24.14.1-win-x64;%PATH%
set D=%LOCALAPPDATA%\exgrid-layer3\run-2026-10-25-19
cd /d %D%\browser
set BASE=http://localhost:5318
if "%1"=="server" set BASE=http://localhost:5317
set CHANNEL=%2
set LABEL=%1
set OUT=%D%\sh42\%1-%2.json
set SHOTS=%D%\sh42\shots-%1-%2
set PROFILE=%D%\sh42\profile-%1-%2
set INPUT=%D%\browser-input.ps1
if exist %PROFILE% rmdir /s /q %PROFILE%
mkdir %PROFILE%
mkdir %SHOTS% 2>nul
node sh42-probe.mjs %BASE% > %D%\sh42\%1-%2.log 2>&1
echo exit=%ERRORLEVEL% >> %D%\sh42\%1-%2.log
