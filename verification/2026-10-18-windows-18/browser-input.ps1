<#
.SYNOPSIS
    Real OS input and window reads for keys-probe.mjs (docs/specs/exsheet/verify-on-windows-11.md,
    Part B), kept running and fed one command per line on stdin; one answer line each ("ok ..." or
    "err ..."). Keys go through SendInput, a virtual-key and a scan code per key, as in the earlier runs'
    input-server.ps1 (verification/2026-10-01-windows-9/, on claude/exsheet-windows-verify-9b).

        powershell -File browser-input.ps1

    find <title prefix>          the visible top-level window whose title starts with it: hwnd, pid
    front <hwnd>                 that window to the front (an Alt tap first only when it is not)
    layout <hwnd> <uk|ja>        the window to the English (UK) or Japanese keyboard
                                 (WM_INPUTLANGCHANGEREQUEST), the IME off (VK_IME_OFF), and the layout
                                 the keys below are mapped through set to the same; then the window's
                                 layout and its IME's open state
    click <x> <y>                a left click at a screen pixel
    chord <mods> <vk hex>        a key by its virtual-key code, with mods held (ctrl, shift, alt, joined
                                 by +, or none)
    ctrlchar <code point hex>    Ctrl with the key that types that character on the layout, and the
                                 Shift it needs
    esc                          Escape
    windows <pid>                the visible top-level windows of a process, front to back (JSON)
    tabs <hwnd>                  the TabItems in the window, through UI Automation: name and whether
                                 selected (JSON)
    shot <hwnd> <path>           every visible top-level window of that window's process, each drawn
                                 by itself (PrintWindow), at its place, cut to the window's rectangle
                                 grown to take in the others; saved as PNG
    quit

    The process is DPI-aware, so coordinates are the screen's physical pixels.
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing, UIAutomationClient, UIAutomationTypes
Add-Type -Namespace BrowserInput -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, int data, UIntPtr extra);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
[DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint thread);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr LoadKeyboardLayout(string id, uint flags);
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
[DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
[DllImport("imm32.dll")] public static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
'@
Add-Type -ReferencedAssemblies UIAutomationClient, UIAutomationTypes, WindowsBase -TypeDefinition @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Text; using System.Threading;
using System.Windows.Automation;
namespace BrowserInput {
public static class Keys {
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit, Size = 40)] struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public KEYBDINPUT ki; }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] i, int size);
    [DllImport("user32.dll")] static extern uint MapVirtualKeyEx(uint code, uint type, IntPtr hkl);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern short VkKeyScanEx(char c, IntPtr hkl);
    public static IntPtr Layout = IntPtr.Zero;
    static readonly HashSet<ushort> Extended = new HashSet<ushort> { 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2D, 0x2E };
    static ushort Scan(ushort vk) { return (ushort)MapVirtualKeyEx(vk, 0, Layout); }
    static void One(ushort vk, bool up) {
        var i = new INPUT[1]; i[0].type = 1; i[0].ki.wVk = vk; i[0].ki.wScan = Scan(vk);
        i[0].ki.dwFlags = (up ? 2u : 0u) | (Extended.Contains(vk) ? 1u : 0u);
        if (SendInput(1, i, Marshal.SizeOf(typeof(INPUT))) != 1) throw new Exception("SendInput failed: " + Marshal.GetLastWin32Error());
    }
    public static string Chord(ushort[] mods, ushort vk) {
        foreach (var m in mods) { One(m, false); Thread.Sleep(20); }
        One(vk, false); Thread.Sleep(30); One(vk, true);
        for (int k = mods.Length - 1; k >= 0; k--) { Thread.Sleep(20); One(mods[k], true); }
        var names = new List<string>();
        foreach (var m in mods) names.Add(m == 0x10 ? "Shift" : m == 0x11 ? "Ctrl" : m == 0x12 ? "Alt" : m.ToString("x2"));
        names.Add("vk 0x" + vk.ToString("x2") + " (scan 0x" + Scan(vk).ToString("x2") + ")");
        return string.Join("+", names);
    }
    public static string CtrlChar(char c) {
        short k = VkKeyScanEx(c, Layout);
        if (k == -1) throw new Exception("no key for character " + ((int)c).ToString("x4"));
        var mods = new List<ushort> { 0x11 };
        if ((k & 0x400) != 0) mods.Add(0x12);
        if ((k & 0x100) != 0) mods.Add(0x10);
        return "vkKeyScan 0x" + ((int)k).ToString("x3") + ": " + Chord(mods.ToArray(), (ushort)(k & 0xff));
    }
}
public static class Win {
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    public static List<Dictionary<string, object>> All(int pid) {
        var list = new List<Dictionary<string, object>>();
        EnumWindows((h, l) => {
            uint p; GetWindowThreadProcessId(h, out p);
            if ((pid == 0 || p == (uint)pid) && IsWindowVisible(h)) {
                var c = new StringBuilder(256); GetClassName(h, c, 256);
                var t = new StringBuilder(512); GetWindowText(h, t, 512);
                RECT r; GetWindowRect(h, out r);
                var d = new Dictionary<string, object>();
                d["hwnd"] = (long)h; d["pid"] = (long)p; d["class"] = c.ToString(); d["title"] = t.ToString();
                d["rect"] = new int[] { r.Left, r.Top, r.Right, r.Bottom };
                list.Add(d);
            }
            return true;
        }, IntPtr.Zero);
        return list;
    }
}
public static class Ui {
    // The TabItems in a window, in the order UI Automation gives them, within ms.
    public static List<Dictionary<string, object>> Tabs(long hwnd, int ms) {
        var o = new List<Dictionary<string, object>>(); Exception err = null;
        var t = new Thread(() => {
            try {
                var root = AutomationElement.FromHandle(new IntPtr(hwnd));
                var all = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));
                foreach (AutomationElement e in all) {
                    var d = new Dictionary<string, object>(); d["name"] = e.Current.Name;
                    object p; if (e.TryGetCurrentPattern(SelectionItemPattern.Pattern, out p)) d["selected"] = ((SelectionItemPattern)p).Current.IsSelected;
                    lock (o) o.Add(d);
                }
            } catch (Exception e) { err = e; }
        });
        t.IsBackground = true; t.SetApartmentState(ApartmentState.MTA); t.Start();
        if (!t.Join(ms)) { var d = new Dictionary<string, object>(); d["error"] = "timed out after " + ms + " ms"; o.Add(d); }
        else if (err != null) { var d = new Dictionary<string, object>(); d["error"] = err.Message.Split('\n')[0]; o.Add(d); }
        return o;
    }
}
}
'@
[void][BrowserInput.Native]::SetProcessDPIAware()
$clock = [Diagnostics.Stopwatch]::StartNew()
$layouts = @{ uk = '00000809'; ja = '00000411' }
[BrowserInput.Keys]::Layout = [BrowserInput.Native]::LoadKeyboardLayout('00000809', 0)
$mods = @{ ctrl = 0x11; shift = 0x10; alt = 0x12 }
function Answer([string]$Text) { [Console]::Out.WriteLine(('{0} t={1}' -f $Text, $clock.ElapsedMilliseconds)); [Console]::Out.Flush() }
function Json($O) { return (ConvertTo-Json -InputObject @($O) -Compress -Depth 6) }   # an array even of one
function Layout-Of([IntPtr]$H) { [uint32]$p = 0; $t = [BrowserInput.Native]::GetWindowThreadProcessId($H, [ref]$p); return ('0x{0:x8}' -f [long][BrowserInput.Native]::GetKeyboardLayout($t)) }
function Ime-Of([IntPtr]$H) {
    $ime = [BrowserInput.Native]::ImmGetDefaultIMEWnd($H)
    if ($ime -eq [IntPtr]::Zero) { return 'no IME window' }
    return ('open={0} conversion=0x{1:x}' -f [long][BrowserInput.Native]::SendMessage($ime, 0x0283, [IntPtr]5, [IntPtr]::Zero), [long][BrowserInput.Native]::SendMessage($ime, 0x0283, [IntPtr]1, [IntPtr]::Zero))
}
function Pid-Of([IntPtr]$H) { [uint32]$p = 0; [void][BrowserInput.Native]::GetWindowThreadProcessId($H, [ref]$p); return [int]$p }

