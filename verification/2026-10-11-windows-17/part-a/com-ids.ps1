param([string]$Culture, [string]$IdsFile, [string]$Out)
$d = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\run-2026-10-11-17'
$o = Join-Path $d 'oracle\ExcelOracle\oracle.ps1'
if ((Get-Culture).Name -ne $Culture) { Set-Culture $Culture; "set $Culture at " + (Get-Date -Format 'HH:mm:ss') }
"a new PowerShell reads " + (powershell.exe -NoProfile -Command '(Get-Culture).Name')
$t0 = Get-Date
while ((Get-Process excel -ErrorAction SilentlyContinue) -and ((Get-Date) - $t0).TotalSeconds -lt 20) { Start-Sleep -Milliseconds 500 }
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $o -Id ((Get-Content (Join-Path $d $IdsFile) -Raw).Trim()) -Out (Join-Path $d $Out)
