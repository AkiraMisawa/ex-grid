param([string]$Culture, [string]$Tag)
$d = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\run-2026-10-11-17'
$o = Join-Path $d 'oracle\ExcelOracle\oracle.ps1'
$ids = (Get-Content (Join-Path $d "ids-$Culture.txt") -Raw).Trim()
Set-Culture $Culture
"set $Culture at " + (Get-Date -Format 'HH:mm:ss')
$now = powershell.exe -NoProfile -Command '(Get-Culture).Name'
"a new PowerShell reads $now"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $o -Id $ids -Out (Join-Path $d "oracle\ExcelOracle\results-2026-10-11-run17-$Culture.json")
