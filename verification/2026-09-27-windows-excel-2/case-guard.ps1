# A time limit per case for the scripts here that drive Excel case by case (by-hand.ps1,
# active-cell.ps1), dot-sourced after excel-driver.ps1. A runspace of its own ends an Excel that has
# not finished a case in its time: a COM call made while a dialog of Excel's is up can wait for as
# long as the dialog stays, and a script waiting inside that call cannot end it. A dialog that does
# not close then costs the case's time, not the run.
#
#     Start-CaseGuard                  once
#     Set-CaseDue 60                   before a case, once Excel is connected
#     Clear-CaseDue                    after it; $Guard.Ended says whether Excel was ended
$script:Guard = [hashtable]::Synchronized(@{ Due = [DateTime]::MaxValue; Pid = 0; Ended = '' })

function Start-CaseGuard {
    $shell = [PowerShell]::Create()
    [void]$shell.AddScript({
        param($Guard)
        while ($true) {
            Start-Sleep -Milliseconds 500
            if ($Guard.Pid -ne 0 -and [DateTime]::UtcNow -gt $Guard.Due) {
                $Guard.Due = [DateTime]::MaxValue
                try {
                    Stop-Process -Id $Guard.Pid -Force -ErrorAction Stop
                    $Guard.Ended = "Excel (process $($Guard.Pid)) was ended at $(Get-Date -Format 'HH:mm:ss'): the case had not finished in time"
                }
                catch { $Guard.Ended = "Excel (process $($Guard.Pid)) could not be ended: $($_.Exception.Message)" }
            }
        }
    }).AddArgument($script:Guard)
    [void]$shell.BeginInvoke()
    $script:GuardShell = $shell
}

function Set-CaseDue([int]$Seconds) {
    [uint32]$p = 0
    [void][ExcelDriver.Native]::GetWindowThreadProcessId($script:Hwnd, [ref]$p)
    $script:Guard.Pid = [int]$p
    $script:Guard.Ended = ''
    $script:Guard.Due = [DateTime]::UtcNow.AddSeconds($Seconds)
}

function Clear-CaseDue { $script:Guard.Due = [DateTime]::MaxValue }

# A new Excel after one was ended: the ended one can linger in the running object table a moment.
function Reconnect-Excel {
    Start-Sleep -Seconds 2
    try { return (Connect-Excel) } catch { Start-Sleep -Seconds 3; return (Connect-Excel) }
}
