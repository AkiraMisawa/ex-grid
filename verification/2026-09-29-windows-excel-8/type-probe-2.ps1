# Which injected input reaches Excel intact tonight: scan codes only (KEYEVENTF_SCANCODE, as a
# hardware keyboard sends), VK with scan code, and Unicode packets. Each typed three times.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. '\\wsl.localhost\Ubuntu-24.04\home\akira\src\hobby\ex-grid\verification\2026-09-27-windows-excel\excel-driver.ps1'
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices; using System.Threading;
public static class Inj {
  [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Explicit, Size = 40)] struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public KEYBDINPUT ki; }
  [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] i, int size);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint type);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern short VkKeyScan(char c);
  static void One(ushort vk, ushort scan, uint flags) {
    var i = new INPUT[1]; i[0].type = 1; i[0].ki.wVk = vk; i[0].ki.wScan = scan; i[0].ki.dwFlags = flags;
    if (SendInput(1, i, Marshal.SizeOf(typeof(INPUT))) != 1) throw new Exception("SendInput " + Marshal.GetLastWin32Error());
  }
  // mode: "scan" (scan codes only), "vkscan" (VK and scan code), "unicode"
  public static void Type(string text, string mode, int gapMs) {
    foreach (char c in text) {
      if (mode == "unicode") { One(0, c, 4); Thread.Sleep(5); One(0, c, 4 | 2); Thread.Sleep(gapMs); continue; }
      short k = VkKeyScan(c); ushort vk = (ushort)(k & 0xff); bool shift = (k & 0x100) != 0;
      ushort sc = (ushort)MapVirtualKey(vk, 0); ushort shiftSc = (ushort)MapVirtualKey(0x10, 0);
      uint f = mode == "scan" ? 8u : 0u; ushort v = mode == "scan" ? (ushort)0 : vk; ushort sv = mode == "scan" ? (ushort)0 : (ushort)0x10;
      if (shift) { One(sv, shiftSc, f); Thread.Sleep(5); }
      One(v, sc, f); Thread.Sleep(5); One(v, sc, f | 2);
      if (shift) { Thread.Sleep(5); One(sv, shiftSc, f | 2); }
      Thread.Sleep(gapMs);
    }
  }
}
'@
$before = @(Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$xl = New-Object -ComObject Excel.Application
[uint32]$p = 0; [void][ExcelDriver.Native]::GetWindowThreadProcessId([IntPtr][long]$xl.Hwnd, [ref]$p)
if ($before -contains [int]$p) { throw 'attached to a running Excel' }
$xl.Visible = $true; $xl.DisplayAlerts = $false; $xl.WindowState = -4137
$wb = $xl.Workbooks.Add(); $ws = $wb.Worksheets.Item(1)
$script:Hwnd = [IntPtr][long]$xl.Hwnd
$text = "'abcdefghij1234567890=+!`$:,`"&()[]"
$row = 1
foreach ($mode in 'scan', 'vkscan', 'unicode') {
  foreach ($gap in 30, 30, 120) {
    [void]$ws.Range("A$row").Select(); Show-Excel $xl
    [Inj]::Type($text, $mode, $gap); Send-Keys $xl '~' 700
    $got = [string]$ws.Range("A$row").Formula2
    '{0,-8} gap {1,3} ms: {2}  {3}' -f $mode, $gap, $(if ($got -eq $text.Substring(1)) { 'intact ' } else { 'GARBLED' }), $got
    $row++
  }
}
$wb.Close($false); $xl.Quit(); [void][Runtime.InteropServices.Marshal]::ReleaseComObject($ws); [void][Runtime.InteropServices.Marshal]::ReleaseComObject($wb); [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl)
[GC]::Collect(); [GC]::WaitForPendingFinalizers(); Start-Sleep -Seconds 3
if (Get-Process -Id $p -ErrorAction SilentlyContinue) { Stop-Process -Id $p -Force; "stopped $p" } else { "quit $p" }
