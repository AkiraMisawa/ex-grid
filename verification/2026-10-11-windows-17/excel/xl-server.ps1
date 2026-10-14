<#
.SYNOPSIS
    docs/specs/exsheet/verify-on-windows-17.md, Parts B, C and D in Excel: a visible Excel of this
    script's own, driven with real keys and the real mouse, kept running and fed one command per line on
    stdin, so that each step can be read before the next is chosen. One answer per line, "ok ..." or
    "err ...", with the milliseconds since the start.

        powershell -File xl-server.ps1 <directory for pictures>

    start                          an Excel of this script's own (New-Object; refused if it is an Excel
                                   already running), visible and maximised, a new workbook of one sheet
                                   at 100%, A1 selected; the Excel window's keyboard to English (UK)
    stop                           its workbooks closed unsaved, Quit; Stop-Process on its own process
                                   only when it has not quit after 10 s
    front                          Excel to the front (an Alt tap only when Excel is not in front)
    keys <gap ms> <spec>           keys through SendInput, a virtual-key and a scan code per key, <gap>
                                   between keys. {NAME} or {NAME n} a named key (ALT, F1-F12, ENTER,
                                   ESC, TAB, HOME, END, arrows, DEL, BS, SPACE, PGUP, PGDN, APPS); the
                                   prefixes + ^ % (Shift, Ctrl, Alt held) before a {NAME} or a
                                   character; {c} a character the notation reserves; any other
                                   character as itself, through the UK layout
    click|dblclick|rclick|shiftclick|ctrlclick <x> <y>
                                   the pointer moved there as a hand moves it (15 steps of 15 ms), then
                                   the press; screen pixels (the process is DPI-aware)
    move <x> <y>                   the pointer moved there, no press
    shot <name>                    every visible window of this Excel drawn by itself (PrintWindow,
                                   PW_RENDERFULLCONTENT), back to front, into one picture of the work
                                   area: <dir>\<name>.png
    screen <name> <x> <y> <w> <h>  the screen there (CopyFromScreen): <dir>\<name>.png
    crop <src> <x> <y> <w> <h> <dst> <scale>
                                   a part of a picture, enlarged <scale> times (nearest neighbour)
    ready                          whether Excel answers COM (it refuses every call while it edits)
    cell <A1> [<A1> ...]           Value2 (with its type), Formula2, Text and NumberFormat (en-US) of
                                   each, through COM
    geo <A1> [<A1> ...]            each cell's box in screen pixels (PointsToScreenPixels), and the
                                   window's
    uia                            the places of the Formula Bar, the Name Box, the status bar and the
                                   sheet tabs, through UI Automation: only while nothing is typed
    windows                        every visible top-level window of this Excel: handle, class, title,
                                   place
    allwindows                     every visible top-level window: handle, class, process, place
    clipclear                      the clipboard emptied
    clipboard                      the clipboard's text, as JSON (retried while another process holds it)
    imeoff                         VK_IME_OFF
    layout                         the keyboard layout of Excel's window
    setup <A1> <text>              setup only, through COM: Formula2 of a cell (an "=" text) or its
                                   Value2
    addsheet <name>                setup only, through COM: a sheet added after the last
    select <range>                 setup only, through COM: a range selected
    clearsheet                     setup only: the active sheet's Tables removed, its cells cleared
    table <range> <name>           setup only: a range made a Table of that name, headers in its first row
    setfmt <A1> <format>           setup only, through COM: a Number Format (en-US), on an empty cell
    colwidth <A1> [<A1> ...]       each cell's column width and UseStandardWidth, and the sheet's
                                   StandardWidth
    savecols                       the <cols> of the active sheet in a copy saved as .xlsx to the
                                   temporary folder (customWidth), the copy deleted at once
    quit
