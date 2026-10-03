<#
.SYNOPSIS
    Real OS input for beside-excel-probe.mjs (verify-on-windows-11.md, Part C: ExSheet beside Excel),
    kept running so that a key goes out within milliseconds of the page being ready for it. The
    fifteenth run's helper (verification/2026-10-01-windows-15/input-server.ps1, on
    claude/exsheet-windows-verify-15), every key sent through SendInput as a virtual-key and a scan
    code, with these added for this run: Ctrl with the key that types a character on the English (UK)
    layout (ctrlchar, as Part A's Ctrl-Char sent Ctrl+Shift+& and the like), a drag with Ctrl held
    (ctrldrag, Part A's case 14), and the named keys Ctrl+B, Ctrl+1 and the like need (KEYB, KEY1, ...).

    Earlier: the thirteenth and eighth runs' helpers, with a Shift+click, a drag, a key held over a
    click, Ctrl and Shift together, a burst that holds a click among keys, the cursor's shape, and the
    IME's keys and state (the fifteenth run).

        powershell -File input-server.ps1        (commands on stdin, one per line; one answer each)

    front <title prefix>                 the window whose title starts with it, to the front
    layout <title prefix>                that window's keyboard layout (its thread's HKL)
    english <title prefix>               that window to the English (UK) keyboard
                                         (WM_INPUTLANGCHANGEREQUEST), then its layout
    move <x> <y>                         the cursor, to a screen pixel (moved 1 px and back)
    click <x> <y>                        a left click there (pressed for 60 ms)
    dblclick <x> <y>                     two left clicks there, 80 ms apart
    shiftclick <x> <y>                   a left click there with Shift held
    drag <x1> <y1> <x2> <y2> <steps>     pressed at the first point, moved in steps of 15 ms to the
                                         second, released there
    ctrldrag <x1> <y1> <x2> <y2> <steps> the same with Ctrl held throughout
    rclick <x> <y>                       a right click there (the Context Menu)
    ctrlchar <c>                         Ctrl with the key that types the character c on the English (UK)
                                         layout, and the Shift that character needs: ctrlchar & is
                                         Ctrl+Shift+7, ctrlchar # is Ctrl+OEM_7, ctrlchar = is Ctrl+OEM_PLUS
    type <gap ms> <keys>                 keys in SendKeys' notation, one key <gap> ms after the last:
                                         {NAME} or {NAME n} a named key, +{NAME} with Shift held,
                                         ^{NAME} with Ctrl, ^+{NAME} with both, {c} a character
                                         SendKeys reserves, any other as itself
    burst <keys>                         the same keys, every key event in one SendInput call: as
                                         fast as SendKeys.SendWait sends a string (by-hand.ps1)
    burstclick <x> <y> <keys>|<keys>     the pointer put at (x, y), then in one SendInput call the
                                         first keys, a left press and release, and the second keys
    wheel <x> <y> <delta> <v|h> <n>      the pointer put at (x, y), then n turns of the wheel (v) or
                                         of the horizontal wheel (h), each of delta, 60 ms apart
    cursor                               the cursor's handle, and which of the system's it is
    imeoff                               VK_IME_OFF
    imeon                                VK_IME_ON
    japanese <title prefix>              that window to the Japanese keyboard (0x04110411,
                                         WM_INPUTLANGCHANGEREQUEST), then its layout
    imestate <title prefix>              the IME of the window's focused child: its open status and
                                         conversion mode (WM_IME_CONTROL to the default IME window),
                                         and the window's layout
    windows                              every visible top-level window: handle|class|process|place
    imeui                                every visible top-level window of an IME (class or process),
                                         with its place, and the names UI Automation reads under it
    screen <x> <y> <w> <h> <file>        a picture of the screen there (CopyFromScreen), saved as PNG;
                                         answers how many pixels are not black
    hc                                   whether high contrast is on, and its scheme
    hcset <on|off> [scheme]              high contrast on (with that scheme) or off, the flags'
                                         other bits kept
    quit

    The process is DPI-aware, so coordinates are the screen's physical pixels. Its own thread types
    through the English (UK) keyboard, so a character becomes the key a UK keyboard has for it. The
    Japanese keyboard here has the US keys (kbd101.dll); every character the cases type with the IME
    off (= + ( ) : , and letters and digits) is the same key on both.
    Each answer is one line, "ok ..." or "err ...", with the time (ms since start) it finished.
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -Namespace InputServer -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, int data, UIntPtr extra);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
[DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint thread);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr LoadKeyboardLayout(string id, uint flags);
[DllImport("user32.dll")] public static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint flags);
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
[DllImport("user32.dll")] public static extern bool GetCursorInfo(ref CURSORINFO info);
[DllImport("user32.dll")] public static extern IntPtr LoadCursor(IntPtr instance, IntPtr name);
[StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
[StructLayout(LayoutKind.Sequential)] public struct CURSORINFO { public uint cbSize; public uint flags; public IntPtr hCursor; public POINT pt; }
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool SystemParametersInfo(uint action, uint param, ref HIGHCONTRAST value, uint winIni);
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct HIGHCONTRAST { public uint cbSize; public uint dwFlags; public IntPtr lpszDefaultScheme; }
'@
Add-Type -TypeDefinition @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Threading;
namespace InputServer {
public static class Keys {
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit, Size = 40)] public struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] KEYBDINPUT ki; [FieldOffset(8)] MOUSEINPUT mi;
        public static INPUT Key(ushort vk, bool up, bool extended) {
            var i = new INPUT(); i.type = 1; i.ki.wVk = vk; i.ki.wScan = (ushort)MapVirtualKey(vk, 0); i.ki.dwFlags = (up ? 2u : 0u) | (extended ? 1u : 0u); return i;
        }
        // The left button, where the pointer is (MOUSEEVENTF_LEFTDOWN 0x2, LEFTUP 0x4).
        public static INPUT Button(bool up) { var i = new INPUT(); i.type = 0; i.mi.dwFlags = up ? 4u : 2u; return i; }
    }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] i, int size);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern short VkKeyScan(char c);
    public static void Send(List<INPUT> events) {
        var a = events.ToArray();
        if (SendInput((uint)a.Length, a, Marshal.SizeOf(typeof(INPUT))) != a.Length) throw new Exception("SendInput failed: " + Marshal.GetLastWin32Error());
    }
    public static void Tap(List<INPUT> e, ushort vk, bool extended) { e.Add(INPUT.Key(vk, false, extended)); e.Add(INPUT.Key(vk, true, extended)); }
    public static void Chord(List<INPUT> e, ushort mod, ushort vk, bool extended) { e.Add(INPUT.Key(mod, false, false)); Tap(e, vk, extended); e.Add(INPUT.Key(mod, true, false)); }
    // A character, through this thread's keyboard layout, with Shift, Ctrl or Alt as it needs.
    public static void Char(List<INPUT> e, char c) {
        short k = VkKeyScan(c);
        if (k == -1) throw new Exception("no key for character " + ((int)c).ToString("x4"));
        ushort vk = (ushort)(k & 0xff); bool shift = (k & 0x100) != 0, ctrl = (k & 0x200) != 0, alt = (k & 0x400) != 0;
        if (shift) e.Add(INPUT.Key(0x10, false, false));
        if (ctrl) e.Add(INPUT.Key(0x11, false, false));
        if (alt) e.Add(INPUT.Key(0x12, false, false));
        Tap(e, vk, false);
        if (alt) e.Add(INPUT.Key(0x12, true, false));
        if (ctrl) e.Add(INPUT.Key(0x11, true, false));
        if (shift) e.Add(INPUT.Key(0x10, true, false));
    }
}
}
'@
# The IME: its state as the focused window's default IME window answers it, and its own windows.
Add-Type -ReferencedAssemblies UIAutomationClient, UIAutomationTypes, WindowsBase -TypeDefinition @'
using System; using System.Collections.Generic; using System.Diagnostics; using System.Runtime.InteropServices; using System.Text; using System.Threading;
using System.Windows.Automation;
namespace InputServer {
public static class Ime {
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct GUITHREADINFO { public int cbSize; public int flags; public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret; public RECT rcCaret; }
    [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint thread, ref GUITHREADINFO info);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("imm32.dll")] static extern IntPtr ImmGetDefaultIMEWnd(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint ms, out IntPtr result);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);

    // The open status (IMC_GETOPENSTATUS, 5) and the conversion mode (IMC_GETCONVERSIONMODE, 1) of the
    // IME of the focused window of the window's thread, through WM_IME_CONTROL (0x283).
    public static string State(IntPtr top) {
        uint pid; uint tid = GetWindowThreadProcessId(top, out pid);
        var gi = new GUITHREADINFO(); gi.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
        IntPtr focus = GetGUIThreadInfo(tid, ref gi) && gi.hwndFocus != IntPtr.Zero ? gi.hwndFocus : top;
        IntPtr ime = ImmGetDefaultIMEWnd(focus);
        if (ime == IntPtr.Zero) return "no default IME window";
        IntPtr open, mode;
        if (SendMessageTimeout(ime, 0x283, new IntPtr(5), IntPtr.Zero, 2, 500, out open) == IntPtr.Zero) return "the IME window did not answer";
        SendMessageTimeout(ime, 0x283, new IntPtr(1), IntPtr.Zero, 2, 500, out mode);
        long m = (long)mode;
        var names = new List<string>();
        if ((m & 1) != 0) names.Add((m & 2) != 0 ? "katakana" : "hiragana"); else names.Add("alphanumeric");
        names.Add((m & 8) != 0 ? "full-width" : "half-width");
        if ((m & 0x10) != 0) names.Add("romaji");
        return string.Format("open={0} mode=0x{1:x} ({2})", (long)open != 0, m, string.Join(", ", names));
    }

    // Every visible top-level window whose class or process names an IME or the text input host,
    // with its place and, read on a thread with a time limit, the names under it.
    // Every visible top-level window, with its class, process and place, front to back.
    public static List<string> All() {
        var o = new List<string>();
        EnumWindows((h, l) => {
            if (!IsWindowVisible(h)) return true;
            var c = new StringBuilder(256); GetClassName(h, c, 256);
            uint pid; GetWindowThreadProcessId(h, out pid); string proc = "";
            try { proc = Process.GetProcessById((int)pid).ProcessName; } catch { }
            RECT r; GetWindowRect(h, out r);
            o.Add(string.Format("{0:x}|{1}|{2}|{3},{4},{5},{6}", (long)h, c.ToString(), proc, r.Left, r.Top, r.Right, r.Bottom));
            return true;
        }, IntPtr.Zero);
        return o;
    }
    public static List<string> Windows(int ms) {
        var o = new List<string>();
        var hits = new List<IntPtr>();
        EnumWindows((h, l) => {
            if (!IsWindowVisible(h)) return true;
            var c = new StringBuilder(256); GetClassName(h, c, 256); string cls = c.ToString();
            uint pid; GetWindowThreadProcessId(h, out pid); string proc = "";
            try { proc = Process.GetProcessById((int)pid).ProcessName; } catch { }
            bool ime = cls.IndexOf("IME", StringComparison.OrdinalIgnoreCase) >= 0 || cls.IndexOf("Candidate", StringComparison.OrdinalIgnoreCase) >= 0
                || proc.Equals("TextInputHost", StringComparison.OrdinalIgnoreCase) || proc.Equals("ctfmon", StringComparison.OrdinalIgnoreCase);
            if (!ime) return true;
            RECT r; GetWindowRect(h, out r);
            if (r.Right - r.Left <= 1 || r.Bottom - r.Top <= 1) return true;
            o.Add(string.Format("window class={0} process={1} rect={2},{3},{4},{5}", cls, proc, r.Left, r.Top, r.Right, r.Bottom));
            hits.Add(h);
            return true;
        }, IntPtr.Zero);
        foreach (var h in hits) {
            var names = new List<string>();
            var t = new Thread(() => {
                try {
                    var w = TreeWalker.RawViewWalker; var stack = new Stack<AutomationElement>(); int seen = 0;
                    stack.Push(AutomationElement.FromHandle(h));
                    while (stack.Count > 0 && seen < 300) {
                        var e = stack.Pop(); seen++;
                        string n = e.Current.Name; string type = e.Current.ControlType.ProgrammaticName.Replace("ControlType.", "");
                        object p; string sel = "";
                        if (e.TryGetCurrentPattern(SelectionItemPattern.Pattern, out p) && ((SelectionItemPattern)p).Current.IsSelected) sel = " (selected)";
                        if (!string.IsNullOrEmpty(n)) names.Add(type + ":" + n + sel);
                        var kids = new List<AutomationElement>(); var c = w.GetFirstChild(e);
                        while (c != null) { kids.Add(c); c = w.GetNextSibling(c); }
                        for (int i = kids.Count - 1; i >= 0; i--) stack.Push(kids[i]);
                    }
                } catch (Exception x) { names.Add("error: " + x.Message.Split('\n')[0]); }
            });
            t.IsBackground = true; t.Start();
            if (!t.Join(ms)) names.Add("timed out after " + ms + " ms");
            o.Add("  names: " + string.Join(" | ", names.ToArray()));
        }
        return o;
    }
}
}
'@
[void][InputServer.Native]::SetProcessDPIAware()
$uk =[InputServer.Native]::LoadKeyboardLayout('00000809', 1)
[void][InputServer.Native]::ActivateKeyboardLayout($uk, 0)
$clock = [Diagnostics.Stopwatch]::StartNew()
$named = @{ DOWN = @(0x28, 1); UP = @(0x26, 1); LEFT = @(0x25, 1); RIGHT = @(0x27, 1); HOME = @(0x24, 1); END = @(0x23, 1)
    DEL = @(0x2E, 1); DELETE = @(0x2E, 1); F2 = @(0x71, 0); F3 = @(0x72, 0); F4 = @(0x73, 0); ESC = @(0x1B, 0); ENTER = @(0x0D, 0); TAB = @(0x09, 0); BS = @(0x08, 0)
    SPACE = @(0x20, 0); KEYF = @(0x46, 0); KEYA = @(0x41, 0); KEYB = @(0x42, 0); KEYI = @(0x49, 0); KEYU = @(0x55, 0); KEYZ = @(0x5A, 0)
    KEY0 = @(0x30, 0); KEY1 = @(0x31, 0); KEY2 = @(0x32, 0); KEY3 = @(0x33, 0); KEY4 = @(0x34, 0); KEY5 = @(0x35, 0) }   # ^{KEYB} for Ctrl+B: a modifier needs a name of two letters or more

