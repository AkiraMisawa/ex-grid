param([string]$Culture, [string]$IdsFile)
$d = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\run-2026-10-11-17'
$o = Join-Path $d 'oracle\ExcelOracle\oracle.ps1'
"culture " + (Get-Culture).Name + " at " + (Get-Date -Format 'HH:mm:ss')
$t0 = Get-Date
while ((Get-Process excel -ErrorAction SilentlyContinue) -and ((Get-Date) - $t0).TotalSeconds -lt 20) { Start-Sleep -Milliseconds 500 }
Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { "Excel still running before the pass: $($_.Id) $($_.StartTime)" }
$ids = (Get-Content (Join-Path $d $IdsFile) -Raw).Trim()
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $o -Keys -Id $ids -Out (Join-Path $d "oracle\ExcelOracle\results-2026-10-11-run17-keys-$Culture.json")