#>
param([string]$Dir)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing, UIAutomationClient, UIAutomationTypes
Add-Type -Namespace Xl -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
[DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, int data, UIntPtr extra);
[DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint thread);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr LoadKeyboardLayout(string id, uint flags);
[DllImport("user32.dll")] public static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint flags);
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
'@
Add-Type -TypeDefinition @'
using System; using System.Collections.Generic; using System.Diagnostics; using System.Runtime.InteropServices; using System.Text;
namespace Xl {
public static class Keys {
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit, Size = 40)] public struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] KEYBDINPUT ki;
        public static INPUT Key(ushort vk, bool up, bool extended) {
            var i = new INPUT(); i.type = 1; i.ki.wVk = vk; i.ki.wScan = (ushort)MapVirtualKey(vk, 0); i.ki.dwFlags = (up ? 2u : 0u) | (extended ? 1u : 0u); return i;
        }
    }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] i, int size);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern short VkKeyScan(char c);
    public static void Send(List<INPUT> events) {
        var a = events.ToArray();
        if (SendInput((uint)a.Length, a, Marshal.SizeOf(typeof(INPUT))) != a.Length) throw new Exception("SendInput failed: " + Marshal.GetLastWin32Error());
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
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    // Visible top-level windows, front to back: of one process (pid > 0) or of every process (0).
    public static List<string[]> Of(int pid) {
        var list = new List<string[]>();
        EnumWindows((h, l) => {
            if (!IsWindowVisible(h)) return true;
            uint p; GetWindowThreadProcessId(h, out p);
            if (pid > 0 && p != pid) return true;
            var c = new StringBuilder(256); GetClassName(h, c, 256);
            var t = new StringBuilder(256); GetWindowText(h, t, 256);
            RECT r; GetWindowRect(h, out r);
            if (r.Right - r.Left <= 0 || r.Bottom - r.Top <= 0) return true;
            string proc = ""; try { proc = Process.GetProcessById((int)p).ProcessName; } catch { }
            list.Add(new string[] { ((long)h).ToString(), c.ToString(), t.ToString(), r.Left + "," + r.Top + "," + r.Right + "," + r.Bottom, proc });
            return true;
        }, IntPtr.Zero);
        return list;
    }
}
}
'@
[void][Xl.Native]::SetProcessDPIAware()
$uk = [Xl.Native]::LoadKeyboardLayout('00000809', 1)
[void][Xl.Native]::ActivateKeyboardLayout($uk, 0)
if (-not (Test-Path $Dir)) { [void](New-Item -ItemType Directory $Dir) }
$clock = [Diagnostics.Stopwatch]::StartNew()
$script:Xl = $null; $script:XlPid = 0; $script:Hwnd = [IntPtr]::Zero; $script:Book = $null
$script:Area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$named = @{ DOWN = @(0x28, 1); UP = @(0x26, 1); LEFT = @(0x25, 1); RIGHT = @(0x27, 1); HOME = @(0x24, 1); END = @(0x23, 1)
    DEL = @(0x2E, 1); DELETE = @(0x2E, 1); INS = @(0x2D, 1); ESC = @(0x1B, 0); ENTER = @(0x0D, 0); TAB = @(0x09, 0); BS = @(0x08, 0)
    SPACE = @(0x20, 0); PGDN = @(0x22, 1); PGUP = @(0x21, 1); ALT = @(0x12, 0); APPS = @(0x5D, 1)
    F1 = @(0x70, 0); F2 = @(0x71, 0); F3 = @(0x72, 0); F4 = @(0x73, 0); F5 = @(0x74, 0); F6 = @(0x75, 0); F7 = @(0x76, 0)
    F8 = @(0x77, 0); F9 = @(0x78, 0); F10 = @(0x79, 0); F11 = @(0x7A, 0); F12 = @(0x7B, 0) }

function Answer([string]$Text) { [Console]::Out.WriteLine(('{0} t={1}' -f ($Text -replace "`r?`n", ' '), $clock.ElapsedMilliseconds)); [Console]::Out.Flush() }
function New-Events { return New-Object 'Collections.Generic.List[Xl.Keys+INPUT]' }

