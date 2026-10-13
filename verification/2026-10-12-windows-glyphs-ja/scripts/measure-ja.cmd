@echo off
rem The four measurements under the Japanese display language: system-ui and roboto, in Chrome and in Edge, by
rem tests/GlyphWidths/measure.mjs (f5b9f093), from a copy of the repository's files it reads, so the English records in
rem the repository are not overwritten. Writes <face>.windows.<browser>.json in the copy; the session renames them.
set PATH=%LOCALAPPDATA%\exgrid-layer3\node-v24.14.1-win-x64;%PATH%
set G=%LOCALAPPDATA%\exgrid-layer3\glyphs-ja-2026-10-12
set CHROME=C:\Program Files (x86)\Google\Chrome\Application\chrome.exe
cd /d %G%\repo
echo start %DATE% %TIME% > %G%\measure.log
node --version >> %G%\measure.log
node tests\GlyphWidths\measure.mjs system-ui --browser chrome --chrome "%CHROME%" >> %G%\measure.log 2>&1
echo system-ui chrome exit=%ERRORLEVEL% %TIME% >> %G%\measure.log
node tests\GlyphWidths\measure.mjs roboto --browser chrome --chrome "%CHROME%" >> %G%\measure.log 2>&1
echo roboto chrome exit=%ERRORLEVEL% %TIME% >> %G%\measure.log
node tests\GlyphWidths\measure.mjs system-ui --browser msedge >> %G%\measure.log 2>&1
echo system-ui msedge exit=%ERRORLEVEL% %TIME% >> %G%\measure.log
node tests\GlyphWidths\measure.mjs roboto --browser msedge >> %G%\measure.log 2>&1
echo roboto msedge exit=%ERRORLEVEL% %TIME% >> %G%\measure.log
echo end %DATE% %TIME% >> %G%\measure.log
