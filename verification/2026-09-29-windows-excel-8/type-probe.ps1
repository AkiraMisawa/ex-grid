# Which way of typing reaches Excel intact: SendKeys under English (UK) and under the Japanese
# keyboard, and keybd_event with scan codes. Plain letters and digits into A1..A6, read back.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. '\\wsl.localhost\Ubuntu-24.04\home\akira\src\hobby\ex-grid\verification\2026-09-27-windows-excel\excel-driver.ps1'
Add-Type -Namespace TP -Name N -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
[DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint thread);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr LoadKeyboardLayout(string id, uint flags);
[DllImport("user32.dll")] public static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint flags);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
[DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint type);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern short VkKeyScan(char c);
'@
$before = @(Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$xl = New-Object -ComObject Excel.Application
[uint32]$p = 0; [void][TP.N]::GetWindowThreadProcessId([IntPtr][long]$xl.Hwnd, [ref]$p)
if ($before -contains [int]$p) { throw 'attached to a running Excel' }
$xl.Visible = $true; $xl.DisplayAlerts = $false; $xl.WindowState = -4137
$wb = $xl.Workbooks.Add(); $ws = $wb.Worksheets.Item(1)
$script:Hwnd = [IntPtr][long]$xl.Hwnd
function Layout { [uint32]$q = 0; $t = [TP.N]::GetWindowThreadProcessId($script:Hwnd, [ref]$q); '0x{0:x8}' -f [long][TP.N]::GetKeyboardLayout($t) }
function Read([string]$a) { [string]$ws.Range($a).Formula2 }
$text = 'abcdefghij1234567890'
Show-Excel $xl
"excel layout at start: " + (Layout) + "; own " + ('0x{0:x8}' -f [long][TP.N]::GetKeyboardLayout(0))
# 1. SendKeys, both on the Japanese keyboard (the earlier runs' way)
[void]$ws.Range('A1').Select(); Send-Keys $xl ($text + '~') 800
"1 SendKeys, JP/JP:        " + (Read 'A1')
# 2. SendKeys slowly, one char at a time
[void]$ws.Range('A2').Select(); Show-Excel $xl
foreach ($c in $text.ToCharArray()) { [System.Windows.Forms.SendKeys]::SendWait([string]$c); Start-Sleep -Milliseconds 40 }
Send-Keys $xl '~' 800
"2 SendKeys char by char:  " + (Read 'A2')
# 3. keybd_event with scan codes, JP
[void]$ws.Range('A3').Select(); Show-Excel $xl
foreach ($c in $text.ToCharArray()) { $vk = [byte]([TP.N]::VkKeyScan($c) -band 0xff); $sc = [byte][TP.N]::MapVirtualKey($vk, 0); [TP.N]::keybd_event($vk, $sc, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 15; [TP.N]::keybd_event($vk, $sc, 2, [UIntPtr]::Zero); Start-Sleep -Milliseconds 25 }
Send-Keys $xl '~' 800
"3 keybd_event, JP:        " + (Read 'A3')
# 4. both to English (UK)
[void][TP.N]::PostMessage($script:Hwnd, 0x0050, [IntPtr]::Zero, [IntPtr]0x08090809); Start-Sleep -Milliseconds 600
$uk = [TP.N]::LoadKeyboardLayout('00000809', 1); [void][TP.N]::ActivateKeyboardLayout($uk, 0)
"excel layout now: " + (Layout) + "; own " + ('0x{0:x8}' -f [long][TP.N]::GetKeyboardLayout(0))
[void]$ws.Range('A4').Select(); Send-Keys $xl ($text + '~') 800
"4 SendKeys, UK/UK:        " + (Read 'A4')
[void]$ws.Range('A5').Select(); Show-Excel $xl
foreach ($c in $text.ToCharArray()) { $vk = [byte]([TP.N]::VkKeyScan($c) -band 0xff); $sc = [byte][TP.N]::MapVirtualKey($vk, 0); [TP.N]::keybd_event($vk, $sc, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 15; [TP.N]::keybd_event($vk, $sc, 2, [UIntPtr]::Zero); Start-Sleep -Milliseconds 25 }
Send-Keys $xl '~' 800
"5 keybd_event, UK:        " + (Read 'A5')
[void][TP.N]::PostMessage($script:Hwnd, 0x0050, [IntPtr]::Zero, [IntPtr]0x04110411); Start-Sleep -Milliseconds 600
[TP.N]::keybd_event(0x1A, 0, 0, [UIntPtr]::Zero); [TP.N]::keybd_event(0x1A, 0, 2, [UIntPtr]::Zero)
"excel layout at end: " + (Layout)
$wb.Close($false); $xl.Quit(); [void][Runtime.InteropServices.Marshal]::ReleaseComObject($ws); [void][Runtime.InteropServices.Marshal]::ReleaseComObject($wb); [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl)
[GC]::Collect(); [GC]::WaitForPendingFinalizers(); Start-Sleep -Seconds 3
if (Get-Process -Id $p -ErrorAction SilentlyContinue) { Stop-Process -Id $p -Force; "stopped $p" } else { "quit $p" }