# One key as a user presses it: the modifiers down in order, the key, the modifiers up in reverse.
function Key-Group([int[]]$Mods, [int]$Vk, [bool]$Ext, [bool]$CharShift) {
    $e = New-Events
    $all = @($Mods); if ($CharShift -and ($all -notcontains 0x10)) { $all += 0x10 }
    foreach ($m in $all) { $e.Add([Xl.Keys+INPUT]::Key([uint16]$m, $false, $false)) }
    $e.Add([Xl.Keys+INPUT]::Key([uint16]$Vk, $false, $Ext)); $e.Add([Xl.Keys+INPUT]::Key([uint16]$Vk, $true, $Ext))
    for ($i = $all.Count - 1; $i -ge 0; $i--) { $e.Add([Xl.Keys+INPUT]::Key([uint16]$all[$i], $true, $false)) }
    return , $e
}
function Parse-Keys([string]$Spec) {
    $groups = New-Object Collections.Generic.List[object]
    $i = 0
    while ($i -lt $Spec.Length) {
        $mods = @()
        while ($i -lt $Spec.Length - 1 -and '+^%'.Contains([string]$Spec[$i])) {
            $mods += $(switch ($Spec[$i]) { '+' { 0x10 } '^' { 0x11 } '%' { 0x12 } }); $i++
        }
        $c = $Spec[$i]
        if ($c -eq '{' -and $Spec.IndexOf('}', $i + 2) -gt $i) {
            $end = $Spec.IndexOf('}', $i + 2); $inner = $Spec.Substring($i + 1, $end - $i - 1); $i = $end + 1
            if ($inner.Length -eq 1) {
                $k = [Xl.Keys]::VkKeyScan($inner[0]); if ($k -eq -1) { throw "no key for $inner" }
                $groups.Add((Key-Group $mods ($k -band 0xff) $false (($k -band 0x100) -ne 0))); continue
            }
            $p = $inner -split ' '; $n = if ($p.Count -gt 1) { [int]$p[1] } else { 1 }
            $k = $named[$p[0].ToUpperInvariant()]; if ($null -eq $k) { throw "no key named $($p[0])" }
            for ($j = 0; $j -lt $n; $j++) { $groups.Add((Key-Group $mods $k[0] ([bool]$k[1]) $false)) }
            continue
        }
        $k = [Xl.Keys]::VkKeyScan($c); if ($k -eq -1) { throw ("no key for character {0:x4}" -f [int]$c) }
        $groups.Add((Key-Group $mods ($k -band 0xff) $false (($k -band 0x100) -ne 0))); $i++
    }
    return , $groups
}
function Pid-Of([IntPtr]$H) { [uint32]$q = 0; [void][Xl.Native]::GetWindowThreadProcessId($H, [ref]$q); return [int]$q }
function Front-Pid { [uint32]$p = 0; [void][Xl.Native]::GetWindowThreadProcessId([Xl.Native]::GetForegroundWindow(), [ref]$p); return [int]$p }
function Show-Excel {
    if ($script:XlPid -eq 0) { throw 'no Excel started' }
    if ((Front-Pid) -eq $script:XlPid) { return }
    $e = New-Events; $e.Add([Xl.Keys+INPUT]::Key(0x12, $false, $false)); $e.Add([Xl.Keys+INPUT]::Key(0x12, $true, $false)); [Xl.Keys]::Send($e)
    if ([Xl.Native]::IsIconic($script:Hwnd)) { [void][Xl.Native]::ShowWindow($script:Hwnd, 9) }
    [void][Xl.Native]::SetForegroundWindow($script:Hwnd); Start-Sleep -Milliseconds 400
    if ((Front-Pid) -ne $script:XlPid) { throw 'Excel is not in front; nothing was sent' }
}
function Hand([int]$X, [int]$Y) {
    $from = [System.Windows.Forms.Cursor]::Position
    for ($i = 1; $i -le 15; $i++) { [void][Xl.Native]::SetCursorPos([int]($from.X + ($X - $from.X) * $i / 15), [int]($from.Y + ($Y - $from.Y) * $i / 15)); Start-Sleep -Milliseconds 15 }
    Start-Sleep -Milliseconds 80
}
function Press([uint32]$Down, [uint32]$Up) { [Xl.Native]::mouse_event($Down, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 90; [Xl.Native]::mouse_event($Up, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 40 }
function Layout-Of([IntPtr]$H) { [uint32]$p = 0; $t = [Xl.Native]::GetWindowThreadProcessId($H, [ref]$p); return ('0x{0:x8}' -f [long][Xl.Native]::GetKeyboardLayout($t)) }
function Grab([string]$Name) {
    $wins = [Xl.Win]::Of($script:XlPid)
    $bmp = New-Object Drawing.Bitmap $script:Area.Width, $script:Area.Height
    $g = [Drawing.Graphics]::FromImage($bmp); $g.Clear([Drawing.Color]::FromArgb(255, 64, 64, 64))
    $drawn = @()
    for ($i = $wins.Count - 1; $i -ge 0; $i--) {
        $r = $wins[$i][3].Split(',') | ForEach-Object { [int]$_ }; $w = $r[2] - $r[0]; $h = $r[3] - $r[1]
        if ($wins[$i][1] -eq 'MSO_BORDEREFFECT_WINDOW_CLASS') { continue }
        $one = New-Object Drawing.Bitmap $w, $h
        $og = [Drawing.Graphics]::FromImage($one); $hdc = $og.GetHdc()
        $ok = [Xl.Native]::PrintWindow([IntPtr][long]$wins[$i][0], $hdc, 2)
        $og.ReleaseHdc($hdc); $og.Dispose()
        if ($ok) { $g.DrawImage($one, $r[0] - $script:Area.Left, $r[1] - $script:Area.Top, $w, $h) }
        $one.Dispose(); $drawn += ('{0}@{1}' -f $wins[$i][1], $wins[$i][3])
    }
    $g.Dispose()
    $f = Join-Path $Dir ($Name + '.png'); $bmp.Save($f, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    return ('{0} windows: {1}' -f $f, ($drawn -join ' ; '))
}
function Value-Of($v) {
    if ($null -eq $v) { return 'empty' }
    if ($v -is [int]) { $t = @{ -2146826281 = '#DIV/0!'; -2146826246 = '#N/A'; -2146826259 = '#NAME?'; -2146826288 = '#NULL!'; -2146826252 = '#NUM!'; -2146826265 = '#REF!'; -2146826273 = '#VALUE!' }[$v]; if ($t) { return "error $t" }; return "int $v" }
    if ($v -is [double]) { return 'number ' + $v.ToString('R', [Globalization.CultureInfo]::InvariantCulture) }
    return ('{0} {1}' -f $v.GetType().Name, (ConvertTo-Json -InputObject ([string]$v) -Compress))
}
function Uia-Rect([Windows.Automation.AutomationElement]$Root, [string]$Name, [string]$Class) {
    $c = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, $Name)
    $all = $Root.FindAll([Windows.Automation.TreeScope]::Descendants, $c)
    foreach ($e in $all) { if (-not $Class -or $e.Current.ClassName -eq $Class) { $r = $e.Current.BoundingRectangle; return ('{0}:{1},{2},{3},{4}' -f $Name, [int]$r.Left, [int]$r.Top, [int]$r.Right, [int]$r.Bottom) } }
    return "${Name}:not found"
}

[Console]::Out.WriteLine('ready'); [Console]::Out.Flush()
while ($true) {
    $line = [Console]::In.ReadLine()
    if ($null -eq $line -or $line -eq 'quit') { break }
    $parts = $line.Split(' ', 2); $cmd = $parts[0]; $rest = if ($parts.Count -gt 1) { $parts[1] } else { '' }
    try {
        switch ($cmd) {
            'start' {
                if ($script:Xl) { throw 'an Excel of this script''s is already running' }
                $before = @(Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
                $x = New-Object -ComObject Excel.Application
                # (A type-constrained [uint32] variable here, in the script's scope, would refuse every later
                # assignment of another type to the same name: the process id goes through a helper.)
                $xp = Pid-Of ([IntPtr][long]$x.Hwnd)
                if ($before -contains $xp) { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($x); throw "New-Object gave an Excel already running ($xp); nothing changed in it" }
                $script:Xl = $x; $script:XlPid = $xp
                $x.Visible = $true; $x.DisplayAlerts = $false
                $opened = @($x.Workbooks | ForEach-Object { [string]$_.Name })
                $wb = $x.Workbooks.Add(); $wb.EnableAutoRecover = $false
                while ($wb.Worksheets.Count -gt 1) { $wb.Worksheets.Item($wb.Worksheets.Count).Delete() }
                $script:Book = $wb
                $win = $wb.Windows.Item(1); $win.WindowState = -4137; $win.Zoom = 100
                [void]$wb.Worksheets.Item(1).Range('A1').Select()
                $script:Hwnd = [IntPtr][long]$win.Hwnd
                Show-Excel
                [void][Xl.Native]::PostMessage($script:Hwnd, 0x0050, [IntPtr]::Zero, [IntPtr]0x08090809); Start-Sleep -Milliseconds 600
                Answer ('ok start pid={0} book={1} sheet={2} openedByItself={3} before={4} layout={5} version={6}' -f $xp, $wb.Name, $wb.Worksheets.Item(1).Name, ($opened -join '|'), ($before -join '|'), (Layout-Of $script:Hwnd), $x.Version)
            }
            'stop' {
                $id = $script:XlPid; $how = 'quit'
                try { $script:Book.Close($false) } catch { }
                try { $script:Xl.Quit() } catch { }
                try { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($script:Book) } catch { }
                try { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($script:Xl) } catch { }
                $script:Xl = $null; $script:Book = $null; [GC]::Collect(); [GC]::WaitForPendingFinalizers()
                for ($i = 0; $i -lt 20; $i++) { if (-not (Get-Process -Id $id -ErrorAction SilentlyContinue)) { break }; Start-Sleep -Milliseconds 500 }
                if (Get-Process -Id $id -ErrorAction SilentlyContinue) { Stop-Process -Id $id -Force; $how = 'ended by Stop-Process (its own)' }
                $script:XlPid = 0
                Answer "ok stop $id $how"
            }
            'front' { Show-Excel; Answer 'ok front' }
            'keys' {
                $p = $rest.Split(' ', 2); $gap = [int]$p[0]; $groups = Parse-Keys $p[1]
                Show-Excel
                foreach ($g in $groups) { [Xl.Keys]::Send($g); Start-Sleep -Milliseconds ([Math]::Max($gap, 1)) }
                Answer ('ok keys ' + $groups.Count)
            }
            'click' { $a = $rest.Split(' ') | % { [int]$_ }; Show-Excel; Hand $a[0] $a[1]; Press 0x2 0x4; Answer 'ok click' }
            'rclick' { $a = $rest.Split(' ') | % { [int]$_ }; Show-Excel; Hand $a[0] $a[1]; Press 0x8 0x10; Answer 'ok rclick' }
            'dblclick' { $a = $rest.Split(' ') | % { [int]$_ }; Show-Excel; Hand $a[0] $a[1]; Press 0x2 0x4; Start-Sleep -Milliseconds 40; Press 0x2 0x4; Answer 'ok dblclick' }
            'shiftclick' {
                $a = $rest.Split(' ') | % { [int]$_ }; Show-Excel; Hand $a[0] $a[1]
                $e = New-Events; $e.Add([Xl.Keys+INPUT]::Key(0x10, $false, $false)); [Xl.Keys]::Send($e); Start-Sleep -Milliseconds 40
                Press 0x2 0x4
                $e = New-Events; $e.Add([Xl.Keys+INPUT]::Key(0x10, $true, $false)); [Xl.Keys]::Send($e)
                Answer 'ok shiftclick'
            }
            'ctrlclick' {
                $a = $rest.Split(' ') | % { [int]$_ }; Show-Excel; Hand $a[0] $a[1]
                $e = New-Events; $e.Add([Xl.Keys+INPUT]::Key(0x11, $false, $false)); [Xl.Keys]::Send($e); Start-Sleep -Milliseconds 40
                Press 0x2 0x4
                $e = New-Events; $e.Add([Xl.Keys+INPUT]::Key(0x11, $true, $false)); [Xl.Keys]::Send($e)
                Answer 'ok ctrlclick'
            }
            'move' { $a = $rest.Split(' ') | % { [int]$_ }; Hand $a[0] $a[1]; Answer 'ok move' }
            # Added for Part H: a press dragged from one point to another, alone or with Ctrl held (h1, h2, h4).
            'drag' { $a = $rest.Split(' ') | % { [int]$_ }; Show-Excel; Hand $a[0] $a[1]; [Xl.Native]::mouse_event(0x2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 80; Hand $a[2] $a[3]; Start-Sleep -Milliseconds 120; [Xl.Native]::mouse_event(0x4, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60; Answer 'ok drag' }
            'ctrldrag' {
                $a = $rest.Split(' ') | % { [int]$_ }; Show-Excel; Hand $a[0] $a[1]
                $e = New-Events; $e.Add([Xl.Keys+INPUT]::Key(0x11, $false, $false)); [Xl.Keys]::Send($e); Start-Sleep -Milliseconds 40
                [Xl.Native]::mouse_event(0x2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 80; Hand $a[2] $a[3]; Start-Sleep -Milliseconds 120; [Xl.Native]::mouse_event(0x4, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60
                $e = New-Events; $e.Add([Xl.Keys+INPUT]::Key(0x11, $true, $false)); [Xl.Keys]::Send($e)
                Answer 'ok ctrldrag'
            }
            # Setup only (h7, h8): a column's width in characters, set through COM, which Excel then keeps as a custom width.
            'setwidth' { $p = $rest.Split(' '); $c = $script:Xl.ActiveSheet.Range($p[0]); $c.ColumnWidth = [double]::Parse($p[1], [Globalization.CultureInfo]::InvariantCulture); Answer ("ok setwidth $($p[0]) " + $c.ColumnWidth) }
            # Setup only (h10): one edge of a cell, through COM: LineStyle and Weight (xlContinuous 1; xlThin 2, xlMedium -4138, xlThick 4).
            'setedge' { $p = $rest.Split(' '); $b = $script:Xl.ActiveSheet.Range($p[0]).Borders.Item([int]$p[1]); $b.LineStyle = [int]$p[2]; $b.Weight = [int]$p[3]; Answer "ok setedge $rest" }
            # Setup only (h11): a solid fill of RRGGBB (Interior.Color takes BGR).
            'fill' { $p = $rest.Split(' '); $rgb = [Convert]::ToInt32($p[1], 16); $bgr = (($rgb -band 0xFF) -shl 16) -bor ($rgb -band 0xFF00) -bor (($rgb -shr 16) -band 0xFF); $script:Xl.ActiveSheet.Range($p[0]).Interior.Color = $bgr; Answer "ok fill $rest" }
            # The window's zoom, in per cent (h11).
            'zoom' { $script:Book.Windows.Item(1).Zoom = [int]$rest; Answer ("ok zoom " + $script:Book.Windows.Item(1).Zoom) }
            # Each cell's font and borders as Excel holds them (h7, h8, h10): bold, italic, underline, strikethrough,
            # and each edge's LineStyle and Weight (xlEdgeTop 8, xlEdgeBottom 9, xlEdgeLeft 7, xlEdgeRight 10).
            'look' {
                $ws = $script:Xl.ActiveSheet; $o = @()
                foreach ($a in $rest.Split(' ')) {
                    $r = $ws.Range($a); $fo = $r.Font
                    $edges = @(8, 9, 7, 10) | ForEach-Object { $b = $r.Borders.Item($_); '{0}={1}/{2}' -f @{8='top';9='bottom';7='left';10='right'}[$_], $b.LineStyle, $b.Weight }
                    $o += ('{0}: bold {1} italic {2} underline {3} strike {4} size {5} | {6} | width {7} | text {8}' -f $a, $fo.Bold, $fo.Italic, $fo.Underline, $fo.Strikethrough, $fo.Size, ($edges -join ' '), $r.ColumnWidth, (ConvertTo-Json -InputObject ([string]$r.Text) -Compress))
                }
                Answer ('ok look ' + ($o -join ' ;; '))
            }
            # Every named element UI Automation sees under Excel's own windows whose name matches a pattern (h5's list,
            # h10's dialog), with its control type, in tree order; at most 80.
            'uianames' {
                $re = $rest; $o = New-Object Collections.Generic.List[string]
                foreach ($w in ([Xl.Win]::Of($script:XlPid))) {
                    $root = [Windows.Automation.AutomationElement]::FromHandle([IntPtr][long]$w[0])
                    foreach ($e in $root.FindAll([Windows.Automation.TreeScope]::Subtree, [Windows.Automation.Condition]::TrueCondition)) {
                        $n = $e.Current.Name; if ($n -and $n -match $re -and $o.Count -lt 80) { $o.Add(('{0} | {1} | {2}' -f $n, $e.Current.ControlType.ProgrammaticName, $e.Current.ClassName)) }
                    }
                }
                Answer ('ok uianames ' + (ConvertTo-Json -InputObject @($o) -Compress))
            }
            'shot' { Answer ('ok shot ' + (Grab $rest)) }
            'screen' {
                $p = $rest.Split(' '); $x = [int]$p[1]; $y = [int]$p[2]; $w = [int]$p[3]; $h = [int]$p[4]
                $bmp = New-Object Drawing.Bitmap $w, $h; $g = [Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($x, $y, 0, 0, (New-Object Drawing.Size $w, $h)); $g.Dispose()
                $f = Join-Path $Dir ($p[0] + '.png'); $bmp.Save($f, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose(); Answer "ok screen $f"
            }
            'crop' {
                $p = $rest.Split(' '); $src = [Drawing.Bitmap]::FromFile((Join-Path $Dir ($p[0] + '.png')))
                $x = [int]$p[1]; $y = [int]$p[2]; $w = [Math]::Min([int]$p[3], $src.Width - $x); $h = [Math]::Min([int]$p[4], $src.Height - $y); $s = [int]$p[6]
                $big = New-Object Drawing.Bitmap ($w * $s), ($h * $s); $gr = [Drawing.Graphics]::FromImage($big)
                $gr.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor; $gr.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
                $gr.DrawImage($src, (New-Object Drawing.Rectangle 0, 0, ($w * $s), ($h * $s)), (New-Object Drawing.Rectangle $x, $y, $w, $h), [Drawing.GraphicsUnit]::Pixel)
                $gr.Dispose(); $src.Dispose(); $f = Join-Path $Dir ($p[5] + '.png'); $big.Save($f, [Drawing.Imaging.ImageFormat]::Png); $big.Dispose(); Answer "ok crop $f"
            }
            'ready' {
                $r = 'refused'
                try { [void]$script:Xl.Workbooks.Count; [void]$script:Xl.ActiveCell.Address($false, $false); $r = 'ready=' + $script:Xl.Ready + ' active=' + $script:Xl.ActiveSheet.Name + '!' + $script:Xl.ActiveCell.Address($false, $false) + ' selection=' + $script:Xl.Selection.Address($false, $false) } catch { $r = 'refused: ' + $_.Exception.Message }
                Answer "ok ready $r"
            }
            'cell' {
                $ws = $script:Xl.ActiveSheet; $o = @()
                foreach ($a in $rest.Split(' ')) {
                    $sheet = $ws; $addr = $a
                    if ($a.Contains('!')) { $q = $a.Split('!'); $sheet = $script:Book.Worksheets.Item($q[0]); $addr = $q[1] }
                    $r = $sheet.Range($addr)
                    $nf = [string]$r.GetType().InvokeMember('NumberFormat', [Reflection.BindingFlags]::GetProperty, $null, $r, $null, [Globalization.CultureInfo]::GetCultureInfo('en-US'))
                    $o += ('{0}: {1} | formula {2} | text {3} | numberFormat {4}' -f $a, (Value-Of $r.Value2), (ConvertTo-Json -InputObject ([string]$r.Formula2) -Compress), (ConvertTo-Json -InputObject ([string]$r.Text) -Compress), (ConvertTo-Json -InputObject $nf -Compress))
                }
                Answer ('ok cell ' + ($o -join ' ;; '))
            }
            'geo' {
                $win = $script:Xl.ActiveWindow; $pane = $win.ActivePane; $ws = $script:Xl.ActiveSheet; $o = @()
                foreach ($a in $rest.Split(' ')) {
                    $r = $ws.Range($a)
                    $o += ('{0}:{1},{2},{3},{4}' -f $a, $pane.PointsToScreenPixelsX($r.Left), $pane.PointsToScreenPixelsY($r.Top), $pane.PointsToScreenPixelsX($r.Left + $r.Width), $pane.PointsToScreenPixelsY($r.Top + $r.Height))
                }
                Answer ('ok geo ' + ($o -join ' '))
            }
            'uia' {
                $root = [Windows.Automation.AutomationElement]::FromHandle($script:Hwnd); $o = @()
                $o += Uia-Rect $root 'Formula Bar' 'XLFormulaBarEditor'
                $o += Uia-Rect $root 'Name Box' ''
                $o += Uia-Rect $root 'Status Bar' ''
                $o += Uia-Rect $root 'Sheet Tab' ''
                foreach ($n in @($script:Book.Worksheets | ForEach-Object { [string]$_.Name })) { $o += Uia-Rect $root $n '' }
                Answer ('ok uia ' + ($o -join ' '))
            }
            'windows' { Answer ('ok windows ' + ((([Xl.Win]::Of($script:XlPid)) | ForEach-Object { $_[0..3] -join '|' }) -join ' ;; ')) }
            'allwindows' { Answer ('ok allwindows ' + ((([Xl.Win]::Of(0)) | Select-Object -First 25 | ForEach-Object { '{0}|{1}|{2}|{3}' -f $_[0], $_[1], $_[4], $_[3] }) -join ' ;; ')) }
            'clipclear' { for ($i = 0; $i -lt 20; $i++) { try { [System.Windows.Forms.Clipboard]::Clear(); break } catch { Start-Sleep -Milliseconds 100 } }; Answer 'ok clipclear' }
            'clipboard' {
                $text = $null; $err = ''
                for ($i = 0; $i -lt 20; $i++) { try { $text = [System.Windows.Forms.Clipboard]::GetText(); break } catch { $err = $_.Exception.Message; Start-Sleep -Milliseconds 100 } }
                if ($null -eq $text) { Answer ('ok clipboard error ' + $err) } else { Answer ('ok clipboard ' + (ConvertTo-Json -InputObject ([string]$text) -Compress)) }
            }
            'imeoff' { $e = New-Events; $e.Add([Xl.Keys+INPUT]::Key(0x1A, $false, $false)); $e.Add([Xl.Keys+INPUT]::Key(0x1A, $true, $false)); [Xl.Keys]::Send($e); Answer 'ok imeoff' }
            'layout' { Answer ('ok layout ' + (Layout-Of $script:Hwnd)) }
            'setup' {
                $p = $rest.Split(' ', 2); $r = $script:Xl.ActiveSheet.Range($p[0])
                if ($p[1].StartsWith('=')) { $r.Formula2 = $p[1] } else { $r.Value2 = $p[1] }
                Answer "ok setup $($p[0])"
            }
            'addsheet' { $s = $script:Book.Worksheets.Add([Type]::Missing, $script:Book.Worksheets.Item($script:Book.Worksheets.Count)); $s.Name = $rest; [void]$script:Book.Worksheets.Item(1).Activate(); Answer "ok addsheet $rest" }
            'select' { [void]$script:Xl.ActiveSheet.Range($rest).Select(); Answer "ok select $rest" }
            'clearsheet' {
                # Between cases, setup only: every Table of the active sheet removed, every cell cleared, A1 selected.
                $ws = $script:Xl.ActiveSheet
                foreach ($lo in @($ws.ListObjects)) { $lo.Delete() }
                [void]$ws.Cells.Clear(); [void]$ws.Range('A1').Select()
                Answer "ok clearsheet $($ws.Name)"
            }
            'table' {
                # Setup only: a range made a Table of that name, its first row the headers.
                $p = $rest.Split(' ', 2); $ws = $script:Xl.ActiveSheet
                $lo = $ws.ListObjects.Add(1, $ws.Range($p[0]), [Type]::Missing, 1); $lo.Name = $p[1]
                Answer "ok table $($lo.Name) $($lo.Range.Address($false, $false))"
            }
            'setfmt' {
                # Setup only: a Number Format through COM, in en-US (PowerShell 5.1's binder would pass the
                # user's locale), on a cell that holds nothing yet (a format set on a value widens a column).
                $p = $rest.Split(' ', 2); $r = $script:Xl.ActiveSheet.Range($p[0])
                [void]$r.GetType().InvokeMember('NumberFormat', [Reflection.BindingFlags]::SetProperty, $null, $r, @($p[1]), [Globalization.CultureInfo]::GetCultureInfo('en-US'))
                Answer "ok setfmt $($p[0])"
            }
            'colwidth' {
                $ws = $script:Xl.ActiveSheet; $o = @()
                foreach ($a in $rest.Split(' ')) { $c = $ws.Range($a); $o += ('{0}: width {1} | useStandardWidth {2}' -f $a, $c.ColumnWidth.ToString('R', [Globalization.CultureInfo]::InvariantCulture), $c.UseStandardWidth) }
                Answer ('ok colwidth standardWidth ' + $ws.StandardWidth.ToString('R', [Globalization.CultureInfo]::InvariantCulture) + ' ;; ' + ($o -join ' ;; '))
            }
            'savecols' {
                # A copy of the workbook saved as .xlsx to the temporary folder; the <cols> of its active
                # sheet read from the package (the file is never kept: its properties name the account).
                $f = Join-Path $env:TEMP ('xs-cols-{0}.xlsx' -f $clock.ElapsedMilliseconds)
                $script:Book.SaveCopyAs($f)
                Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
                $fs = [IO.File]::Open($f, 'Open', 'Read', 'ReadWrite'); $zip = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Read)
                $idx = $script:Xl.ActiveSheet.Index
                $e = $zip.GetEntry("xl/worksheets/sheet$idx.xml"); $sr = New-Object IO.StreamReader($e.Open()); $xml = $sr.ReadToEnd(); $sr.Dispose()
                $zip.Dispose(); $fs.Dispose(); Remove-Item $f -Force
                $m = [regex]::Match($xml, '<cols>.*?</cols>'); $fmt = [regex]::Match($xml, '<sheetFormatPr[^>]*>')
                Answer ('ok savecols ' + $(if ($m.Success) { $m.Value } else { 'no <cols>' }) + ' ' + $fmt.Value)
            }
            default { Answer "err unknown command $cmd" }
        }
    } catch { Answer ('err ' + $_.Exception.Message) }
}
if ($script:Xl) { try { $script:Book.Close($false) } catch { }; try { $script:Xl.Quit() } catch { } }
