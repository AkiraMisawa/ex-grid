param([string]$Culture, [string]$Area, [string]$Id, [switch]$Keys, [string]$Tag)
$d = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\readings-18a'
$o = Join-Path $d 'oracle\ExcelOracle\oracle.ps1'
if ((Get-Culture).Name -ne $Culture) { Set-Culture $Culture; "set $Culture at " + (Get-Date -Format 'HH:mm:ss') }
"a new PowerShell reads " + (powershell.exe -NoProfile -Command '(Get-Culture).Name')
$t0 = Get-Date
while ((Get-Process excel -ErrorAction SilentlyContinue) -and ((Get-Date) - $t0).TotalSeconds -lt 20) { Start-Sleep -Milliseconds 500 }
Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { "Excel still running before the pass: $($_.Id) $($_.StartTime)" }
$out = Join-Path $d "oracle\ExcelOracle\results-2026-10-14-run18a-$Tag.json"
$args2 = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $o, '-Update', '-Out', $out)
if ($Area) { $args2 += @('-Area', $Area) }
if ($Id) { $args2 += @('-Id', $Id) }
if ($Keys) { $args2 += '-Keys' }
"pass $Tag started " + (Get-Date -Format 'HH:mm:ss')
& powershell.exe @args2
"pass $Tag ended " + (Get-Date -Format 'HH:mm:ss')
