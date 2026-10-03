<#
.SYNOPSIS
    Real input for wide-probe.mjs: the OS's own wheel and keys, not the browser's emulated ones.

        powershell -File os-input.ps1 -Title '<window title>' -Keys '^{ADD}'
        powershell -File os-input.ps1 -X 1200 -Y 800 -Move
        powershell -File os-input.ps1 -X 1200 -Y 800 -Wheel -120

    The process is made DPI-aware, so X and Y are the screen's physical pixels. -Title brings the
    window whose title starts with it to the front first (an Alt tap lets a background process
    take the foreground). -Keys goes through SendKeys; -Wheel is one mouse_event of that delta
    (one notch is 120) at the cursor, after moving it there.
#>
param([string]$Title, [string]$Keys, [int]$X = -1, [int]$Y = -1, [switch]$Move, [int]$Wheel = 0)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -Namespace OsInput -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, int data, UIntPtr extra);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
'@
[void][OsInput.Native]::SetProcessDPIAware()
if ($Title) {
    $p = Get-Process | Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle.StartsWith($Title) } | Select-Object -First 1
    if ($null -eq $p) { throw "no window titled '$Title...'" }
    if ([OsInput.Native]::GetForegroundWindow() -ne $p.MainWindowHandle) {
        [OsInput.Native]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [OsInput.Native]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
        [void][OsInput.Native]::SetForegroundWindow($p.MainWindowHandle)
        Start-Sleep -Milliseconds 400
    }
    if ([OsInput.Native]::GetForegroundWindow() -ne $p.MainWindowHandle) { throw "the window '$Title...' is not in front" }
}
if ($X -ge 0) { [void][OsInput.Native]::SetCursorPos($X, $Y); Start-Sleep -Milliseconds 150 }
if ($Move) { [void][OsInput.Native]::SetCursorPos($X + 1, $Y); Start-Sleep -Milliseconds 60; [void][OsInput.Native]::SetCursorPos($X, $Y); Start-Sleep -Milliseconds 150 }
if ($Wheel -ne 0) { [OsInput.Native]::mouse_event(0x0800, 0, 0, $Wheel, [UIntPtr]::Zero); Start-Sleep -Milliseconds 100 }
if ($Keys) { [System.Windows.Forms.SendKeys]::SendWait($Keys); Start-Sleep -Milliseconds 250 }
'ok'
