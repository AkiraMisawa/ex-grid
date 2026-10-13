param([string]$Cmd)
$d = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\run-2026-10-11-17'
Remove-Item (Join-Path $d "ui-policy-$Cmd.log") -ErrorAction SilentlyContinue
try { $p = Start-Process powershell.exe -Verb RunAs -Wait -PassThru -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $d 'ui-policy.ps1'), $Cmd; "exit $($p.ExitCode)" } catch { "not started: $($_.Exception.Message)" }
Get-Content (Join-Path $d "ui-policy-$Cmd.log") -ErrorAction SilentlyContinue
