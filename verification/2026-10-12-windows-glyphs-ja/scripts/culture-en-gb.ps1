$d = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\glyphs-ja-2026-10-12'
$before = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\area-2026-10-12b\international-before.reg'
reg export "HKCU\Control Panel\International" "$d\international-now.reg" /y | Out-Null
"now    " + (Get-FileHash "$d\international-now.reg").Hash + " culture " + (powershell.exe -NoProfile -Command '(Get-Culture).Name')
Set-Culture en-GB
Start-Sleep -Seconds 1
reg export "HKCU\Control Panel\International" "$d\international-after.reg" /y | Out-Null
"before " + (Get-FileHash $before).Hash
"after  " + (Get-FileHash "$d\international-after.reg").Hash
"a new PowerShell reads " + (powershell.exe -NoProfile -Command '(Get-Culture).Name')
"override " + (Get-WinUILanguageOverride | % { $_.Name })
