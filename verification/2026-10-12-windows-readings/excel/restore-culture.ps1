$d = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\readings-2026-10-12'
Set-Culture en-GB
"set en-GB at " + (Get-Date -Format 'HH:mm:ss')
Start-Sleep -Seconds 1
reg export "HKCU\Control Panel\International" "$d\international-after.reg" /y | Out-Null
"before " + (Get-FileHash "$d\international-before.reg").Hash
"after  " + (Get-FileHash "$d\international-after.reg").Hash
"a new PowerShell reads " + (powershell.exe -NoProfile -Command '(Get-Culture).Name')
Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { "Excel running: $($_.Id) $($_.StartTime)" }