# SendKeys' notation to key groups: each group is one key as a user presses it (with its Shift).
function Parse-Keys([string]$Spec) {
    $groups = New-Object Collections.Generic.List[object]
    $i = 0
    while ($i -lt $Spec.Length) {
        $c = $Spec[$i]; $mods = @()
        $j0 = $i
        while ($j0 -lt $Spec.Length -and ($Spec[$j0] -eq '+' -or $Spec[$j0] -eq '^')) { $j0++ }
        if ($j0 -gt $i -and $j0 + 1 -lt $Spec.Length -and $Spec[$j0] -eq '{' -and $Spec.IndexOf('}', $j0 + 2) -gt $j0 + 2) {
            for ($m = $i; $m -lt $j0; $m++) { $mods += $(if ($Spec[$m] -eq '+') { 0x10 } else { 0x11 }) }
            $i = $j0; $c = $Spec[$i]
        }
        $e = New-Object 'Collections.Generic.List[InputServer.Keys+INPUT]'
        if ($c -eq '{') {
            $end = $Spec.IndexOf('}', $i + 2); $inner = $Spec.Substring($i + 1, $end - $i - 1); $i = $end + 1
            if ($inner.Length -eq 1 -and $mods.Count -eq 0) { [InputServer.Keys]::Char($e, $inner[0]); $groups.Add($e); continue }
            $p = $inner -split ' '; $n = if ($p.Count -gt 1) { [int]$p[1] } else { 1 }
            $k = $named[$p[0].ToUpperInvariant()]; if ($null -eq $k) { throw "no key named $($p[0])" }
            for ($j = 0; $j -lt $n; $j++) {
                $e = New-Object 'Collections.Generic.List[InputServer.Keys+INPUT]'
                foreach ($m in $mods) { $e.Add([InputServer.Keys+INPUT]::Key([uint16]$m, $false, $false)) }
                [InputServer.Keys]::Tap($e, [uint16]$k[0], [bool]$k[1])
                for ($m = $mods.Count - 1; $m -ge 0; $m--) { $e.Add([InputServer.Keys+INPUT]::Key([uint16]$mods[$m], $true, $false)) }
                $groups.Add($e)
            }
            continue
        }
        [InputServer.Keys]::Char($e, $c); $groups.Add($e); $i++
    }
    return , $groups
}
function Find-Window([string]$Prefix) {
    $until = (Get-Date).AddSeconds(3)
    do {
        $p = Get-Process | Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle.StartsWith($Prefix) } | Select-Object -First 1
        if ($null -ne $p) { return $p.MainWindowHandle }
        Start-Sleep -Milliseconds 150
    } while ((Get-Date) -lt $until)
    throw "no window titled '$Prefix...'"
}
function Layout-Of([IntPtr]$H) { [uint32]$p = 0; $t = [InputServer.Native]::GetWindowThreadProcessId($H, [ref]$p); return ('0x{0:x8}' -f [long][InputServer.Native]::GetKeyboardLayout($t)) }
function Alt-Tap { $e = New-Object 'Collections.Generic.List[InputServer.Keys+INPUT]'; [InputServer.Keys]::Tap($e, 0x12, $false); [InputServer.Keys]::Send($e) }
function Press([int]$X, [int]$Y) {
    [void][InputServer.Native]::SetCursorPos($X, $Y); Start-Sleep -Milliseconds 20
    [InputServer.Native]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60
    [InputServer.Native]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
}
function Get-HighContrast {
    $hc = New-Object InputServer.Native+HIGHCONTRAST; $hc.cbSize = [uint32][Runtime.InteropServices.Marshal]::SizeOf($hc)
    [void][InputServer.Native]::SystemParametersInfo(0x0042, $hc.cbSize, [ref]$hc, 0)   # SPI_GETHIGHCONTRAST
    $scheme = if ($hc.lpszDefaultScheme -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::PtrToStringUni($hc.lpszDefaultScheme) } else { '' }
    return ('on={0} flags=0x{1:x} scheme={2}' -f [bool]($hc.dwFlags -band 1), $hc.dwFlags, $scheme)
}
function Answer([string]$Text) { [Console]::Out.WriteLine(('{0} t={1}' -f $Text, $clock.ElapsedMilliseconds)); [Console]::Out.Flush() }

