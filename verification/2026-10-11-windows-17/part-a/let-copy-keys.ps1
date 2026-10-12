$d = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\run-2026-10-11-17'
"culture " + (Get-Culture).Name
$t0 = Get-Date; while ((Get-Process excel -ErrorAction SilentlyContinue) -and ((Get-Date) - $t0).TotalSeconds -lt 20) { Start-Sleep -Milliseconds 500 }
powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $d 'oracle\ExcelOracle\oracle.ps1') -Keys -Cases (Join-Path $d 'let-en-US-copy\ExcelCases') -Id ((Get-Content (Join-Path $d 'ids-keys-en-GB.txt') -Raw).Trim()) -Out (Join-Path $d 'oracle\ExcelOracle\results-2026-10-11-run17-keys-en-GB-cases-typed-under-en-US.json')