[Console]::Out.WriteLine('ready'); [Console]::Out.Flush()
while ($true) {
    $line = [Console]::In.ReadLine()
    if ($null -eq $line -or $line -eq 'quit') { break }
    $parts = $line.Split(' ', 2)
    $cmd = $parts[0]; $rest = if ($parts.Count -gt 1) { $parts[1] } else { '' }
    try {
        switch ($cmd) {
            'find' {
                $until = (Get-Date).AddSeconds(5); $w = $null
                do { $w = @([BrowserInput.Win]::All(0) | Where-Object { $_['title'].StartsWith($rest) }) | Select-Object -First 1; if (-not $w) { Start-Sleep -Milliseconds 200 } } while (-not $w -and (Get-Date) -lt $until)
                if (-not $w) { throw "no window titled '$rest...'" }
                Answer ('ok find hwnd={0} pid={1}' -f $w['hwnd'], $w['pid'])
            }
            'front' {
                $h = [IntPtr][long]$rest
                if ([BrowserInput.Native]::GetForegroundWindow() -ne $h) {
                    [void][BrowserInput.Keys]::Chord([uint16[]]@(), [uint16]0x12)
                    [void][BrowserInput.Native]::SetForegroundWindow($h); Start-Sleep -Milliseconds 400
                }
                if ([BrowserInput.Native]::GetForegroundWindow() -ne $h) { throw 'the window is not in front' }
                Answer 'ok front'
            }
            'layout' {
                $p = $rest.Split(' '); $h = [IntPtr][long]$p[0]; $was = Layout-Of $h
                $hkl = [BrowserInput.Native]::LoadKeyboardLayout($layouts[$p[1]], 0)
                [void][BrowserInput.Native]::PostMessage($h, 0x0050, [IntPtr]::Zero, $hkl); Start-Sleep -Milliseconds 700
                [BrowserInput.Native]::keybd_event(0x1A, 0, 0, [UIntPtr]::Zero); [BrowserInput.Native]::keybd_event(0x1A, 0, 2, [UIntPtr]::Zero); Start-Sleep -Milliseconds 300
                [BrowserInput.Keys]::Layout = $hkl
                Answer ('ok layout asked={0} hkl=0x{1:x8} was={2} now={3} ime={4}' -f $p[1], [long]$hkl, $was, (Layout-Of $h), (Ime-Of $h))
            }
            'click' {
                $a = $rest.Split(' ') | ForEach-Object { [int]$_ }
                [void][BrowserInput.Native]::SetCursorPos($a[0], $a[1]); Start-Sleep -Milliseconds 40
                [BrowserInput.Native]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 70
                [BrowserInput.Native]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
                Answer 'ok click'
            }
            'chord' {
                $p = $rest.Split(' ')
                $m = @(); if ($p[0] -ne 'none') { $m = @($p[0].Split('+') | ForEach-Object { $mods[$_] }) }
                Answer ('ok chord ' + [BrowserInput.Keys]::Chord([uint16[]]$m, [uint16][Convert]::ToInt32($p[1], 16)))
            }
            'ctrlchar' { Answer ('ok ctrlchar ' + [BrowserInput.Keys]::CtrlChar([char][Convert]::ToInt32($rest, 16))) }
            'esc' { Answer ('ok esc ' + [BrowserInput.Keys]::Chord([uint16[]]@(), [uint16]0x1B)) }
            'windows' { Answer ('ok windows ' + (Json @([BrowserInput.Win]::All([int]$rest)))) }
            'tabs' { Answer ('ok tabs ' + (Json @([BrowserInput.Ui]::Tabs([long]$rest, 4000)))) }
            'shot' {
                $p = $rest.Split(' ', 2); $h = [IntPtr][long]$p[0]
                $wins = @([BrowserInput.Win]::All((Pid-Of $h)))
                $main = $wins | Where-Object { $_['hwnd'] -eq [long]$h } | Select-Object -First 1
                $l = $main['rect'][0]; $t = $main['rect'][1]; $r = $main['rect'][2]; $b = $main['rect'][3]
                foreach ($w in $wins) { $q = $w['rect']; if ($q[2] - $q[0] -gt 0 -and $q[3] - $q[1] -gt 0) { $l = [Math]::Min($l, $q[0]); $t = [Math]::Min($t, $q[1]); $r = [Math]::Max($r, $q[2]); $b = [Math]::Max($b, $q[3]) } }
                $bmp = New-Object Drawing.Bitmap ($r - $l), ($b - $t)
                $g = [Drawing.Graphics]::FromImage($bmp); $g.Clear([Drawing.Color]::FromArgb(255, 64, 64, 64))
                for ($i = $wins.Count - 1; $i -ge 0; $i--) {
                    $q = $wins[$i]['rect']; $ww = $q[2] - $q[0]; $hh = $q[3] - $q[1]
                    if ($ww -le 0 -or $hh -le 0) { continue }
                    $one = New-Object Drawing.Bitmap $ww, $hh
                    $og = [Drawing.Graphics]::FromImage($one); $hdc = $og.GetHdc()
                    $ok = [BrowserInput.Native]::PrintWindow([IntPtr][long]$wins[$i]['hwnd'], $hdc, 2)
                    $og.ReleaseHdc($hdc); $og.Dispose()
                    if ($ok) { $g.DrawImage($one, $q[0] - $l, $q[1] - $t, $ww, $hh) }
                    $one.Dispose()
                }
                $g.Dispose(); $bmp.Save($p[1], [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
                Answer ('ok shot windows={0} box={1},{2},{3},{4}' -f $wins.Count, $l, $t, $r, $b)
            }
            default { Answer "err unknown command $cmd" }
        }
    } catch { Answer ('err ' + ($_.Exception.Message -replace "`r?`n", ' ')) }
}