[Console]::Out.WriteLine('ready'); [Console]::Out.Flush()
while ($true) {
    $line = [Console]::In.ReadLine()
    if ($null -eq $line -or $line -eq 'quit') { break }
    $parts = $line.Split(' ', 2)
    $cmd = $parts[0]; $rest = if ($parts.Count -gt 1) { $parts[1] } else { '' }
    try {
        switch ($cmd) {
            'front' {
                $h = Find-Window $rest
                if ([InputServer.Native]::GetForegroundWindow() -ne $h) { Alt-Tap; [void][InputServer.Native]::SetForegroundWindow($h); Start-Sleep -Milliseconds 300 }
                if ([InputServer.Native]::GetForegroundWindow() -ne $h) { throw "the window '$rest...' is not in front" }
                Answer 'ok front'
            }
            'layout' { Answer ('ok layout ' + (Layout-Of (Find-Window $rest))) }
            'english' {
                $h = Find-Window $rest; $was = Layout-Of $h
                [void][InputServer.Native]::PostMessage($h, 0x0050, [IntPtr]::Zero, [IntPtr]0x08090809); Start-Sleep -Milliseconds 500
                Answer ('ok english was=' + $was + ' now=' + (Layout-Of $h))
            }
            'move' {
                $a = $rest.Split(' ') | ForEach-Object { [int]$_ }
                [void][InputServer.Native]::SetCursorPos($a[0] + 1, $a[1]); Start-Sleep -Milliseconds 30
                [void][InputServer.Native]::SetCursorPos($a[0], $a[1]); Start-Sleep -Milliseconds 30
                Answer 'ok move'
            }
            'click' { $a = $rest.Split(' ') | ForEach-Object { [int]$_ }; Press $a[0] $a[1]; Answer 'ok click' }
            'shiftclick' {
                $a = $rest.Split(' ') | ForEach-Object { [int]$_ }
                $e = New-Object 'Collections.Generic.List[InputServer.Keys+INPUT]'; $e.Add([InputServer.Keys+INPUT]::Key(0x10, $false, $false)); [InputServer.Keys]::Send($e)
                Start-Sleep -Milliseconds 40; Press $a[0] $a[1]; Start-Sleep -Milliseconds 40
                $e = New-Object 'Collections.Generic.List[InputServer.Keys+INPUT]'; $e.Add([InputServer.Keys+INPUT]::Key(0x10, $true, $false)); [InputServer.Keys]::Send($e)
                Answer 'ok shiftclick'
            }
            'drag' {
                $a = $rest.Split(' ') | ForEach-Object { [int]$_ }; $n = [Math]::Max($a[4], 1)
                [void][InputServer.Native]::SetCursorPos($a[0], $a[1]); Start-Sleep -Milliseconds 20
                [InputServer.Native]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60
                for ($i = 1; $i -le $n; $i++) { [void][InputServer.Native]::SetCursorPos([int]($a[0] + ($a[2] - $a[0]) * $i / $n), [int]($a[1] + ($a[3] - $a[1]) * $i / $n)); Start-Sleep -Milliseconds 15 }
                Start-Sleep -Milliseconds 100
                [InputServer.Native]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
                Answer 'ok drag'
            }
            'ctrldrag' {
                $a = $rest.Split(' ') | ForEach-Object { [int]$_ }; $n = [Math]::Max($a[4], 1)
                $e = New-Object 'Collections.Generic.List[InputServer.Keys+INPUT]'; $e.Add([InputServer.Keys+INPUT]::Key(0x11, $false, $false)); [InputServer.Keys]::Send($e)
                Start-Sleep -Milliseconds 60
                [void][InputServer.Native]::SetCursorPos($a[0], $a[1]); Start-Sleep -Milliseconds 20
                [InputServer.Native]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60
                for ($i = 1; $i -le $n; $i++) { [void][InputServer.Native]::SetCursorPos([int]($a[0] + ($a[2] - $a[0]) * $i / $n), [int]($a[1] + ($a[3] - $a[1]) * $i / $n)); Start-Sleep -Milliseconds 15 }
                Start-Sleep -Milliseconds 100
                [InputServer.Native]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60
                $e = New-Object 'Collections.Generic.List[InputServer.Keys+INPUT]'; $e.Add([InputServer.Keys+INPUT]::Key(0x11, $true, $false)); [InputServer.Keys]::Send($e)
                Answer 'ok ctrldrag'
            }
            'rclick' {
                $a = $rest.Split(' ') | ForEach-Object { [int]$_ }
                [void][InputServer.Native]::SetCursorPos($a[0], $a[1]); Start-Sleep -Milliseconds 20
                [InputServer.Native]::mouse_event(0x0008, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60
                [InputServer.Native]::mouse_event(0x0010, 0, 0, 0, [UIntPtr]::Zero)
                Answer 'ok rclick'
            }
            'ctrlchar' {
                $e = New-Object 'Collections.Generic.List[InputServer.Keys+INPUT]'
                $e.Add([InputServer.Keys+INPUT]::Key(0x11, $false, $false))
                [InputServer.Keys]::Char($e, $rest[0])
                $e.Add([InputServer.Keys+INPUT]::Key(0x11, $true, $false))
                [InputServer.Keys]::Send($e)
                Answer ('ok ctrlchar events=' + $e.Count)
            }
            'burstclick' {
                $p = $rest.Split(' ', 3); $x = [int]$p[0]; $y = [int]$p[1]; $halves = $p[2].Split('|', 2)
                [void][InputServer.Native]::SetCursorPos($x, $y); Start-Sleep -Milliseconds 30
                $all = New-Object 'Collections.Generic.List[InputServer.Keys+INPUT]'
                if ($halves[0]) { foreach ($g in (Parse-Keys $halves[0])) { $all.AddRange($g) } }
                $all.Add([InputServer.Keys+INPUT]::Button($false)); $all.Add([InputServer.Keys+INPUT]::Button($true))
                if ($halves.Count -gt 1 -and $halves[1]) { foreach ($g in (Parse-Keys $halves[1])) { $all.AddRange($g) } }
                $t0 = $clock.ElapsedMilliseconds; [InputServer.Keys]::Send($all)
                Answer ('ok burstclick events=' + $all.Count + ' ms=' + ($clock.ElapsedMilliseconds - $t0))
            }
            'wheel' {
                $a = $rest.Split(' ')
                [void][InputServer.Native]::SetCursorPos([int]$a[0], [int]$a[1]); Start-Sleep -Milliseconds 60
                $flag = if ($a[3] -eq 'h') { 0x01000 } else { 0x0800 }   # MOUSEEVENTF_HWHEEL, MOUSEEVENTF_WHEEL
                for ($i = 0; $i -lt [int]$a[4]; $i++) { [InputServer.Native]::mouse_event($flag, 0, 0, [int]$a[2], [UIntPtr]::Zero); Start-Sleep -Milliseconds 60 }
                Answer 'ok wheel'
            }
            'cursor' {
                $ci = New-Object InputServer.Native+CURSORINFO; $ci.cbSize = [uint32][Runtime.InteropServices.Marshal]::SizeOf($ci)
                [void][InputServer.Native]::GetCursorInfo([ref]$ci)
                $known = @{ 32512 = 'arrow'; 32513 = 'ibeam'; 32515 = 'cross'; 32649 = 'hand'; 32644 = 'sizewe'; 32646 = 'sizeall' }
                $name = 'none of the system''s'
                foreach ($k in $known.Keys) { if ([InputServer.Native]::LoadCursor([IntPtr]::Zero, [IntPtr]$k) -eq $ci.hCursor) { $name = $known[$k] } }
                Answer ('ok cursor handle=0x{0:x} system={1} at={2},{3}' -f [long]$ci.hCursor, $name, $ci.pt.X, $ci.pt.Y)
            }
            'dblclick' { $a = $rest.Split(' ') | ForEach-Object { [int]$_ }; Press $a[0] $a[1]; Start-Sleep -Milliseconds 80; Press $a[0] $a[1]; Answer 'ok dblclick' }
            'type' {
                $p = $rest.Split(' ', 2); $gap = [int]$p[0]
                $groups = Parse-Keys $p[1]
                foreach ($g in $groups) { [InputServer.Keys]::Send($g); if ($gap -gt 0) { Start-Sleep -Milliseconds $gap } }
                Answer ('ok type keys=' + $groups.Count)
            }
            'burst' {
                $groups = Parse-Keys $rest
                $all = New-Object 'Collections.Generic.List[InputServer.Keys+INPUT]'
                foreach ($g in $groups) { $all.AddRange($g) }
                $t0 = $clock.ElapsedMilliseconds; [InputServer.Keys]::Send($all)
                Answer ('ok burst keys=' + $groups.Count + ' events=' + $all.Count + ' ms=' + ($clock.ElapsedMilliseconds - $t0))
            }
            'imeoff' {
                $e = New-Object 'Collections.Generic.List[InputServer.Keys+INPUT]'; [InputServer.Keys]::Tap($e, 0x1A, $false); [InputServer.Keys]::Send($e)
                Start-Sleep -Milliseconds 100; Answer 'ok imeoff'
            }
            'imeon' {
                $e = New-Object 'Collections.Generic.List[InputServer.Keys+INPUT]'; [InputServer.Keys]::Tap($e, 0x16, $false); [InputServer.Keys]::Send($e)
                Start-Sleep -Milliseconds 100; Answer 'ok imeon'
            }
            'japanese' {
                $h = Find-Window $rest; $was = Layout-Of $h
                [void][InputServer.Native]::PostMessage($h, 0x0050, [IntPtr]::Zero, [IntPtr]0x04110411); Start-Sleep -Milliseconds 500
                Answer ('ok japanese was=' + $was + ' now=' + (Layout-Of $h))
            }
            'imestate' { $h = Find-Window $rest; Answer ('ok imestate ' + [InputServer.Ime]::State($h) + ' layout=' + (Layout-Of $h)) }
            'windows' { Answer ('ok windows ' + (([InputServer.Ime]::All()) -join ' ;; ')) }
            'imeui' { Answer ('ok imeui ' + (([InputServer.Ime]::Windows(1500)) -join ' ;; ')) }
            'screen' {
                $p = $rest.Split(' ', 5); $x = [int]$p[0]; $y = [int]$p[1]; $w = [int]$p[2]; $h = [int]$p[3]
                $bmp = New-Object Drawing.Bitmap $w, $h
                $g = [Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($x, $y, 0, 0, (New-Object Drawing.Size $w, $h)); $g.Dispose()
                $lit = 0; for ($yy = 0; $yy -lt $h; $yy += 7) { for ($xx = 0; $xx -lt $w; $xx += 7) { $c = $bmp.GetPixel($xx, $yy); if ($c.R + $c.G + $c.B -gt 0) { $lit++ } } }
                $bmp.Save($p[4], [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
                Answer ('ok screen sampled-not-black=' + $lit)
            }
            'hc' { Answer ('ok hc ' + (Get-HighContrast)) }
            'hcset' {
                # Only the on bit changes: the flags' other bits (the user's hotkey, 0x7e on this
                # machine) are kept, which SPI_SETHIGHCONTRAST with 0x1 or 0x0 drops (the eighth run).
                $p = $rest.Split(' ', 2)
                $now = New-Object InputServer.Native+HIGHCONTRAST; $now.cbSize = [uint32][Runtime.InteropServices.Marshal]::SizeOf($now)
                [void][InputServer.Native]::SystemParametersInfo(0x0042, $now.cbSize, [ref]$now, 0)
                $hc = New-Object InputServer.Native+HIGHCONTRAST; $hc.cbSize = $now.cbSize
                $hc.dwFlags = if ($p[0] -eq 'on') { $now.dwFlags -bor 0x1 } else { $now.dwFlags -band (-bnot 0x1) }
                $hc.lpszDefaultScheme = if ($p.Count -gt 1 -and $p[1]) { [Runtime.InteropServices.Marshal]::StringToHGlobalUni($p[1]) } else { $now.lpszDefaultScheme }
                $ok = [InputServer.Native]::SystemParametersInfo(0x0043, $hc.cbSize, [ref]$hc, 0x3)   # SPI_SETHIGHCONTRAST, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE
                Start-Sleep -Milliseconds 1500
                Answer ('ok hcset ' + $ok + ' ' + (Get-HighContrast))
            }
            default { Answer "err unknown command $cmd" }
        }
    } catch { Answer ('err ' + ($_.Exception.Message -replace "`r?`n", ' ')) }
}
