<#
.SYNOPSIS
    docs/specs/exsheet/verify-on-windows-11.md, Part A: what Excel shows and does for ADR-0063's
    readings (a Sheet's Cell Format), asked with real keys and the real mouse.

        powershell -File cell-format.ps1 -Case 0                  the environment, and the keyboard checked
        powershell -File cell-format.ps1 -Case 1,2,3b             cases of the procedure's tables
        powershell -File cell-format.ps1 -Case 8,9 -Zoom 150      at Excel's zoom 150% (files 8-z150-...)
        powershell -File cell-format.ps1 -Explore dialog          UI Automation of Format Cells, for writing
                                                                  group 5 (nothing recorded in the log)

    The method is the tenth run's (verification/2026-09-30-windows-excel-10/excel-only.ps1, on
    claude/exsheet-windows-verify-10), which this script began as. Every key a case asks about is sent
    through SendInput (a virtual-key and a scan code per key, the scan code from the layout of Excel's
    window), and every click is the real mouse. COM only sets a case up and reads the result: a value, a
    Number Format, a Font, a Fill, a Border. The script starts an Excel of its own for every case and
    ends only that one: it never attaches to a running Excel, because one of the user's may be open.

    Each case: a new workbook with one sheet, Sheet1, maximised, at 100% zoom (or -Zoom), A1 in view
    and selected; Excel's window switched to the English (UK) keyboard (or the Japanese one, IME off,
    where a case says so). Colours are sampled as hex from the pictures, never from COM.

    Pictures are the windows' own rendering (PrintWindow, PW_RENDERFULLCONTENT): the screen copy comes
    back empty on this machine since 2026-09-29. A dialog or a popup of Excel's is a window of its own,
    so it is captured on its own and laid over the window at its place.

    Outputs, beside this script:
      cell-format.jsonl  one line per case: the set-up, per state what COM read and what the pixels say
      shots\             per state: the cells (A1 to about H16, with the headings), any dialog or popup
                         of Excel's, and the crops a case names (enlarged four times where it says so)
    The full pictures stay on this machine, in %LOCALAPPDATA%\exgrid-layer3\cell-format-11\.
#>
#Requires -Version 5.1
param([string[]]$Case, [string]$Pass = '', [int]$Zoom = 100, [string]$Explore = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Case) { $Case = @($Case | ForEach-Object { $_ -split ',' } | Where-Object { $_ }) }

. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel\excel-driver.ps1')
$script:Shots = Join-Path $PSScriptRoot 'shots'
$Log = Join-Path $PSScriptRoot 'cell-format.jsonl'
$Raw = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\cell-format-11'
if (-not (Test-Path $Raw)) { [void](New-Item -ItemType Directory $Raw) }
if (-not (Test-Path $script:Shots)) { [void](New-Item -ItemType Directory $script:Shots) }
$Utf8 = New-Object Text.UTF8Encoding $false
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

Add-Type -Namespace CellFormat -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
[DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
[DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint thread);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr LoadKeyboardLayout(string id, uint flags);
[DllImport("user32.dll")] public static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint flags);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern short VkKeyScanEx(char c, IntPtr hkl);
[DllImport("user32.dll")] public static extern uint MapVirtualKeyEx(uint code, uint type, IntPtr hkl);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
[DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
[DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
[DllImport("imm32.dll")] public static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint action, uint param, ref HIGHCONTRAST value, uint winIni);
[StructLayout(LayoutKind.Sequential)] public struct HIGHCONTRAST { public uint cbSize; public uint dwFlags; public IntPtr lpszDefaultScheme; }
'@

# Keys through SendInput, a key down and a key up with its virtual-key code and its scan code, as a
# keyboard sends them (the eighth run: SendKeys and keybd_event reached Excel garbled on this machine,
# SendInput intact). The scan code is the one the given layout maps the key to.
Add-Type -TypeDefinition @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Threading;
namespace CellFormat {
public static class Keys {
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit, Size = 40)] struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public KEYBDINPUT ki; }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] i, int size);
    [DllImport("user32.dll")] static extern uint MapVirtualKeyEx(uint code, uint type, IntPtr hkl);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern short VkKeyScanEx(char c, IntPtr hkl);
    public static IntPtr Layout = IntPtr.Zero;
    static readonly HashSet<ushort> Extended = new HashSet<ushort> { 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2D, 0x2E };
    public static ushort Scan(ushort vk) { return (ushort)MapVirtualKeyEx(vk, 0, Layout); }
    public static void One(ushort vk, bool up) {
        var i = new INPUT[1]; i[0].type = 1; i[0].ki.wVk = vk; i[0].ki.wScan = Scan(vk);
        i[0].ki.dwFlags = (up ? 2u : 0u) | (Extended.Contains(vk) ? 1u : 0u);
        if (SendInput(1, i, Marshal.SizeOf(typeof(INPUT))) != 1) throw new Exception("SendInput failed: " + Marshal.GetLastWin32Error());
    }
    // A key with modifiers held (Shift 0x10, Ctrl 0x11, Alt 0x12), pressed in that order and let go in
    // the reverse order. Returns what was sent.
    public static string Chord(ushort[] mods, ushort vk) {
        foreach (var m in mods) { One(m, false); Thread.Sleep(20); }
        One(vk, false); Thread.Sleep(30); One(vk, true);
        for (int k = mods.Length - 1; k >= 0; k--) { Thread.Sleep(20); One(mods[k], true); }
        var names = new List<string>();
        foreach (var m in mods) names.Add(m == 0x10 ? "Shift" : m == 0x11 ? "Ctrl" : m == 0x12 ? "Alt" : m.ToString("x2"));
        names.Add("vk 0x" + vk.ToString("x2") + " (scan 0x" + Scan(vk).ToString("x2") + ")");
        return string.Join("+", names);
    }
    // The key that types a character on the layout, with the Shift, Ctrl or Alt that it needs, and any
    // modifiers asked for besides (Ctrl for "Ctrl with the character").
    public static string Char(char c, ushort[] extra) {
        short k = VkKeyScanEx(c, Layout);
        if (k == -1) throw new Exception("no key for character " + ((int)c).ToString("x4") + " on layout " + Layout.ToInt64().ToString("x8"));
        var mods = new List<ushort>(extra);
        if ((k & 0x200) != 0 && !mods.Contains(0x11)) mods.Add(0x11);
        if ((k & 0x400) != 0 && !mods.Contains(0x12)) mods.Add(0x12);
        if ((k & 0x100) != 0 && !mods.Contains(0x10)) mods.Add(0x10);
        return Chord(mods.ToArray(), (ushort)(k & 0xff));
    }
    public static short VkKeyScanOf(char c) { return VkKeyScanEx(c, Layout); }
}
}
'@

# Excel's windows, and what UI Automation reads in them, in C#: each read runs on a thread of its own
# with a time limit (on 2026-09-27 a UI Automation search in a dialog of Excel's hung for 50 s).
Add-Type -ReferencedAssemblies UIAutomationClient, UIAutomationTypes, WindowsBase -TypeDefinition @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Text; using System.Threading;
using System.Windows.Automation;
namespace CellFormat {
public static class Win {
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    // The visible top-level windows of a process, front to back.
    public static List<Dictionary<string, object>> Of(int pid) {
        var list = new List<Dictionary<string, object>>();
        EnumWindows((h, l) => {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == (uint)pid && IsWindowVisible(h)) {
                var c = new StringBuilder(256); GetClassName(h, c, 256);
                var t = new StringBuilder(512); GetWindowText(h, t, 512);
                RECT r; GetWindowRect(h, out r);
                var d = new Dictionary<string, object>();
                d["hwnd"] = (long)h; d["class"] = c.ToString(); d["title"] = t.ToString();
                d["rect"] = new int[] { r.Left, r.Top, r.Right, r.Bottom };
                list.Add(d);
            }
            return true;
        }, IntPtr.Zero);
        return list;
    }
}

public static class Ui {
    static Dictionary<string, object> Timed(Func<Dictionary<string, object>> f, int ms) {
        Dictionary<string, object> r = null; Exception err = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var t = new Thread(() => { try { r = f(); } catch (Exception e) { err = e; } });
        t.IsBackground = true; t.SetApartmentState(ApartmentState.MTA); t.Start();
        bool done = t.Join(ms);
        if (r == null) r = new Dictionary<string, object>();
        r["ms"] = sw.ElapsedMilliseconds;
        if (!done) r["error"] = "timed out after " + ms + " ms";
        else if (err != null) r["error"] = err.Message.Split('\n')[0];
        return r;
    }
    static int[] Rect(AutomationElement e) {
        var b = e.Current.BoundingRectangle;
        if (b.IsEmpty) return null;
        return new int[] { (int)b.Left, (int)b.Top, (int)b.Right, (int)b.Bottom };
    }
    static string Short(ControlType t) { return t.ProgrammaticName.Replace("ControlType.", ""); }
    static Dictionary<string, object> Describe(AutomationElement e, int depth) {
        var d = new Dictionary<string, object>();
        d["depth"] = depth; d["type"] = Short(e.Current.ControlType); d["name"] = e.Current.Name; d["class"] = e.Current.ClassName;
        if (!string.IsNullOrEmpty(e.Current.AutomationId)) d["id"] = e.Current.AutomationId;
        if (!e.Current.IsEnabled) d["enabled"] = false;
        var r = Rect(e); if (r != null) d["rect"] = r;
        object p;
        if (e.TryGetCurrentPattern(SelectionItemPattern.Pattern, out p)) d["selected"] = ((SelectionItemPattern)p).Current.IsSelected;
        if (e.TryGetCurrentPattern(TogglePattern.Pattern, out p)) d["toggle"] = ((TogglePattern)p).Current.ToggleState.ToString();
        if (e.TryGetCurrentPattern(ValuePattern.Pattern, out p)) d["value"] = ((ValuePattern)p).Current.Value;
        if (e.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out p)) d["expand"] = ((ExpandCollapsePattern)p).Current.ExpandCollapseState.ToString();
        string help = e.Current.HelpText; if (!string.IsNullOrEmpty(help)) d["help"] = help;
        return d;
    }
    // Every element under a window, depth first, in the order UI Automation gives them (the raw view).
    public static Dictionary<string, object> Tree(long hwnd, int maxDepth, int maxCount, int ms) {
        return Timed(() => {
            var items = new List<Dictionary<string, object>>();
            var o = new Dictionary<string, object>(); o["items"] = items;
            Walk(AutomationElement.FromHandle(new IntPtr(hwnd)), 0, maxDepth, maxCount, items);
            return o;
        }, ms);
    }
    static void Walk(AutomationElement e, int depth, int maxDepth, int maxCount, List<Dictionary<string, object>> items) {
        if (items.Count >= maxCount) return;
        var d = Describe(e, depth);
        items.Add(d);
        if (depth >= maxDepth) return;
        // A list without a name (Format Cells' Font, Font style and Size lists) is not walked: the Font
        // list holds every installed font, and walking it takes 10 s or more. Its value is its selection.
        if ((string)d["type"] == "List" && (string)d["name"] == "") { d["children"] = "not walked"; return; }
        var w = TreeWalker.RawViewWalker;
        var c = w.GetFirstChild(e);
        while (c != null && items.Count < maxCount) { Walk(c, depth + 1, maxDepth, maxCount, items); c = w.GetNextSibling(c); }
    }
    // The elements under a window whose name matches (a regular expression) and, if given, whose
    // control type is the one named (PowerShell passes $null as ""); at most maxCount, from a walk of at
    // most maxSeen elements.
    public static Dictionary<string, object> FindAll(long hwnd, string namePattern, string type, int maxCount, int maxSeen, int ms) {
        return Timed(() => {
            var items = new List<Dictionary<string, object>>();
            var o = new Dictionary<string, object>(); o["items"] = items;
            var rx = new System.Text.RegularExpressions.Regex(namePattern);
            var w = TreeWalker.RawViewWalker; var stack = new Stack<KeyValuePair<AutomationElement, int>>(); int seen = 0;
            stack.Push(new KeyValuePair<AutomationElement, int>(AutomationElement.FromHandle(new IntPtr(hwnd)), 0));
            while (stack.Count > 0 && seen < maxSeen && items.Count < maxCount) {
                var kv = stack.Pop(); var e = kv.Key; seen++;
                string n = e.Current.Name ?? "";
                if (rx.IsMatch(n) && (string.IsNullOrEmpty(type) || Short(e.Current.ControlType) == type)) items.Add(Describe(e, kv.Value));
                var kids = new List<AutomationElement>(); var c = w.GetFirstChild(e);
                while (c != null) { kids.Add(c); c = w.GetNextSibling(c); }
                for (int i = kids.Count - 1; i >= 0; i--) stack.Push(new KeyValuePair<AutomationElement, int>(kids[i], kv.Value + 1));
            }
            o["seen"] = seen;
            return o;
        }, ms);
    }
    // The element with the keyboard focus.
    public static Dictionary<string, object> Focused(int ms) {
        return Timed(() => {
            var f = AutomationElement.FocusedElement;
            var o = Describe(f, 0); o["processId"] = f.Current.ProcessId;
            return o;
        }, ms);
    }
    // The Formula Bar's text and selection, through its TextPattern (the tenth run).
    public static Dictionary<string, object> Bar(long hwnd, int ms) {
        return Timed(() => {
            var o = new Dictionary<string, object>();
            var w = TreeWalker.RawViewWalker; var stack = new Stack<AutomationElement>(); int seen = 0; AutomationElement found = null;
            stack.Push(AutomationElement.FromHandle(new IntPtr(hwnd)));
            while (stack.Count > 0 && seen < 6000) {
                var e = stack.Pop(); seen++;
                if (e.Current.ClassName == "XLFormulaBarEditor") { found = e; break; }
                var kids = new List<AutomationElement>(); var c = w.GetFirstChild(e);
                while (c != null) { kids.Add(c); c = w.GetNextSibling(c); }
                for (int i = kids.Count - 1; i >= 0; i--) stack.Push(kids[i]);
            }
            if (found == null) { o["error"] = "no XLFormulaBarEditor"; return o; }
            object p;
            if (found.TryGetCurrentPattern(ValuePattern.Pattern, out p)) o["value"] = ((ValuePattern)p).Current.Value;
            if (found.TryGetCurrentPattern(TextPattern.Pattern, out p)) {
                var tp = (TextPattern)p; var doc = tp.DocumentRange; var at = new List<object>(); var sel = new List<string>();
                foreach (var s in tp.GetSelection()) {
                    string st = s.GetText(-1); sel.Add(st);
                    var pre = doc.Clone();
                    pre.MoveEndpointByRange(System.Windows.Automation.Text.TextPatternRangeEndpoint.End, s, System.Windows.Automation.Text.TextPatternRangeEndpoint.Start);
                    int start = pre.GetText(-1).Length; at.Add(new object[] { start, start + st.Length });
                }
                o["document"] = doc.GetText(-1); o["selection"] = sel; o["selectionAt"] = at;
            }
            return o;
        }, ms);
    }
}
}
'@

# Pixels, in C#.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Imaging; using System.Runtime.InteropServices;
namespace CellFormat {
public class Img {
    public int W, H; public int[] P;
    public static Img Load(string path) { using (var b = new Bitmap(path)) { return From(b); } }
    public static Img From(Bitmap b) {
        var img = new Img(); img.W = b.Width; img.H = b.Height; img.P = new int[b.Width * b.Height];
        var d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < b.Height; y++) Marshal.Copy(IntPtr.Add(d.Scan0, y * d.Stride), img.P, y * b.Width, b.Width);
        b.UnlockBits(d);
        return img;
    }
    public int At(int x, int y) { return (x < 0 || y < 0 || x >= W || y >= H) ? -1 : (P[y * W + x] & 0xFFFFFF); }
}
public static class Px {
    public static string Hex(int c) { return c < 0 ? null : c.ToString("x6"); }
    static int R(int c) { return (c >> 16) & 255; } static int G(int c) { return (c >> 8) & 255; } static int B(int c) { return c & 255; }
    public static int Dist(int a, int b) {
        if (a < 0 || b < 0) return 999;
        return Math.Max(Math.Abs(R(a) - R(b)), Math.Max(Math.Abs(G(a) - G(b)), Math.Abs(B(a) - B(b))));
    }
    public static int Lum(int c) { return (R(c) * 30 + G(c) * 59 + B(c) * 11) / 100; }
    public static int Sat(int c) { if (c < 0) return 0; int mx = Math.Max(R(c), Math.Max(G(c), B(c))), mn = Math.Min(R(c), Math.Min(G(c), B(c))); return mx - mn; }
    // The most frequent colour in a box, x0..x1-1, y0..y1-1.
    public static int Mode(Img img, int x0, int y0, int x1, int y1) {
        var c = new Dictionary<int, int>();
        for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) { int p = img.At(x, y); if (p < 0) continue; int n; c.TryGetValue(p, out n); c[p] = n + 1; }
        int best = -1, bn = 0; foreach (var kv in c) if (kv.Value > bn) { bn = kv.Value; best = kv.Key; }
        return best;
    }
    // The colours in a box that lie further than tol from the ground, most frequent first ("hex:count"),
    // with the count of all such pixels: the text in a cell, its strokes' cores being the most frequent.
    public static Dictionary<string, object> Ink(Img img, int x0, int y0, int x1, int y1, int ground, int tol, int n) {
        var c = new Dictionary<int, int>(); int total = 0;
        for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) { int p = img.At(x, y); if (p < 0 || Dist(p, ground) <= tol) continue; total++; int k; c.TryGetValue(p, out k); c[p] = k + 1; }
        var l = new List<KeyValuePair<int, int>>(c); l.Sort((a, b) => b.Value.CompareTo(a.Value));
        var top = new List<string>(); for (int i = 0; i < l.Count && i < n; i++) top.Add(Hex(l[i].Key) + ":" + l[i].Value);
        int far = -1, fd = -1; foreach (var kv in c) { if (kv.Value < 3) continue; int d = Dist(kv.Key, ground); if (d > fd) { fd = d; far = kv.Key; } }
        var o = new Dictionary<string, object>(); o["ground"] = Hex(ground); o["pixels"] = total; o["top"] = top; o["furthestFromGround"] = Hex(far); o["distinct"] = c.Count;
        return o;
    }
    // The pixels across a line: at `along` (x for a horizontal line, y for a vertical one), from coord+from
    // to coord+to.
    public static string[] Across(Img img, bool horizontal, int coord, int along, int from, int to) {
        var o = new string[to - from + 1];
        for (int d = from; d <= to; d++) o[d - from] = Hex(horizontal ? img.At(along, coord + d) : img.At(coord + d, along));
        return o;
    }
    // A line's pattern along len px from x0 (horizontal) at row y, in four levels of luminance:
    // '#' below 64, '=' below 128, '-' below 192, '.' otherwise.
    public static string Pattern(Img img, bool horizontal, int coord, int start, int len) {
        var s = new char[len];
        for (int i = 0; i < len; i++) {
            int p = horizontal ? img.At(start + i, coord) : img.At(coord, start + i);
            int l = p < 0 ? 255 : Lum(p);
            s[i] = l < 64 ? '#' : l < 128 ? '=' : l < 192 ? '-' : '.';
        }
        return new string(s);
    }
    // The distinct colours along a row (or column) of len px, most frequent first.
    public static List<string> Colours(Img img, bool horizontal, int coord, int start, int len, int n) {
        var c = new Dictionary<int, int>();
        for (int i = 0; i < len; i++) { int p = horizontal ? img.At(start + i, coord) : img.At(coord, start + i); if (p < 0) continue; int k; c.TryGetValue(p, out k); c[p] = k + 1; }
        var l = new List<KeyValuePair<int, int>>(c); l.Sort((a, b) => b.Value.CompareTo(a.Value));
        var o = new List<string>(); for (int i = 0; i < l.Count && i < n; i++) o.Add(Hex(l[i].Key) + ":" + l[i].Value);
        return o;
    }
}
}
'@

# ---- Keys ---------------------------------------------------------------------------------------

$script:Named = @{ DOWN = 0x28; UP = 0x26; LEFT = 0x25; RIGHT = 0x27; HOME = 0x24; END = 0x23; PGUP = 0x21; PGDN = 0x22
    DEL = 0x2E; F1 = 0x70; F2 = 0x71; F8 = 0x77; ESC = 0x1B; ENTER = 0x0D; TAB = 0x09; BS = 0x08; SPACE = 0x20; ALT = 0x12 }
$script:Sent = New-Object Collections.ArrayList
# SendKeys' notation: {NAME} or {NAME n} for a named key; ^ (Ctrl) and + (Shift) before a {NAME} or a
# character hold it; {c} for a character SendKeys reserves; any other character as itself (with the
# Shift its layout needs). Returns what was sent, key by key.
function Type-Keys([string]$Spec, [int]$GapMs = 40) {
    $i = 0; $out = @()
    while ($i -lt $Spec.Length) {
        $mods = @()
        while ($i -lt $Spec.Length - 1 -and ($Spec[$i] -eq '^' -or $Spec[$i] -eq '+')) { $mods += $(if ($Spec[$i] -eq '^') { 0x11 } else { 0x10 }); $i++ }
        $c = $Spec[$i]
        if ($c -eq '{') {
            $end = $Spec.IndexOf('}', $i + 2)
            $inner = $Spec.Substring($i + 1, $end - $i - 1)
            $i = $end + 1
            if ($inner.Length -eq 1) { $out += [CellFormat.Keys]::Char($inner[0], [uint16[]]$mods); Start-Sleep -Milliseconds $GapMs; continue }
            $parts = $inner -split ' '; $n = if ($parts.Count -gt 1) { [int]$parts[1] } else { 1 }
            $vk = $script:Named[$parts[0].ToUpperInvariant()]
            if ($null -eq $vk) { throw "no key named $($parts[0])" }
            for ($j = 0; $j -lt $n; $j++) { $out += [CellFormat.Keys]::Chord([uint16[]]$mods, [uint16]$vk); Start-Sleep -Milliseconds $GapMs }
            continue
        }
        if ($c -eq '~' -and $mods.Count -eq 0) { $out += [CellFormat.Keys]::Chord([uint16[]]@(), [uint16]0x0D) }
        elseif ($mods.Count -gt 0 -and $c -match '[a-z0-9]') {
            # ^b, ^2: the key of that letter or digit, with only the modifiers written.
            $vk = [int][CellFormat.Keys]::VkKeyScanOf($c) -band 0xff
            $out += [CellFormat.Keys]::Chord([uint16[]]$mods, [uint16]$vk)
        }
        else { $out += [CellFormat.Keys]::Char($c, [uint16[]]$mods) }
        Start-Sleep -Milliseconds $GapMs
        $i++
    }
    [void]$script:Sent.AddRange(@($out))
    return ($out -join ', ')
}
# Ctrl with the key that types a character on the current layout (and the Shift that character needs).
function Ctrl-Char([char]$C) {
    Show-Excel $script:Xl
    $k = [int][CellFormat.Keys]::VkKeyScanOf($C)
    $sent = [CellFormat.Keys]::Char($C, [uint16[]]@(0x11))
    [void]$script:Sent.Add($sent)
    $script:VkScan["$C"] = ('0x{0:x3}' -f $k)
    Start-Sleep -Milliseconds 300
    return "Ctrl with '$C' as the character (vkKeyScan 0x$('{0:x3}' -f $k)): $sent"
}
# Ctrl+Shift with a key given by its virtual-key code (the US position of a character).
function Ctrl-Shift-Vk([int]$Vk, [string]$Name) {
    Show-Excel $script:Xl
    $sent = [CellFormat.Keys]::Chord([uint16[]]@(0x11, 0x10), [uint16]$Vk)
    [void]$script:Sent.Add($sent)
    Start-Sleep -Milliseconds 300
    return "Ctrl+Shift with VK ${Name}: $sent"
}
# The driver's Show-Excel, with the Alt tap through SendInput. Excel is in front when any window of this
# run's Excel is (its own popups and dialogs included).
function Show-Excel($xl) {
    $hwnd = $script:Hwnd
    [uint32]$frontPid = 0
    [void][ExcelDriver.Native]::GetWindowThreadProcessId([ExcelDriver.Native]::GetForegroundWindow(), [ref]$frontPid)
    if ($script:OwnPid -ne 0 -and $frontPid -eq $script:OwnPid) { return }
    [void][CellFormat.Keys]::Chord([uint16[]]@(), [uint16]0x12)
    if ([CellFormat.Native]::IsIconic($hwnd)) { [void][ExcelDriver.Native]::ShowWindow($hwnd, 9) }   # SW_RESTORE
    [void][ExcelDriver.Native]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 400
    [void][ExcelDriver.Native]::GetWindowThreadProcessId([ExcelDriver.Native]::GetForegroundWindow(), [ref]$frontPid)
    if ($frontPid -ne $script:OwnPid) { throw 'Excel is not in the foreground; keys would go elsewhere.' }
}
function Keys([string]$Spec, [int]$Settle = 400) {
    Show-Excel $script:Xl
    $s = Type-Keys $Spec
    Start-Sleep -Milliseconds $Settle
    return "$Spec ($s)"
}
function Send-KeysRaw([string]$Spec, [int]$Settle = 400) { [void](Keys $Spec $Settle) }

# ---- Mouse --------------------------------------------------------------------------------------

# A move as a hand makes it: 15 steps of 15 ms.
function Hand-Move([int]$X, [int]$Y) {
    $from = [System.Windows.Forms.Cursor]::Position
    for ($i = 1; $i -le 15; $i++) { [void][ExcelDriver.Native]::SetCursorPos([int]($from.X + ($X - $from.X) * $i / 15), [int]($from.Y + ($Y - $from.Y) * $i / 15)); Start-Sleep -Milliseconds 15 }
}
function Hand-Click([int]$X, [int]$Y) {
    Show-Excel $script:Xl
    Hand-Move $X $Y
    Start-Sleep -Milliseconds 100
    [ExcelDriver.Native]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 90
    [ExcelDriver.Native]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 300
    return "a click at ($X, $Y)"
}
# A drag from one cell's middle to another's, Ctrl held throughout when asked.
function Drag-Cells([string]$From, [string]$To, [switch]$Ctrl) {
    $a = Cell-Rect $From; $b = Cell-Rect $To
    $x1 = [int](($a.Left + $a.Right) / 2); $y1 = [int](($a.Top + $a.Bottom) / 2); $x2 = [int](($b.Left + $b.Right) / 2); $y2 = [int](($b.Top + $b.Bottom) / 2)
    Show-Excel $script:Xl
    Hand-Move $x1 $y1; Start-Sleep -Milliseconds 100
    if ($Ctrl) { [CellFormat.Keys]::One(0x11, $false); Start-Sleep -Milliseconds 60 }
    [ExcelDriver.Native]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 120
    for ($i = 1; $i -le 20; $i++) { [void][ExcelDriver.Native]::SetCursorPos([int]($x1 + ($x2 - $x1) * $i / 20), [int]($y1 + ($y2 - $y1) * $i / 20)); Start-Sleep -Milliseconds 20 }
    Start-Sleep -Milliseconds 150
    [ExcelDriver.Native]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 100
    if ($Ctrl) { [CellFormat.Keys]::One(0x11, $true) }
    Start-Sleep -Milliseconds 300
    return "a drag from $From ($x1, $y1) to $To ($x2, $y2)$(if ($Ctrl) { ' with Ctrl held' })"
}
function Park-Mouse { [void][ExcelDriver.Native]::SetCursorPos([int]$script:Area.Right - 200, [int]$script:Area.Bottom - 300) }

# ---- The log ------------------------------------------------------------------------------------

function Write-Line($Row) { [IO.File]::AppendAllText($Log, (($Row | ConvertTo-Json -Compress -Depth 18) + "`n"), $Utf8) }
function Say([string]$Text) { Write-Host ('{0} {1}' -f (Get-Date -Format 'HH:mm:ss.fff'), $Text) }
function G($D, [string]$K) { if ($null -ne $D -and (@($D.Keys) -contains $K)) { return $D[$K] }; return $null }

# ---- Excel: one of this script's own, for each case --------------------------------------------

$script:Xl = $null
$script:OwnPid = 0
$script:Book = $null
$script:ExcelsAtStart = @(Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$script:OwnPids = @()

function Start-OwnExcel {
    $before = @(Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
    $xl = New-Object -ComObject Excel.Application
    [uint32]$p = 0
    [void][CellFormat.Native]::GetWindowThreadProcessId([IntPtr][long]$xl.Hwnd, [ref]$p)
    if ($before -contains [int]$p) {
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl)
        throw "New-Object gave an Excel that was already running (process $p); nothing was changed in it"
    }
    $script:OwnPid = [int]$p
    $script:OwnPids += [int]$p
    $xl.Visible = $true
    $xl.DisplayAlerts = $false
    $script:Xl = $xl
    $script:OpenedByItself = @($xl.Workbooks | ForEach-Object { [string]$_.Name })
    Say "own Excel: process $($script:OwnPid); Excel processes left alone: $($before -join ', '); workbooks it opened by itself: $($script:OpenedByItself.Count)"
    return $xl
}
function Stop-OwnExcel {
    if ($null -eq $script:Xl) { return }
    $others = @()
    try { $others = @($script:Xl.Workbooks | ForEach-Object { [string]$_.Name } | Where-Object { $_ -ne [string]$script:Book.Name }) } catch { }
    if ($others.Count -eq 0) {
        try { $script:Book.Close($false) } catch { }
        try { $script:Xl.Quit() } catch { }
    }
    try { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($script:Book) } catch { }
    $script:Book = $null; $script:Ws = $null
    try { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($script:Xl) } catch { }
    $script:Xl = $null
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    for ($i = 0; $i -lt 20; $i++) { if (-not (Get-Process -Id $script:OwnPid -ErrorAction SilentlyContinue)) { break }; Start-Sleep -Milliseconds 500 }
    if (Get-Process -Id $script:OwnPid -ErrorAction SilentlyContinue) { Stop-Process -Id $script:OwnPid -Force; Say "own Excel $($script:OwnPid) ended by Stop-Process (workbooks not its own: $($others.Count))" }
    else { Say "own Excel $($script:OwnPid) quit" }
}

# ---- The keyboard -------------------------------------------------------------------------------

function Get-ExcelLayout { [uint32]$p = 0; $t = [CellFormat.Native]::GetWindowThreadProcessId($script:Hwnd, [ref]$p); return ('0x{0:x8}' -f [long][CellFormat.Native]::GetKeyboardLayout($t)) }
# The IME of Excel's window: open or not, and its conversion mode (WM_IME_CONTROL to its default IME window).
function Get-Ime {
    $ime = [CellFormat.Native]::ImmGetDefaultIMEWnd($script:Hwnd)
    if ($ime -eq [IntPtr]::Zero) { return 'no IME window' }
    $open = [CellFormat.Native]::SendMessage($ime, 0x0283, [IntPtr]5, [IntPtr]::Zero)   # IMC_GETOPENSTATUS
    $conv = [CellFormat.Native]::SendMessage($ime, 0x0283, [IntPtr]1, [IntPtr]::Zero)   # IMC_GETCONVERSIONMODE
    return ('open={0} conversion=0x{1:x}' -f [long]$open, [long]$conv)
}
$script:FirstExcelLayout = $null
$script:Layouts = @{ uk = '00000809'; ja = '00000411' }
$script:VkScan = [ordered]@{}
# Excel's window to a keyboard layout, as Win+Space would (WM_INPUTLANGCHANGEREQUEST); the IME turned
# off (VK_IME_OFF); and the layout the keys are mapped through set to the same one.
function Set-Keyboard([string]$Which = 'uk') {
    $was = Get-ExcelLayout
    if ($null -eq $script:FirstExcelLayout) { $script:FirstExcelLayout = $was }
    $hkl = [CellFormat.Native]::LoadKeyboardLayout($script:Layouts[$Which], 0)
    Show-Excel $script:Xl
    [void][CellFormat.Native]::PostMessage($script:Hwnd, 0x0050, [IntPtr]::Zero, $hkl)   # WM_INPUTLANGCHANGEREQUEST
    Start-Sleep -Milliseconds 700
    [CellFormat.Native]::keybd_event(0x1A, 0, 0, [UIntPtr]::Zero); [CellFormat.Native]::keybd_event(0x1A, 0, 2, [UIntPtr]::Zero)   # VK_IME_OFF
    Start-Sleep -Milliseconds 300
    [CellFormat.Keys]::Layout = $hkl
    $script:LayoutName = $Which
    return [ordered]@{ asked = $Which; hkl = ('0x{0:x8}' -f [long]$hkl); excelWas = $was; excelNow = Get-ExcelLayout; ime = Get-Ime }
}
function Restore-Keyboard {
    Show-Excel $script:Xl
    [void][CellFormat.Native]::PostMessage($script:Hwnd, 0x0050, [IntPtr]::Zero, [IntPtr][long]([Convert]::ToInt64($script:FirstExcelLayout, 16)))
    Start-Sleep -Milliseconds 600
    [CellFormat.Native]::keybd_event(0x1A, 0, 0, [UIntPtr]::Zero); [CellFormat.Native]::keybd_event(0x1A, 0, 2, [UIntPtr]::Zero)   # VK_IME_OFF
    Start-Sleep -Milliseconds 200
    return [ordered]@{ restoredTo = $script:FirstExcelLayout; excelNow = Get-ExcelLayout }
}

# ---- COM: set-up and reads ----------------------------------------------------------------------

# A Number Format through COM in en-US codes (PowerShell 5.1 passes the user's locale otherwise).
function Set-NF($Range, [string]$Code) { Set-ComProperty $Range 'NumberFormat' $Code }
function Get-NF($Range) { return [string](Get-ComProperty $Range 'NumberFormat') }
function Hex-Of($BgrColour) {
    if ($null -eq $BgrColour -or $BgrColour -is [DBNull]) { return $null }
    $c = [long][double]$BgrColour
    return ('#{0:X2}{1:X2}{2:X2}' -f ($c -band 0xff), (($c -shr 8) -band 0xff), (($c -shr 16) -band 0xff))
}
$script:LineStyles = @{ 1 = 'Continuous'; -4115 = 'Dash'; 4 = 'DashDot'; 5 = 'DashDotDot'; -4118 = 'Dot'; -4119 = 'Double'; -4142 = 'None'; 13 = 'SlantDashDot' }
$script:Weights = @{ 1 = 'Hairline'; 2 = 'Thin'; -4138 = 'Medium'; 4 = 'Thick' }
$script:Underlines = @{ -4142 = 'None'; 2 = 'Single'; -4119 = 'Double'; 4 = 'SingleAccounting'; 5 = 'DoubleAccounting' }
function Name-Of($Map, $V) { if ($null -eq $V -or $V -is [DBNull]) { return 'mixed (null)' }; $k = [int]$V; if ($Map.ContainsKey($k)) { return $Map[$k] }; return [string]$V }
# One edge of a range: LineStyle, Weight, Color, as names.
function Edge-Of($Range, [int]$Index) {
    $b = $Range.Borders.Item($Index)
    $ls = $b.LineStyle
    return ('{0} {1} {2}' -f (Name-Of $script:LineStyles $ls), (Name-Of $script:Weights $b.Weight), (Hex-Of $b.Color))
}
$script:EdgeNames = [ordered]@{ left = 7; top = 8; bottom = 9; right = 10 }
# Every cell's four edges, for each address (a range is expanded to its cells).
function Read-Edges([string[]]$Addresses) {
    $o = [ordered]@{}
    foreach ($a in $Addresses) {
        foreach ($c in $script:Ws.Range($a).Cells) {
            $addr = [string]$c.Address($false, $false); $e = [ordered]@{}
            foreach ($n in $script:EdgeNames.Keys) { $e[$n] = Edge-Of $c $script:EdgeNames[$n] }
            $o[$addr] = $e
        }
    }
    return $o
}
# Only the edges that hold a line, as "B2 bottom: Continuous Thick #000000", for the log's summary.
function Lines-In($Edges) {
    $o = @()
    foreach ($a in $Edges.Keys) { foreach ($n in $Edges[$a].Keys) { if ($Edges[$a][$n] -notlike 'None *') { $o += "$a ${n}: $($Edges[$a][$n])" } } }
    return $o
}
function Read-Font([string[]]$Addresses) {
    $o = [ordered]@{}
    foreach ($a in $Addresses) {
        $r = $script:Ws.Range($a); $f = $r.Font
        $o[$a] = [ordered]@{ value = [string]$r.Formula; text = [string]$r.Text; bold = $f.Bold; italic = $f.Italic; underline = (Name-Of $script:Underlines $f.Underline); strikethrough = $f.Strikethrough
            fontColour = (Hex-Of $f.Color); numberFormat = (Get-NF $r); numberFormatLocal = [string]$r.NumberFormatLocal; columnWidth = [double]$r.ColumnWidth }
    }
    return $o
}
function Read-Fill([string[]]$Addresses) {
    $o = [ordered]@{}
    foreach ($a in $Addresses) { $i = $script:Ws.Range($a).Interior; $o[$a] = ('pattern {0} colorIndex {1} color {2}' -f $i.Pattern, $i.ColorIndex, (Hex-Of $i.Color)) }
    return $o
}
function Selection-Now { return [ordered]@{ selection = [string]$script:Xl.Selection.Address($false, $false); activeCell = [string]$script:Xl.ActiveCell.Address($false, $false) } }

# Escape until Excel answers COM again (it refuses every call while an edit or a dialog is open).
function Until-Ready([int]$Tries = 8) {
    for ($i = 0; $i -lt $Tries; $i++) {
        try { [void]$script:Xl.ActiveWorkbook.Name; [void]$script:Xl.ActiveCell.Address($false, $false); if ($script:Xl.Ready) { return $i } } catch { }
        Send-KeysRaw '{ESC}' 500
    }
    throw 'Excel did not come back to Ready'
}

# ---- Geometry -----------------------------------------------------------------------------------

# A cell's place on the screen, through COM while Excel is Ready. While an edit or a dialog is open
# ($script:NoCom), COM is not called (it is refused, or it can wait as long as a dialog stays) and the
# place last read is used.
$script:RectCache = @{}
$script:NoCom = $false
function Cell-Rect([string]$A1) {
    if ($script:NoCom) {
        if ($script:RectCache.ContainsKey($A1)) { return $script:RectCache[$A1] }
        throw "no place read for $A1 before the edit or dialog opened"
    }
    $r = $script:Ws.Range($A1); $p = $script:Xl.ActiveWindow.ActivePane
    $o = [ordered]@{ Left = [int]$p.PointsToScreenPixelsX($r.Left); Top = [int]$p.PointsToScreenPixelsY($r.Top)
        Right = [int]$p.PointsToScreenPixelsX($r.Left + $r.Width); Bottom = [int]$p.PointsToScreenPixelsY($r.Top + $r.Height) }
    $script:RectCache[$A1] = $o
    return $o
}
# The view the cell pictures are cut from: A1 to H16 with the headings, and what each cell lies at.
function Get-Geometry {
    $win = $script:Book.Windows.Item(1)
    $a1 = Cell-Rect 'A1'; $h16 = Cell-Rect 'H16'
    $w = New-Object ExcelDriver.Native+RECT; [void][ExcelDriver.Native]::GetWindowRect($script:Hwnd, [ref]$w)
    return [ordered]@{ window = [ordered]@{ Left = $w.Left; Top = $w.Top; Right = $w.Right; Bottom = $w.Bottom }; windowState = [int]$win.WindowState
        zoom = [int]$win.Zoom; dpi = [int][CellFormat.Native]::GetDpiForWindow($script:Hwnd); visible = [string]$win.VisibleRange.Address($false, $false)
        gridlines = [bool]$win.DisplayGridlines; gridlineColour = (Hex-Of $win.GridlineColor); gridlineColorIndex = [int]$win.GridlineColorIndex
        view = [ordered]@{ Left = $a1.Left - 70; Top = $a1.Top - 45; Right = $h16.Right + 2; Bottom = $h16.Bottom + 2 }
        A1 = $a1; H16 = $h16 }
}

# ---- Pictures -----------------------------------------------------------------------------------

$script:Area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$script:Shadows = @('MSO_BORDEREFFECT_WINDOW_CLASS')
# One picture of the work area: every visible window of this run's Excel drawn by itself (PrintWindow),
# back to front, at its place.
function Grab-Scene {
    $wins = @([CellFormat.Win]::Of($script:OwnPid))
    $bmp = New-Object Drawing.Bitmap $script:Area.Width, $script:Area.Height
    $g = [Drawing.Graphics]::FromImage($bmp); $g.Clear([Drawing.Color]::FromArgb(255, 64, 64, 64))
    for ($i = $wins.Count - 1; $i -ge 0; $i--) {
        $r = $wins[$i].rect; $w = $r[2] - $r[0]; $h = $r[3] - $r[1]
        if ($w -le 0 -or $h -le 0 -or $script:Shadows -contains $wins[$i].class) { continue }
        $one = New-Object Drawing.Bitmap $w, $h
        $og = [Drawing.Graphics]::FromImage($one); $hdc = $og.GetHdc()
        $ok = [CellFormat.Native]::PrintWindow([IntPtr][long]$wins[$i].hwnd, $hdc, 2)   # PW_RENDERFULLCONTENT
        $og.ReleaseHdc($hdc); $og.Dispose()
        $wins[$i].printed = [bool]$ok
        if ($ok) { $g.DrawImage($one, $r[0] - $script:Area.Left, $r[1] - $script:Area.Top, $w, $h) }
        $one.Dispose()
    }
    $g.Dispose()
    return @{ bmp = $bmp; windows = $wins }
}
# The account's initials sit at the right of the title bar, left of the window's buttons.
function Blank-Accounts($Bmp, $Wins) {
    $g = [Drawing.Graphics]::FromImage($Bmp)
    foreach ($w in $Wins) {
        if ($w.class -ne 'XLMAIN') { continue }
        $x0 = $w.rect[2] - 640 - $script:Area.Left; $y0 = [Math]::Max($w.rect[1] - $script:Area.Top, 0)
        $ground = $Bmp.GetPixel([Math]::Max($x0 - 10, 0), $y0 + 40)
        $brush = New-Object Drawing.SolidBrush $ground
        $g.FillRectangle($brush, $x0, $y0, 415, 88)
        $brush.Dispose()
    }
    $g.Dispose()
}
function Save-Crop($Bmp, $Rect, [string]$Name, [int]$Scale = 1) {
    $l = [Math]::Max([int]$Rect.Left - $script:Area.Left, 0); $t = [Math]::Max([int]$Rect.Top - $script:Area.Top, 0)
    $r = [Math]::Min([int]$Rect.Right - $script:Area.Left, $Bmp.Width); $b = [Math]::Min([int]$Rect.Bottom - $script:Area.Top, $Bmp.Height)
    if ($r -le $l -or $b -le $t) { return $null }
    $w = $r - $l; $h = $b - $t
    $out = New-Object Drawing.Bitmap ($w * $Scale), ($h * $Scale)
    $gr = [Drawing.Graphics]::FromImage($out)
    $gr.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $gr.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
    $gr.DrawImage($Bmp, (New-Object Drawing.Rectangle 0, 0, ($w * $Scale), ($h * $Scale)), (New-Object Drawing.Rectangle $l, $t, $w, $h), [Drawing.GraphicsUnit]::Pixel)
    $gr.Dispose(); $out.Save((Join-Path $script:Shots ($Name + '.png')), [Drawing.Imaging.ImageFormat]::Png); $out.Dispose()
    return "shots/$Name.png"
}

# One state: a picture of the work area (kept whole on this machine), the cells cut from it, and every
# other window of Excel's that is showing (a dialog, a list), cut and read through UI Automation.
# $script:Img holds the picture for the case's pixel readings; $script:Pics keeps each state's.
function Snap([string]$State, [int]$WaitMs = 500, [int]$TreeDepth = 0) {
    Start-Sleep -Milliseconds $WaitMs
    $a = Grab-Scene
    $id = '{0}-{1}' -f $script:CaseId, $State
    $a.bmp.Save((Join-Path $Raw "$id.png"), [Drawing.Imaging.ImageFormat]::Png)
    $script:Img = [CellFormat.Img]::From($a.bmp)
    $script:Pics[$State] = $script:Img
    Blank-Accounts $a.bmp $a.windows
    $shots = [ordered]@{ cells = (Save-Crop $a.bmp $script:Geo.view $id) }
    $wins = @(); $k = 0
    foreach ($w in $a.windows) {
        if ($w.class -eq 'XLMAIN' -or $script:Shadows -contains $w.class) { continue }
        $k++
        $wr = [ordered]@{ Left = $w.rect[0]; Top = $w.rect[1]; Right = $w.rect[2]; Bottom = $w.rect[3] }
        $one = [ordered]@{ class = $w.class; title = $w.title; rect = $w.rect; printed = $w.printed; shot = (Save-Crop $a.bmp $wr "$id-window-$k") }
        if ($TreeDepth -gt 0) { $one.uia = [CellFormat.Ui]::Tree([long]$w.hwnd, $TreeDepth, 400, 8000) }
        $wins += $one
    }
    $script:LastBmp = $a.bmp
    return [ordered]@{ state = $State; time = (Get-Date -Format 'HH:mm:ss.fff'); shots = $shots; windows = $wins }
}
function Done-Snap { if ($script:LastBmp) { $script:LastBmp.Dispose(); $script:LastBmp = $null } }
# A crop of the last picture, by cell addresses (padded), enlarged four times.
function Crop-Cells([string]$From, [string]$To, [string]$Name, [int]$Pad = 8, [int]$Scale = 4) {
    $a = Cell-Rect $From; $b = Cell-Rect $To
    return Save-Crop $script:LastBmp ([ordered]@{ Left = $a.Left - $Pad; Top = $a.Top - $Pad; Right = $b.Right + $Pad; Bottom = $b.Bottom + $Pad }) ('{0}-{1}' -f $script:CaseId, $Name) $Scale
}

# ---- Pixel readings -----------------------------------------------------------------------------

# Image coordinates from screen ones.
function IX([int]$X) { return $X - $script:Area.Left }
function IY([int]$Y) { return $Y - $script:Area.Top }
# The text in a cell: its ground (the most frequent pixel inside it, 3 px in from its edges) and the
# colours further than 24 from that ground, most frequent first. The first is the strokes' core.
function Ink-Of($Img, [string]$A1) {
    $r = Cell-Rect $A1
    $x0 = (IX $r.Left) + 3; $y0 = (IY $r.Top) + 3; $x1 = (IX $r.Right) - 3; $y1 = (IY $r.Bottom) - 3
    $ground = [CellFormat.Px]::Mode($Img, $x0, $y0, $x1, $y1)
    $o = [CellFormat.Px]::Ink($Img, $x0, $y0, $x1, $y1, $ground, 24, 6)
    $top = @($o['top'])
    return [ordered]@{ cell = $A1; sampled = $(if ($top.Count) { '#' + ($top[0] -split ':')[0].ToUpperInvariant() } else { 'no text' }); ground = '#' + ([string]$o['ground']).ToUpperInvariant()
        top = $top; furthestFromGround = $o['furthestFromGround']; inkPixels = $o['pixels']; distinct = $o['distinct'] }
}
# The pixels across one edge of a cell, from 6 px before to 6 px after the coordinate PointsToScreenPixels
# gives for it (d = 0), in screen order (top to bottom across a horizontal edge, left to right across a
# vertical one), at a quarter, a half and three quarters along it; written as runs: "-6..-2 ffffff |
# -1 d4d4d4 | 0..6 ffff00".
function Across-Edge($Img, [string]$A1, [string]$Side, [int]$Reach = 6) {
    $r = Cell-Rect $A1
    $horizontal = $Side -eq 'top' -or $Side -eq 'bottom'
    $coord = switch ($Side) { 'top' { IY $r.Top } 'bottom' { IY $r.Bottom } 'left' { IX $r.Left } 'right' { IX $r.Right } }
    $o = [ordered]@{ edge = "$A1 $Side"; at = $coord }
    $seen = @{}
    foreach ($f in 0.25, 0.5, 0.75) {
        $along = if ($horizontal) { (IX $r.Left) + [int](($r.Right - $r.Left) * $f) } else { (IY $r.Top) + [int](($r.Bottom - $r.Top) * $f) }
        $px = [CellFormat.Px]::Across($Img, $horizontal, $coord, $along, -$Reach, $Reach)
        # Screen order runs from the top (or left); written here from the neighbour's side to the cell's.
        $runs = Runs-Of $px (-$Reach)
        $o["at$([int]($f * 100))"] = $runs
        $seen[$runs] = 1
    }
    $o.same = ($seen.Count -eq 1)
    return $o
}
function Runs-Of([string[]]$Px, [int]$Start) {
    $out = @(); $i = 0
    while ($i -lt $Px.Count) {
        $j = $i; while ($j + 1 -lt $Px.Count -and $Px[$j + 1] -eq $Px[$i]) { $j++ }
        $a = $Start + $i; $b = $Start + $j
        $out += $(if ($a -eq $b) { "$a $($Px[$i])" } else { "$a..$b $($Px[$i])" })
        $i = $j + 1
    }
    return ($out -join ' | ')
}
# A line along an edge: for each row (or column) from -Reach to +Reach around the edge, its pattern over
# Len px from 8 px inside the cell's start, and the colours along it.
function Line-Pattern($Img, [string]$A1, [string]$Side, [int]$Len = 40, [int]$Reach = 5) {
    $r = Cell-Rect $A1
    $horizontal = $Side -eq 'top' -or $Side -eq 'bottom'
    $coord = switch ($Side) { 'top' { IY $r.Top } 'bottom' { IY $r.Bottom } 'left' { IX $r.Left } 'right' { IX $r.Right } }
    $start = if ($horizontal) { (IX $r.Left) + 8 } else { (IY $r.Top) + 8 }
    $rows = [ordered]@{}
    for ($d = -$Reach; $d -le $Reach; $d++) {
        $p = [CellFormat.Px]::Pattern($Img, $horizontal, $coord + $d, $start, $Len)
        $rows["$d"] = $p + '  ' + ((@([CellFormat.Px]::Colours($Img, $horizontal, $coord + $d, $start, $Len, 4))) -join ' ')
    }
    return [ordered]@{ edge = "$A1 $Side"; at = $coord; from = $start; length = $Len; levels = "'#' luminance < 64, '=' < 128, '-' < 192, '.' otherwise"; rows = $rows }
}

# ---- Set-up -------------------------------------------------------------------------------------

# A fresh Excel and workbook: one sheet, Sheet1, maximised at the zoom asked, A1 in view and selected;
# Excel's window on the keyboard layout asked, the IME off.
function Set-Up([string]$Layout = 'uk') {
    if ($script:Xl) { Stop-OwnExcel }
    [void](Start-OwnExcel)
    $wb = $script:Xl.Workbooks.Add()
    $wb.EnableAutoRecover = $false
    while ($wb.Worksheets.Count -gt 1) { $wb.Worksheets.Item($wb.Worksheets.Count).Delete() }
    $ws = $wb.Worksheets.Item(1)
    if ($ws.Name -ne 'Sheet1') { $ws.Name = 'Sheet1' }
    $script:Book = $wb; $script:Ws = $ws
    $win = $wb.Windows.Item(1)
    $script:Hwnd = [IntPtr][long]$win.Hwnd
    Set-Content $script:HwndFile ([long]$script:Hwnd)
    $win.WindowState = -4137   # xlMaximized
    $win.Zoom = $Zoom
    [void]$script:Xl.Goto($ws.Range('A1'), $true)
    [void]$ws.Range('A1').Select()
    $script:RectCache = @{}; $script:NoCom = $false
    Show-Excel $script:Xl
    $k = Set-Keyboard $Layout
    Start-Sleep -Milliseconds 300
    return $k
}
# A value as typed into the Formula Bar (en-US): '-5', 'TRUE', '=1/0', 'abc'.
function Put([string]$A1, [string]$V) { Set-ComProperty $script:Ws.Range($A1) 'Formula' $V }
function Set-Fill([string]$A1, [int]$Colour) { Set-ComProperty $script:Ws.Range($A1).Interior 'Color' $Colour }
function Set-Edge([string]$A1, [int]$Index, [int]$Style, [int]$Weight, $Colour = $null) {
    $b = $script:Ws.Range($A1).Borders.Item($Index)
    Set-ComProperty $b 'LineStyle' $Style
    Set-ComProperty $b 'Weight' $Weight
    if ($null -ne $Colour) { Set-ComProperty $b 'Color' ([int]$Colour) }
}
function Select-Cell([string]$A1) { [void]$script:Ws.Range($A1).Select() }
$Yellow = 0x00FFFF; $Red = 0x0000FF; $Blue = 0xFF0000; $Black = 0; $White = 0xFFFFFF   # COM colours are BGR

# ---- Cases --------------------------------------------------------------------------------------

$Cases = [ordered]@{}
function Def([string]$Id, [string]$Group, [string]$Setup, [string]$Asked, [string]$Reading, [scriptblock]$Body, [string]$Layout = 'uk', [int]$Due = 180) {
    $Cases[$Id] = [pscustomobject]@{ Id = $Id; Group = $Group; Setup = $Setup; Asked = $Asked; Reading = $Reading; Body = $Body; Layout = $Layout; Due = $Due }
}
# A state of the case: the picture, then what the scriptblock adds (COM reads and pixel readings, made
# while the picture is current).
# (Its own variables are named apart: the scriptblock runs in its scope's child and reads the case's.)
function State([string]$StateName, [string]$StateDid, [scriptblock]$StateThen = $null, [int]$StateTree = 0, [int]$StateWait = 500) {
    $__state = Snap $StateName $StateWait $StateTree
    $__state.did = $StateDid
    if ($StateThen) { $__extra = & $StateThen; if ($__extra) { foreach ($__key in $__extra.Keys) { $__state[$__key] = $__extra[$__key] } } }
    # Rows 1 to 16's heights (points), and their tops on the screen, while COM may be called.
    if (-not $script:NoCom -and $script:Ws) {
        $__h = @(); for ($__r = 1; $__r -le 16; $__r++) { $__h += ('{0}:{1}pt' -f $__r, $script:Ws.Rows($__r).RowHeight) }
        $__state.rowHeights = $__h -join ' '
    }
    Done-Snap
    [void]$script:States.Add($__state)
    Say ("  {0} {1}: {2}" -f $script:CaseId, $StateName, $StateDid)
    return $__state
}

# Group 1: a Number Format's colour.
$script:ColourNames = @('Black', 'Blue', 'Cyan', 'Green', 'Magenta', 'Red', 'White', 'Yellow')
Def '1' 'number-format-colour' 'A1 = -5, B1 = 5; format [Black]0 ... [Yellow]0, one name per row (rows 1-8); for [White], row 7 filled black (Rows(7).Interior)' 'The RGB of each of the eight names' "Excel's legacy palette: #000000, #0000FF, #00FFFF, #00FF00, #FF00FF, #FF0000, #FFFFFF, #FFFF00" {
    for ($r = 1; $r -le 8; $r++) { Put "A$r" '-5'; Put "B$r" '5'; Set-NF $script:Ws.Range("A${r}:B$r") "[$($script:ColourNames[$r - 1])]0" }
    Set-ComProperty $script:Ws.Rows(7).Interior 'Color' $Black
    [void](State 'set' 'COM set-up' {
        $rows = @()
        for ($r = 1; $r -le 8; $r++) {
            $rows += [ordered]@{ row = $r; name = $script:ColourNames[$r - 1]; format = (Get-NF $script:Ws.Range("A$r")); a = (Ink-Of $script:Img "A$r"); b = (Ink-Of $script:Img "B$r")
                textA = [string]$script:Ws.Range("A$r").Text; textB = [string]$script:Ws.Range("B$r").Text; fontColourCom = (Hex-Of $script:Ws.Range("A$r").Font.Color) }
        }
        @{ rows = $rows; crop = (Crop-Cells 'A1' 'B8' 'set-x4') }
    })
}
Def '2' 'number-format-colour' 'A1 = -5, B1 = 5; format 0;[Red]-0; Font.Color blue on both' 'The colour of each' 'A1 red (the format wins), B1 blue' {
    Put 'A1' '-5'; Put 'B1' '5'; Set-NF $script:Ws.Range('A1:B1') '0;[Red]-0'
    Set-ComProperty $script:Ws.Range('A1:B1').Font 'Color' $Blue
    [void](State 'set' 'COM set-up' { @{ a1 = (Ink-Of $script:Img 'A1'); b1 = (Ink-Of $script:Img 'B1'); font = (Read-Font 'A1', 'B1'); crop = (Crop-Cells 'A1' 'B1' 'set-x4') } })
}
# Pass a set [Color10]0 through NumberFormat only, and Excel refused it. Pass b (added) tries the
# spellings below, one per row, through NumberFormat (en-US) and NumberFormatLocal (the user's).
$script:Spellings3 = @(@('NumberFormat', '[Color10]0'), @('NumberFormat', '[COLOR10]0'), @('NumberFormat', '[Colour10]0'), @('NumberFormat', '[Color 10]0'),
    @('NumberFormatLocal', '[Color10]0'), @('NumberFormatLocal', '[Colour10]0'), @('NumberFormat', '0;[Color10]-0'), @('NumberFormat', '[Color3]0'))
Def '3' 'number-format-colour' 'A1 = 5; format [Color10]0 (pass b, added: A1:A8 = 5, each set one spelling, through NumberFormat or NumberFormatLocal)' 'The colour. For a later step; nothing is built on it now' '-' {
    if ($Pass -ne 'b') {
        Put 'A1' '5'
        $err = $null
        try { Set-NF $script:Ws.Range('A1') '[Color10]0' } catch { $err = $_.Exception.InnerException.Message; if (-not $err) { $err = $_.Exception.Message } }
        [void](State 'set' 'COM set-up' { @{ setError = $err; a1 = (Ink-Of $script:Img 'A1'); font = (Read-Font 'A1'); crop = (Crop-Cells 'A1' 'A1' 'set-x4') } })
        return
    }
    $tries = @()
    for ($i = 0; $i -lt $script:Spellings3.Count; $i++) {
        $a = "A$($i + 1)"; Put $a '-5'
        $how, $code = $script:Spellings3[$i]; $err = $null
        try { if ($how -eq 'NumberFormat') { Set-NF $script:Ws.Range($a) $code } else { $script:Ws.Range($a).NumberFormatLocal = $code } }
        catch { $ex = $_.Exception; while ($ex.InnerException) { $ex = $ex.InnerException }; $err = $ex.Message }
        $tries += [ordered]@{ cell = $a; through = $how; code = $code; error = $err }
    }
    $script:Tries3 = $tries
    [void](State 'set' 'COM set-up' {
        $o = @(); foreach ($t in $script:Tries3) { $o += [ordered]@{ try = $t; read = (Read-Font $t.cell)[$t.cell]; ink = (Ink-Of $script:Img $t.cell) } }
        @{ tries = $o; crop = (Crop-Cells 'A1' 'B8' 'set-x4') }
    })
}
Def '3b' 'number-format-colour' 'Format [Red]0 on A1 = abc, A2 = TRUE, A3 = =1/0; and 0;[Red]@ on A4 = 5' 'Is any of them red?' 'none: no section shows them, so no colour' {
    Put 'A1' 'abc'; Put 'A2' 'TRUE'; Put 'A3' '=1/0'; Put 'A4' '5'
    Set-NF $script:Ws.Range('A1:A3') '[Red]0'; Set-NF $script:Ws.Range('A4') '0;[Red]@'
    [void](State 'set' 'COM set-up' { @{ a1 = (Ink-Of $script:Img 'A1'); a2 = (Ink-Of $script:Img 'A2'); a3 = (Ink-Of $script:Img 'A3'); a4 = (Ink-Of $script:Img 'A4'); font = (Read-Font 'A1', 'A2', 'A3', 'A4'); crop = (Crop-Cells 'A1' 'A4' 'set-x4') } })
}
Def '3c' 'number-format-colour' 'A1 = -123456789 in 0;[Red]-0; column A set to width 15, then narrowed by 0.5 until A1 shows ####' 'The colour of the #' 'red: a #### keeps its section''s colour' {
    Put 'A1' '-123456789'; Set-NF $script:Ws.Range('A1') '0;[Red]-0'
    $col = $script:Ws.Range('A:A')
    Set-ComProperty $col 'ColumnWidth' 15
    [void](State 'fits' 'COM: width 15' { @{ a1 = (Ink-Of $script:Img 'A1'); font = (Read-Font 'A1'); crop = (Crop-Cells 'A1' 'A1' 'fits-x4') } })
    $tried = @(); $w = 15.0
    while ($w -gt 1) {
        $w -= 0.5; Set-ComProperty $col 'ColumnWidth' $w
        $t = [string]$script:Ws.Range('A1').Text; $tried += "$w '$t'"
        if ($t -match '^#+$') { break }
    }
    [void](State 'hashes' "COM: narrowed to $w" { @{ widthsTried = $tried; a1 = (Ink-Of $script:Img 'A1'); font = (Read-Font 'A1'); crop = (Crop-Cells 'A1' 'A1' 'hashes-x4') } })
}

# Group 2: Fills and gridlines. The picture before the Fill gives where the gridlines are.
function Fill-Case([string]$Cells, [int]$Colour, [string[]]$Edges) {
    [void](State 'plain' 'nothing set yet' { @{ crop = (Crop-Cells 'A1' 'D3' 'plain-x4') } })
    Set-Fill $Cells $Colour
    [void](State 'filled' "COM: $Cells filled" {
        $o = [ordered]@{ fill = (Read-Fill 'B2', 'C2', 'A2', 'B1', 'B3'); crop = (Crop-Cells 'A1' 'D3' 'filled-x4') }
        $across = @()
        foreach ($e in $Edges) { $c, $side = $e -split ' '; $across += [ordered]@{ edge = $e; plain = (Across-Edge $script:Pics['plain'] $c $side); filled = (Across-Edge $script:Img $c $side) } }
        $o.across = $across
        $o
    })
}
Def '4' 'fill' 'B2 filled yellow; its neighbours empty' 'Is each of B2''s four edges drawn in the gridline''s colour, or in the Fill''s? 1 px on each side of each edge' 'the Fill covers them' {
    Fill-Case 'B2' $Yellow @('B2 top', 'B2 right', 'B2 bottom', 'B2 left')
}
Def '5' 'fill' 'B2 filled white (Interior.Color = 0xFFFFFF, not No Fill)' 'The same' 'the gridlines around B2 disappear' {
    Fill-Case 'B2' $White @('B2 top', 'B2 right', 'B2 bottom', 'B2 left')
}
Def '6' 'fill' 'B2 and C2 filled yellow' 'The line between B2 and C2' 'none; yellow throughout' {
    Fill-Case 'B2:C2' $Yellow @('B2 right', 'B2 top', 'C2 top', 'C2 right')
}

# Group 3: Borders.
Def '7' 'border' 'B2''s right edge thick red; read C2''s left edge. Then C2''s left edge thin blue; read B2''s right edge' 'Does setting one side write the neighbour''s? Which line is drawn?' 'unknown: record both reads and the pixels' {
    [void](State 'plain' 'nothing set yet' { @{ crop = (Crop-Cells 'A1' 'D3' 'plain-x4') } })
    Set-Edge 'B2' 10 1 4 $Red
    [void](State 'b2-right-red' 'COM: B2 Borders(xlEdgeRight) Continuous, Thick, red' {
        @{ edges = (Read-Edges 'B2', 'C2'); c2LeftRead = (Edge-Of $script:Ws.Range('C2') 7); across = (Across-Edge $script:Img 'B2' 'right'); acrossPlain = (Across-Edge $script:Pics['plain'] 'B2' 'right'); crop = (Crop-Cells 'A1' 'D3' 'b2-right-red-x4') }
    })
    Set-Edge 'C2' 7 1 2 $Blue
    [void](State 'c2-left-blue' 'COM: C2 Borders(xlEdgeLeft) Continuous, Thin, blue' {
        @{ edges = (Read-Edges 'B2', 'C2'); b2RightRead = (Edge-Of $script:Ws.Range('B2') 10); across = (Across-Edge $script:Img 'B2' 'right'); crop = (Crop-Cells 'A1' 'D3' 'c2-left-blue-x4') }
    })
}
Def '8' 'border' 'B2''s bottom edge thick black' 'How many pixels above and below the gridline the line takes; inside B2, inside B3, or across both' 'across both, centred' {
    [void](State 'plain' 'nothing set yet' { @{ across = (Across-Edge $script:Img 'B2' 'bottom' 8); b3 = (Cell-Rect 'B3'); crop = (Crop-Cells 'A1' 'C3' 'plain-x4') } })
    Set-Edge 'B2' 9 1 4 $Black
    [void](State 'set' 'COM: B2 Borders(xlEdgeBottom) Continuous, Thick, black' {
        @{ edges = (Read-Edges 'B2', 'B3'); across = (Across-Edge $script:Img 'B2' 'bottom' 8); b3 = (Cell-Rect 'B3'); crop = (Crop-Cells 'A1' 'C3' 'set-x4')
            gridlineBeside = [ordered]@{ a = (Across-Edge $script:Img 'A2' 'bottom' 8); c = (Across-Edge $script:Img 'C2' 'bottom' 8) } }
    })
}
$script:Styles = @(
    @('hair', 1, 1), @('thin', 1, 2), @('medium', 1, -4138), @('thick', 1, 4), @('double', -4119, 4), @('dotted', -4118, 2), @('dashed', -4115, 2),
    @('dash-dot', 4, 2), @('dash-dot-dot', 5, 2), @('medium dashed', -4115, -4138), @('medium dash-dot', 4, -4138), @('medium dash-dot-dot', 5, -4138), @('slanted dash-dot', 13, -4138))
Def '9' 'border' 'B2:B14 each get a bottom edge in one of the thirteen styles (LineStyle, Weight), Automatic colour' 'A x4 crop of each line, with its pattern read as pixels on and off along 40 px' '- (reference crops for layer 3)' {
    [void](State 'plain' 'nothing set yet' {
        $o = @(); for ($i = 0; $i -lt 13; $i++) { $o += (Line-Pattern $script:Img "B$($i + 2)" 'bottom' 40 3) }
        @{ lines = $o; crop = (Crop-Cells 'A1' 'C15' 'plain-x2' 8 2) }
    })
    for ($i = 0; $i -lt 13; $i++) { $s = $script:Styles[$i]; Set-Edge "B$($i + 2)" 9 $s[1] $s[2] }
    [void](State 'set' 'COM: the thirteen bottom edges' {
        $lines = @()
        for ($i = 0; $i -lt 13; $i++) {
            $a = "B$($i + 2)"; $s = $script:Styles[$i]; $r = Cell-Rect $a
            $band = [ordered]@{ Left = $r.Left - 6; Top = $r.Bottom - 8; Right = $r.Right + 6; Bottom = $r.Bottom + 8 }
            $lines += [ordered]@{ style = $s[0]; cell = $a; asked = "$($s[1]) $($s[2])"; read = (Edge-Of $script:Ws.Range($a) 9)
                pattern = (Line-Pattern $script:Img $a 'bottom'); below = (Cell-Rect "B$($i + 3)"); gridlineInA = (Across-Edge $script:Img "A$($i + 2)" 'bottom' 5).at50
                crop = (Save-Crop $script:LastBmp $band ('{0}-{1}-x4' -f $script:CaseId, ($s[0] -replace ' ', '-')) 4) }
        }
        @{ lines = $lines; crop = (Crop-Cells 'A1' 'C15' 'set-x2' 8 2) }
    })
}
Def '10' 'border' 'B2''s bottom edge thick black; B3 filled yellow' 'Does B3''s Fill cover the part of the line inside B3?' 'no; the line is above the Fill' {
    [void](State 'plain' 'nothing set yet' { @{ across = (Across-Edge $script:Img 'B2' 'bottom' 8); b3 = (Cell-Rect 'B3'); crop = (Crop-Cells 'A1' 'C4' 'plain-x4') } })
    Set-Edge 'B2' 9 1 4 $Black
    Set-Fill 'B3' $Yellow
    [void](State 'set' 'COM: B2 bottom thick black; B3 yellow' {
        @{ edges = (Read-Edges 'B2', 'B3'); fill = (Read-Fill 'B3'); across = (Across-Edge $script:Img 'B2' 'bottom' 8); b3 = (Cell-Rect 'B3'); crop = (Crop-Cells 'A1' 'C4' 'set-x4')
            gridlineBeside = [ordered]@{ a = (Across-Edge $script:Img 'A2' 'bottom' 8); c = (Across-Edge $script:Img 'C2' 'bottom' 8) } }
    })
}
$script:Grid11 = @('B2 top', 'B2 left', 'B2 right', 'B2 bottom', 'C3 bottom', 'C3 right')
function Across-All([string]$State, [string[]]$Edges) {
    $o = @(); foreach ($e in $Edges) { $c, $side = $e -split ' '; $o += (Across-Edge $script:Pics[$State] $c $side) }; return $o
}
Def '11' 'border' 'B2:C3 with every edge thin black (Borders 7-12). Then select B2:C3 with real keys from A1: Down, Right, Shift+Right, Shift+Down' 'The screenshot: the Focus and the Selection over the borders' 'the Selection is drawn above' {
    [void](State 'plain' 'nothing set yet' { @{ across = (Across-All 'plain' $script:Grid11); crop = (Crop-Cells 'A1' 'D4' 'plain-x4') } })
    foreach ($i in 7, 8, 9, 10, 11, 12) { $b = $script:Ws.Range('B2:C3').Borders.Item($i); Set-ComProperty $b 'LineStyle' 1; Set-ComProperty $b 'Weight' 2; Set-ComProperty $b 'Color' 0 }
    [void](State 'bordered' 'COM: B2:C3 every edge thin black' { @{ edges = (Read-Edges 'B2:C3'); across = (Across-All 'bordered' $script:Grid11); crop = (Crop-Cells 'A1' 'D4' 'bordered-x4') } })
    $k = Keys '{DOWN}{RIGHT}+{RIGHT}+{DOWN}'
    [void](State 'selected' $k { @{ selection = (Selection-Now); across = (Across-All 'selected' $script:Grid11); crop = (Crop-Cells 'A1' 'D4' 'selected-x4') } })
}
Def '12' 'border' 'B2 filled yellow, its top edge thin and its bottom edge thick (black). Select row 3 with real keys (A3, Shift+Space) and insert (Ctrl+Shift+=)' 'Row 3''s cells: Fill and four edges. Row 4''s (the old row 3) top edge' 'Row 3 takes B2''s Fill; the borders unknown: record' {
    Set-Fill 'B2' $Yellow; Set-Edge 'B2' 8 1 2 $Black; Set-Edge 'B2' 9 1 4 $Black
    [void](State 'set' 'COM set-up' { @{ edges = (Read-Edges 'A2:C3'); fill = (Read-Fill 'A2', 'B2', 'C2', 'B3'); crop = (Crop-Cells 'A1' 'D5' 'set-x4') } })
    $k = Keys '{DOWN}{DOWN}'
    $k2 = Keys '+{SPACE}'
    [void](State 'row3-selected' "$k; $k2" { @{ selection = (Selection-Now) } })
    $k3 = Keys '^+{=}' 1200
    [void](State 'inserted' $k3 {
        @{ selection = (Selection-Now); edges = (Read-Edges 'A2:D4'); fill = (Read-Fill 'A2', 'B2', 'C2', 'A3', 'B3', 'C3', 'D3', 'B4')
            row3Level = [ordered]@{ fill = ('pattern {0} color {1}' -f $script:Ws.Rows(3).Interior.Pattern, (Hex-Of $script:Ws.Rows(3).Interior.Color)); top = (Edge-Of $script:Ws.Rows(3) 8); bottom = (Edge-Of $script:Ws.Rows(3) 9) }
            b4Top = (Edge-Of $script:Ws.Range('B4') 8); usedRange = [string]$script:Ws.UsedRange.Address($false, $false); crop = (Crop-Cells 'A1' 'D5' 'inserted-x4') }
    })
}
$script:Around13 = @('B2:D4', 'A2:A4', 'E2:E4', 'B1:D1', 'B5:D5')
Def '13' 'border' 'Select B2:D4 with real keys from A1 (Down, Right, Shift+Right x2, Shift+Down x2); Ctrl+Shift+& then Ctrl+Shift+_ (as characters on the UK layout)' 'After each key: every edge of B2:D4, and of A2:A4, E2:E4, B1:D1 and B5:D5' '&: the outer edges of the range only; _: every edge of the range cleared. The neighbours: record' {
    $k = Keys '{DOWN}{RIGHT}+{RIGHT}+{RIGHT}+{DOWN}+{DOWN}'
    [void](State 'selected' $k { @{ selection = (Selection-Now) } })
    $k = Ctrl-Char '&'
    [void](State 'amp' $k { $e = Read-Edges $script:Around13; @{ selection = (Selection-Now); edges = $e; lines = (Lines-In $e); crop = (Crop-Cells 'A1' 'F6' 'amp-x2' 8 2) } })
    $k = Ctrl-Char '_'
    [void](State 'underscore' $k { $e = Read-Edges $script:Around13; @{ selection = (Selection-Now); edges = $e; lines = (Lines-In $e); crop = (Crop-Cells 'A1' 'F6' 'underscore-x2' 8 2) } })
}
Def '14' 'border' 'Select B2:C3 with real keys from A1 (Down, Right, Shift+Right, Shift+Down), then E5:F6 by a drag with Ctrl held; Ctrl+Shift+&' 'Does each range get its own outline?' 'yes' {
    $k = Keys '{DOWN}{RIGHT}+{RIGHT}+{DOWN}'
    $d = Drag-Cells 'E5' 'F6' -Ctrl
    [void](State 'selected' "$k; $d" { @{ selection = (Selection-Now) } })
    $k = Ctrl-Char '&'
    [void](State 'amp' $k {
        $e = Read-Edges 'B2:C3', 'E5:F6', 'D2:D3', 'B4:C4', 'B1:C1', 'A2:A3', 'E4:F4', 'D5:D6', 'G5:G6', 'E7:F7', 'D4'
        @{ selection = (Selection-Now); edges = $e; lines = (Lines-In $e); crop = (Crop-Cells 'A1' 'G7' 'amp-x2' 8 2) }
    })
}
Def '15' 'border' 'Select column B with real keys (Right to B1, Ctrl+Space); Ctrl+Shift+&' 'B1''s top edge, B5''s left and right edges, B1048576''s bottom edge; and Columns(2).Borders' 'recorded at column level' {
    $k = Keys '{RIGHT}^{SPACE}'
    [void](State 'selected' $k { @{ selection = (Selection-Now) } })
    $k = Ctrl-Char '&'
    [void](State 'amp' $k {
        $col = [ordered]@{}; $idx = [ordered]@{ left = 7; top = 8; bottom = 9; right = 10; insideHorizontal = 12 }
        foreach ($n in $idx.Keys) { $col[$n] = Edge-Of $script:Ws.Columns(2) $idx[$n] }
        $e = Read-Edges 'B1', 'B2', 'B5', 'A5', 'C5', 'B1048575', 'B1048576'
        @{ selection = (Selection-Now); edges = $e; lines = (Lines-In $e); columns2 = $col; crop = (Crop-Cells 'A1' 'C6' 'amp-x2' 8 2) }
    })
    $script:Book.Windows.Item(1).ScrollRow = 1048560
    $script:RectCache = @{}
    [void](State 'bottom' 'COM: the window scrolled to row 1048560, to picture the last row' {
        @{ across = (Across-Edge $script:Img 'B1048576' 'bottom'); crop = (Crop-Cells 'A1048570' 'C1048576' 'bottom-x2' 8 2) }
    })
}

# Group 4: keys. A1 = abc and A2 = def, with no formatting, before each case.
function Abc { Put 'A1' 'abc'; Put 'A2' 'def' }
Def '16' 'keys' 'A1 = abc, A2 = def. On A1, each of Ctrl+B, Ctrl+2, Ctrl+I, Ctrl+3, Ctrl+U, Ctrl+4 and Ctrl+5, twice; A1''s formats cleared (COM ClearFormats) before each key''s pair' 'What each sets, and whether the second press takes it off' 'each toggles; underline is single' {
    Abc
    foreach ($key in '^b', '^2', '^i', '^3', '^u', '^4', '^5') {
        [void]$script:Ws.Range('A1').ClearFormats(); Select-Cell 'A1'
        $kn = $key -replace '\^', 'ctrl-'
        $k = Keys $key
        [void](State "$kn-1" $k { @{ font = (Read-Font 'A1'); crop = (Crop-Cells 'A1' 'A1' "$kn-1-x4") } })
        $k = Keys $key
        [void](State "$kn-2" $k { @{ font = (Read-Font 'A1'); crop = (Crop-Cells 'A1' 'A1' "$kn-2-x4") } })
    }
}
Def '17' 'keys' 'A1 = abc, A2 = def; A1 bold (COM). Select A1:A2 with the Focus on A1 (A1, Shift+Down), Ctrl+B. Then reset (A1 bold, A2 plain), and select A2:A1 with the Focus on A2 (A2, Shift+Up), Ctrl+B' 'Both cells'' bold after each' 'the Focus decides: first both plain, then both bold' {
    Abc
    Set-ComProperty $script:Ws.Range('A1').Font 'Bold' $true; Select-Cell 'A1'
    $k = Keys '+{DOWN}'
    [void](State 'focus-a1' $k { @{ selection = (Selection-Now); font = (Read-Font 'A1', 'A2') } })
    $k = Keys '^b'
    [void](State 'focus-a1-ctrl-b' $k { @{ selection = (Selection-Now); font = (Read-Font 'A1', 'A2'); crop = (Crop-Cells 'A1' 'A2' 'focus-a1-ctrl-b-x4') } })
    [void]$script:Ws.Range('A1:A2').ClearFormats(); Set-ComProperty $script:Ws.Range('A1').Font 'Bold' $true; Select-Cell 'A2'
    $k = Keys '+{UP}'
    [void](State 'focus-a2' $k { @{ selection = (Selection-Now); font = (Read-Font 'A1', 'A2') } })
    $k = Keys '^b'
    [void](State 'focus-a2-ctrl-b' $k { @{ selection = (Selection-Now); font = (Read-Font 'A1', 'A2'); crop = (Crop-Cells 'A1' 'A2' 'focus-a2-ctrl-b-x4') } })
}
# Before each key A1's Number Format is set to 0.000 and its column to the standard width (COM), so a
# key that changes nothing shows 0.000.
function Sentinel {
    Set-NF $script:Ws.Range('A1') '0.000'; Set-ComProperty $script:Ws.Range('A:A') 'ColumnWidth' ([double]$script:Ws.StandardWidth); Select-Cell 'A1'
    foreach ($a in 'A1', 'B1', 'B2') { [void](Cell-Rect $a) }
}
# A key's state. A key may open an edit (Ctrl+Shift+" copies the value above and edits) or a dialog:
# then COM is refused or can wait, so the state is read from the picture, the Formula Bar and the
# dialog; Esc follows, and the cell is read again once Excel is Ready.
function Any-Dialog { foreach ($w in [CellFormat.Win]::Of($script:OwnPid)) { if ($w.class -in 'bosa_sdm_XL9', '#32770', 'NUIDialog') { return $w } }; return $null }
function Key-State([string]$Name, [string]$Did, [string]$Char, [string[]]$EdgesOf = @()) {
    Start-Sleep -Milliseconds 300
    $script:Dialog19 = Any-Dialog; $busy = $false
    if (-not $script:Dialog19) { try { [void]$script:Xl.ActiveCell.Address($false, $false) } catch { $busy = $true } }
    if (-not $script:Dialog19 -and -not $busy) {
        [void](State $Name $Did { $o = @{ char = $Char; font = (Read-Font 'A1'); crop = (Crop-Cells 'A1' 'B1' "$Name-x2" 8 2) }; if ($EdgesOf) { $o.edges = (Read-Edges $EdgesOf) }; $o })
        return
    }
    $script:NoCom = $true
    [void](State $Name $Did {
        @{ char = $Char; excelBusy = $(if ($script:Dialog19) { "a dialog: $($script:Dialog19.class) [$($script:Dialog19.title)]" } else { 'COM refused the call: an edit is open' })
            bar = [CellFormat.Ui]::Bar([long]$script:Hwnd, 4000); dialog = $(if ($script:Dialog19) { Dialog-State 4 } else { $null }); crop = (Crop-Cells 'A1' 'B2' "$Name-x2" 8 2) }
    })
    Send-KeysRaw '{ESC}' 600
    $e = Wait-NoDialog
    $script:NoCom = $false
    $n = Until-Ready
    [void](State "$Name-esc" "{ESC} ($e more for a dialog, $n more for Excel)" { $o = @{ font = (Read-Font 'A1'); crop = (Crop-Cells 'A1' 'B2' "$Name-esc-x2" 8 2) }; if ($EdgesOf) { $o.edges = (Read-Edges $EdgesOf) }; $o })
}
$script:Chars18 = [ordered]@{ '~' = 'tilde'; '!' = 'bang'; '@' = 'at'; '#' = 'hash'; '$' = 'dollar'; '%' = 'percent'; '^' = 'caret' }
function Keys-18([string]$Prefix) {
    foreach ($c in $script:Chars18.Keys) {
        Sentinel
        $k = Ctrl-Char $c
        Key-State ("$Prefix-char-" + $script:Chars18[$c]) $k $c
    }
}
function Keys-19([string]$Prefix) {
    $us = [ordered]@{ '~' = @(0xC0, 'OEM_3'); '!' = @(0x31, '1'); '@' = @(0x32, '2'); '#' = @(0x33, '3'); '$' = @(0x34, '4'); '%' = @(0x35, '5'); '^' = @(0x36, '6') }
    foreach ($c in $us.Keys) {
        Sentinel
        $k = Ctrl-Shift-Vk $us[$c][0] $us[$c][1]
        Key-State ("$Prefix-vk-" + $script:Chars18[$c]) $k $c
    }
    # & and _ both ways, on A1: as the characters on this layout, and as their US positions (7, OEM_MINUS).
    Sentinel
    foreach ($way in 'char', 'vk') {
        foreach ($c in '&', '_') {
            $k = if ($way -eq 'char') { Ctrl-Char $c } elseif ($c -eq '&') { Ctrl-Shift-Vk 0x37 '7' } else { Ctrl-Shift-Vk 0xBD 'OEM_MINUS' }
            Key-State ("$Prefix-$way-" + $(if ($c -eq '&') { 'amp' } else { 'underscore' })) $k $c @('A1')
        }
    }
}
Def '18' 'keys' 'A1 = 1234.5 (A2 = def). On A1, Ctrl with each of ~ ! @ # $ % ^ as the character on the UK layout (with the Shift the layout needs); A1 set to 0.000 and column A to its standard width before each key (COM)' 'NumberFormat and NumberFormatLocal after each, and the text shown' 'General, #,##0.00, a time, a date, a currency, 0%, 0.00E+00' {
    Put 'A1' '1234.5'; Put 'A2' 'def'
    Keys-18 'uk'
}
Def '19' 'keys' 'As 18, but Ctrl+Shift with the US position of each character (VK OEM_3, 1, 2, 3, 4, 5, 6) on the UK layout; then Ctrl+Shift+& and _ on A1 both ways (as the characters; as VK 7 and OEM_MINUS)' 'Which of the two ways Excel answers, on each layout' 'unknown: record' {
    Put 'A1' '1234.5'; Put 'A2' 'def'
    Keys-19 'uk'
}
Def '18j' 'keys' 'Case 18 with Excel''s window on the Japanese layout (IME off): Ctrl with each character as the Japanese layout types it' 'As 18, on the Japanese layout' 'unknown: record' {
    Put 'A1' '1234.5'; Put 'A2' 'def'
    Keys-18 'ja'
} 'ja'
Def '19j' 'keys' 'Case 19 with Excel''s window on the Japanese layout (IME off)' 'As 19, on the Japanese layout' 'unknown: record' {
    Put 'A1' '1234.5'; Put 'A2' 'def'
    Keys-19 'ja'
} 'ja'

# Case 20: case 18 under other regional formats. Set-Culture writes the user's regional format; a new
# Excel reads it as it starts. The format the run began with is set back at the end, and the registry
# key compared with the export taken before the run.
function Read-International {
    $x = $script:Xl
    return [ordered]@{ countryCode = $x.International(1); countrySetting = $x.International(2); decimalSeparator = $x.International(3); thousandsSeparator = $x.International(4)
        listSeparator = $x.International(5); dateOrder = $x.International(32); currencyCode = $x.International(25); dateSeparator = $x.International(17) }
}
Def '20' 'keys' 'Case 18 under the regional formats en-US and ja-JP: Set-Culture, then a new Excel; the culture the run began with set back afterwards' 'The formats each key applies under each' 'unknown: record' {
    $intlKey = 'HKCU:\Control Panel\International'
    $orig = [string](Get-ItemProperty $intlKey).LocaleName
    $runDir = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\run-2026-10-01-11'
    try {
        foreach ($cul in 'en-US', 'ja-JP') {
            Set-Culture -CultureInfo $cul
            Start-Sleep -Seconds 2
            $script:Keyboard20 = Set-Up 'uk'
            Set-CaseDue 240
            $script:Geo = Get-Geometry
            $child = (& powershell.exe -NoProfile -Command '(Get-Culture).Name' | Out-String).Trim()
            [void](State "$cul-start" "Set-Culture $cul; a new Excel (process $($script:OwnPid))" {
                @{ culture = $cul; registryLocaleName = [string](Get-ItemProperty $intlKey).LocaleName; cultureInANewPowerShell = $child; international = (Read-International); keyboard = $script:Keyboard20 }
            })
            Put 'A1' '1234.5'; Put 'A2' 'def'
            Keys-18 $cul
        }
    }
    finally {
        if ($script:Xl) { Stop-OwnExcel }
        Set-Culture -CultureInfo $orig
        Start-Sleep -Seconds 2
        $after = Join-Path $runDir 'international-after-20.reg'
        & reg.exe export 'HKCU\Control Panel\International' $after /y | Out-Null
        $b = @(Get-Content (Join-Path $runDir 'international-before.reg')); $a = @(Get-Content $after)
        $diff = @(Compare-Object $b $a | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" })
        $script:Restored20 = [ordered]@{ setBackTo = $orig; differencesFromTheExportBefore = $diff }
        Say "  20: culture set back to $orig; differences from the export before: $($diff.Count)"
    }
    [void](State 'restored' "Set-Culture $orig" { @{ restored = $script:Restored20 } })
}

Def '21' 'keys' 'A1 = abc, A2 = def. F2 on A1; select b (Left, Shift+Left); Ctrl+B; Ctrl+Shift+$ (as the character on the UK layout); Ctrl+1, Esc to close it; Esc to cancel the edit' 'What each key does while the edit is open' 'Ctrl+B bolds b alone; $: record; Ctrl+1 opens a Font-only dialog' {
    Abc; Select-Cell 'A1'
    foreach ($a in 'A1', 'B1', 'A2') { [void](Cell-Rect $a) }
    $script:NoCom = $true
    $bar = { @{ bar = [CellFormat.Ui]::Bar([long]$script:Hwnd, 4000); focused = [CellFormat.Ui]::Focused(3000) } }
    try {
        $k = Keys '{F2}'
        [void](State 'f2' $k { (& $bar) + @{ crop = (Crop-Cells 'A1' 'B1' 'f2-x4') } })
        $k = Keys '{LEFT}+{LEFT}'
        [void](State 'b-selected' $k { (& $bar) + @{ crop = (Crop-Cells 'A1' 'B1' 'b-selected-x4') } })
        $k = Keys '^b'
        [void](State 'ctrl-b' $k { (& $bar) + @{ crop = (Crop-Cells 'A1' 'B1' 'ctrl-b-x4') } })
        $k = Ctrl-Char '$'
        [void](State 'ctrl-shift-dollar' $k { (& $bar) + @{ crop = (Crop-Cells 'A1' 'B1' 'ctrl-shift-dollar-x4') } })
        $k = Keys '^1' 1500
        [void](State 'ctrl-1' $k { @{ focused = [CellFormat.Ui]::Focused(3000); dialog = (Dialog-State) } })
        $k = Keys '{ESC}' 800
        [void](State 'dialog-closed' $k { (& $bar) + @{ crop = (Crop-Cells 'A1' 'B1' 'dialog-closed-x4') } })
        $k = Keys '{ESC}' 800
        [void](State 'edit-cancelled' $k { (& $bar) + @{ crop = (Crop-Cells 'A1' 'B1' 'edit-cancelled-x4') } })
    }
    finally { $script:NoCom = $false }
    $n = Until-Ready
    [void](State 'after' "COM reads ($n further Escapes)" { @{ font = (Read-Font 'A1'); characterB = [ordered]@{ bold = $script:Ws.Range('A1').Characters(2, 1).Font.Bold } } })
}

# Group 5: Format Cells and the palette. While a dialog of Excel's is open COM is not called
# ($script:NoCom): a COM call made while one is up can wait as long as it stays (2026-09-27).
function Window-Of([string]$Class) { foreach ($w in [CellFormat.Win]::Of($script:OwnPid)) { if ($w.class -eq $Class) { return $w } }; return $null }
function Dialog-Items([int]$Depth = 8) {
    $w = Window-Of 'bosa_sdm_XL9'
    if (-not $w) { return @() }
    $t = [CellFormat.Ui]::Tree([long]$w.hwnd, $Depth, 800, 30000)
    return @(G $t 'items')
}
function Item-Named($Items, [string]$Pattern, [string]$Type = '') {
    foreach ($i in $Items) { if ([string]$i['name'] -match $Pattern -and (-not $Type -or $i['type'] -eq $Type)) { return $i } }
    return $null
}
function Click-Item($Item) {
    if (-not $Item -or -not $Item.ContainsKey('rect')) { throw "nothing to click: $($Item | ConvertTo-Json -Compress)" }
    $r = $Item['rect']
    return "a click on $($Item['type']) [$($Item['name'])] " + (Hand-Click ([int](($r[0] + $r[2]) / 2)) ([int](($r[1] + $r[3]) / 2)))
}
# What the dialog shows, one line per element: its type, name, and value, toggle or selection (the
# title bar, scroll bars and images left out).
function Dialog-Summary($Items) {
    $o = @()
    foreach ($i in $Items) {
        if ($i['type'] -in 'TitleBar', 'MenuBar', 'Image' -or $i['class'] -eq 'ScrollBar' -or $i['name'] -in 'System', 'Context help', 'Close', 'System Menu Bar') { continue }
        $line = '{0} [{1}]' -f $i['type'], $i['name']
        if ($i.ContainsKey('value')) { $line += " value=[$($i['value'])]" }
        if ($i.ContainsKey('toggle')) { $line += " toggle=$($i['toggle'])" }
        if ($i.ContainsKey('selected')) { $line += " selected=$($i['selected'])" }
        if ($i.ContainsKey('enabled')) { $line += ' disabled' }
        if ($i.ContainsKey('children')) { $line += " ($($i['children']))" }
        $o += $line
    }
    return $o
}
function Selected-Tab($Items) { $t = Item-Named $Items '^Format Cells$' 'Tab'; if ($t) { return $t['value'] }; return $null }
function Tab-Names($Items) { return @($Items | Where-Object { $_['type'] -eq 'TabItem' } | ForEach-Object { [string]$_['name'] } | Select-Object -Unique) }
# The dialog's state: the tab shown, the tabs in order, and every element.
function Dialog-State([int]$Depth = 8) {
    $items = Dialog-Items $Depth
    return [ordered]@{ open = [bool]$items.Count; selectedTab = (Selected-Tab $items); tabs = (Tab-Names $items); elements = (Dialog-Summary $items) }
}
function Open-FormatCells([string]$Name) {
    $k = Keys '^1' 1500
    $script:NoCom = $true
    return $k
}
# COM is called again only once no dialog of Excel's is showing.
function Wait-NoDialog {
    for ($i = 0; $i -lt 4; $i++) {
        if (-not (Window-Of 'bosa_sdm_XL9')) { return $i }
        if ($i -gt 0) { Send-KeysRaw '{ESC}' 700 } else { Start-Sleep -Milliseconds 700 }
    }
    if (Window-Of 'bosa_sdm_XL9') { throw 'the dialog did not close' }
    return 4
}
function Close-Dialog([string]$Keys = '{ESC}') {
    $k = Keys $Keys 900
    $e = Wait-NoDialog
    $script:NoCom = $false
    $n = Until-Ready
    return "$k ($e further Escapes for the dialog, $n for Excel)"
}
function Visit-Tab([string]$Tab) {
    $items = Dialog-Items 2
    $d = Click-Item (Item-Named $items "^$Tab$" 'TabItem')
    Start-Sleep -Milliseconds 600
    [void](State "tab-$($Tab.ToLowerInvariant())" $d { @{ dialog = (Dialog-State) } })
}

Def '22' 'format-cells' 'A fresh Excel; Ctrl+1 on A1. Each tab visited by a click on it, in the order shown; Esc; Ctrl+1 again; Esc' 'A screenshot of each tab. The Number tab''s category names in order. The Border tab''s line styles in order, and its presets. The order of the tabs. Which tab opens first on a fresh Excel, and on the second Ctrl+1' 'Number, Alignment, Font, Border, Fill, Protection' {
    Select-Cell 'A1'
    $k = Open-FormatCells
    $first = $null
    [void](State 'opened' $k { $script:First22 = Dialog-State; @{ dialog = $script:First22 } })
    foreach ($t in @($script:First22.tabs)) { Visit-Tab $t }
    $k = Close-Dialog
    [void](State 'closed' $k { $null })
    $k = Open-FormatCells
    [void](State 'second' $k { @{ dialog = (Dialog-State) } })
    $k = Close-Dialog
    [void](State 'second-closed' $k { $null })
} 'uk' 300
Def '24' 'format-cells' 'A1 bold with a red Fill and a thick bottom edge (black); A2 plain (both empty). Select A1:A2 with the Focus on A1 (A1, Shift+Down), Ctrl+1; the Font, Fill and Border tabs visited by clicks; Esc' 'How the Font, Fill and Border tabs show the parts that differ' 'greyed or empty: record' {
    Set-ComProperty $script:Ws.Range('A1').Font 'Bold' $true; Set-Fill 'A1' $Red; Set-Edge 'A1' 9 1 4 $Black
    Select-Cell 'A1'
    $k = Keys '+{DOWN}'
    [void](State 'selected' $k { @{ selection = (Selection-Now); font = (Read-Font 'A1', 'A2'); fill = (Read-Fill 'A1', 'A2'); edges = (Read-Edges 'A1:A2') } })
    $k = Open-FormatCells
    [void](State 'opened' $k { @{ dialog = (Dialog-State) } })
    foreach ($t in 'Font', 'Fill', 'Border') { Visit-Tab $t }
    $k = Close-Dialog
    [void](State 'closed' $k { @{ font = (Read-Font 'A1', 'A2'); fill = (Read-Fill 'A1', 'A2'); edges = (Read-Edges 'A1:A2') } })
} 'uk' 300
# The colour picker of Format Cells: every swatch's name and place through UI Automation, and its
# colour sampled from the picture (the mode of 11 x 11 px at its middle).
function Picker-Swatches($Img) {
    $w = Window-Of 'Net UI Tool Window'
    if (-not $w) { return 'no picker window' }
    $t = [CellFormat.Ui]::Tree([long]$w.hwnd, 8, 300, 20000)
    $o = @()
    foreach ($i in @(G $t 'items')) {
        if ($i['type'] -ne 'ListItem' -or -not $i.ContainsKey('rect')) { continue }
        $r = $i['rect']; $cx = [int](($r[0] + $r[2]) / 2); $cy = [int](($r[1] + $r[3]) / 2)
        $o += [ordered]@{ name = $i['name']; selected = $i['selected']; rect = $r; sampled = (Swatch-Colour $Img $cx $cy) }
    }
    return $o
}
function Swatch-Colour($Img, [int]$Cx, [int]$Cy) {
    $x0 = (IX $Cx) - 5; $y0 = (IY $Cy) - 5
    $m = [CellFormat.Px]::Mode($Img, $x0, $y0, $x0 + 11, $y0 + 11)
    $n = 0; for ($y = $y0; $y -lt $y0 + 11; $y++) { for ($x = $x0; $x -lt $x0 + 11; $x++) { if ($Img.At($x, $y) -eq $m) { $n++ } } }
    return ('#{0} ({1}/121 px)' -f ([CellFormat.Px]::Hex($m)).ToUpperInvariant(), $n)
}
Def '25' 'format-cells' 'A1 bold, A2 italic (both empty). Select A1:A2 (A1, Shift+Down), Ctrl+1, the Font tab by a click, its Colour box by a click, Red (Standard Colours) by a click, OK by a click' 'A1''s and A2''s bold, italic and colour' 'only the colour changed' {
    Set-ComProperty $script:Ws.Range('A1').Font 'Bold' $true; Set-ComProperty $script:Ws.Range('A2').Font 'Italic' $true
    Select-Cell 'A1'
    $k = Keys '+{DOWN}'
    [void](State 'selected' $k { @{ selection = (Selection-Now); font = (Read-Font 'A1', 'A2') } })
    $k = Open-FormatCells
    [void](State 'opened' $k { @{ dialog = (Dialog-State 2) } })
    Visit-Tab 'Font'
    $d = Click-Item (Item-Named (Dialog-Items) '^Colou?r' 'Button')
    Start-Sleep -Milliseconds 1200
    [void](State 'picker' $d { @{ swatches = (Picker-Swatches $script:Img) } })
    $w = Window-Of 'Net UI Tool Window'
    $red = Item-Named @(G ([CellFormat.Ui]::Tree([long]$w.hwnd, 8, 300, 20000)) 'items') '^Red$' 'ListItem'
    $d = Click-Item $red
    Start-Sleep -Milliseconds 600
    [void](State 'red-chosen' $d { @{ dialog = (Dialog-State) } })
    $d = Click-Item (Item-Named (Dialog-Items 2) '^OK$' 'Button')
    Start-Sleep -Milliseconds 800
    $e = Wait-NoDialog
    if ($e) { $d += " (the dialog was still open: $e Escapes)" }
    $script:NoCom = $false
    $n = Until-Ready
    [void](State 'after-ok' "$d ($n Escapes needed)" { @{ font = (Read-Font 'A1', 'A2'); fill = (Read-Fill 'A1', 'A2'); crop = (Crop-Cells 'A1' 'A2' 'after-ok-x4') } })
} 'uk' 300
Def '26' 'format-cells' 'Ctrl+1 on A1; then Ctrl+Tab; then Ctrl+PageDown; Esc' 'Which tab each moves to' 'the next tab' {
    Select-Cell 'A1'
    $k = Open-FormatCells
    [void](State 'opened' $k { @{ dialog = (Dialog-State 2) } })
    $k = Keys '^{TAB}' 800
    [void](State 'ctrl-tab' $k { @{ dialog = (Dialog-State 2) } })
    $k = Keys '^{PGDN}' 800
    [void](State 'ctrl-pgdn' $k { @{ dialog = (Dialog-State 2) } })
    $k = Close-Dialog
    [void](State 'closed' $k { $null })
} 'uk' 240

# Case 23: the Font Colour list on the Home ribbon. Its swatches are not in its UI Automation tree (the
# Format Cells picker's are), so each is found at its place in the list (measured on this machine's
# list: columns 42 px apart from 23 px in, the theme row at 175 px down, the five tints from 230 px in
# steps of 36, the standard row at 476, Automatic at (28, 86)), its colour sampled from the picture, and
# its name read from the tooltip that hovering it shows.
function Ribbon-Item([string]$Pattern, [string]$Type) {
    $r = [CellFormat.Ui]::FindAll([long]$script:Hwnd, $Pattern, $Type, 1, 20000, 20000)
    $i = @(G $r 'items'); if ($i.Count) { return $i[0] }; return $null
}
# The tooltip showing now; with -At, only one placed where a tooltip for a pointer at that point goes
# (its left edge at the pointer's x, its top 30 px below the pointer, as measured on this machine).
function Tooltip-Now([int[]]$At = $null) {
    foreach ($w in [CellFormat.Win]::Of($script:OwnPid)) {
        if ($w.class -ne 'Net UI Tool Window') { continue }
        if ($At -and ([Math]::Abs($w.rect[0] - $At[0]) -gt 8 -or [Math]::Abs($w.rect[1] - ($At[1] + 30)) -gt 12)) { continue }
        $t = [CellFormat.Ui]::Tree([long]$w.hwnd, 4, 30, 3000)
        $tip = @(@(G $t 'items') | Where-Object { $_['type'] -eq 'ToolTip' })
        if ($tip.Count) { return [ordered]@{ name = $tip[0]['name']; texts = @(@(G $t 'items') | Where-Object { $_['type'] -eq 'Text' } | ForEach-Object { $_['name'] }); rect = $w.rect } }
    }
    return $null
}
Def '23' 'palette' 'A1 selected. The Font Colour list on the Home ribbon opened with a click on its arrow; every swatch hovered in turn; Esc. Then Page Layout > Colours opened with clicks; Esc' 'Every swatch''s hex (theme colours 10 x 6, standard colours 10) with its name from the tooltip, and the theme''s name (Page Layout > Colours)' 'the current Office theme' {
    Select-Cell 'A1'
    $split = Ribbon-Item '^Font Colou?r$' 'SplitButton'
    if (-not $split) { throw 'the Font Colour button was not found' }
    $r = $split['rect']
    $d = 'a click on the arrow of ' + $split['name'] + ' ' + (Hand-Click ($r[2] - 11) ([int](($r[1] + $r[3]) / 2)))
    $script:NoCom = $true
    Start-Sleep -Milliseconds 1500
    $menu = Window-Of 'Net UI Tool Window'
    foreach ($w in [CellFormat.Win]::Of($script:OwnPid)) { if ($w.class -eq 'Net UI Tool Window' -and ($w.rect[3] - $w.rect[1]) -gt 400) { $menu = $w } }
    $script:Menu23 = $menu
    $points = @([ordered]@{ row = 'Automatic'; col = 0; x = 28; y = 86 })
    $rows = [ordered]@{ 'theme' = 175; 'tint 1' = 230; 'tint 2' = 266; 'tint 3' = 302; 'tint 4' = 338; 'tint 5' = 374; 'standard' = 476 }
    foreach ($row in $rows.Keys) { for ($c = 0; $c -lt 10; $c++) { $points += [ordered]@{ row = $row; col = $c + 1; x = 23 + 42 * $c; y = $rows[$row] } } }
    [void](State 'palette' $d {
        $o = @()
        foreach ($p in $points) { $o += [ordered]@{ row = $p.row; col = $p.col; at = @(($script:Menu23.rect[0] + $p.x), ($script:Menu23.rect[1] + $p.y)); sampled = (Swatch-Colour $script:Img ($script:Menu23.rect[0] + $p.x) ($script:Menu23.rect[1] + $p.y)) } }
        $script:Swatches23 = $o
        @{ menu = $script:Menu23; swatches = $o; crop = (Save-Crop $script:LastBmp ([ordered]@{ Left = $script:Menu23.rect[0]; Top = $script:Menu23.rect[1]; Right = $script:Menu23.rect[2]; Bottom = $script:Menu23.rect[3] }) "$($script:CaseId)-palette-x2" 2) }
    })
    $named = @()
    foreach ($s in $script:Swatches23) {
        Hand-Move ([int]$s.at[0]) ([int]$s.at[1])
        # Pass a read the tooltip 800 ms after each move and often got none, or the previous swatch's.
        # Here the read waits (up to 3 s) for a tooltip placed at this swatch.
        $sw = [Diagnostics.Stopwatch]::StartNew(); $tip = $null
        while ($sw.ElapsedMilliseconds -lt 3000 -and -not $tip) { Start-Sleep -Milliseconds 150; $tip = Tooltip-Now @([int]$s.at[0], [int]$s.at[1]) }
        $named += [ordered]@{ row = $s.row; col = $s.col; sampled = $s.sampled; tooltip = $(if ($tip) { $tip.name } else { $null }); tooltipTexts = $(if ($tip) { $tip.texts } else { $null }); tooltipAfterMs = $sw.ElapsedMilliseconds }
    }
    [void](State 'hovered' 'each swatch hovered in turn; the tooltip placed at it read as soon as it showed (up to 3 s)' { @{ swatches = $named } })
    $k = Keys '{ESC}' 800
    $script:NoCom = $false
    [void](Until-Ready)
    [void](State 'palette-closed' $k { $null })
    $d = Click-Item (Ribbon-Item '^Page Layout$' 'TabItem')
    Start-Sleep -Milliseconds 1000
    $d2 = Click-Item (Ribbon-Item '^Colou?rs$' 'MenuItem')
    $script:NoCom = $true
    Start-Sleep -Milliseconds 1500
    $g = $null; foreach ($w in [CellFormat.Win]::Of($script:OwnPid)) { if ($w.class -eq 'Net UI Tool Window' -and ($w.rect[3] - $w.rect[1]) -gt 400) { $g = $w } }
    $script:Gallery23 = $g
    [void](State 'theme-colours' "$d; $d2" {
        if (-not $script:Gallery23) { return @{ gallery = 'not found' } }
        $gr = $script:Gallery23.rect
        # The ground down the gallery, 178 px in from its left edge (between each entry's colours and its
        # name): a highlighted entry shows as a run of another ground.
        $x = $gr[0] + 178
        $col = [CellFormat.Px]::Across($script:Img, $true, (IY $gr[1]), (IX $x), 0, $gr[3] - $gr[1] - 1)
        @{ gallery = $script:Gallery23; groundDown = [ordered]@{ x = $x; fromY = $gr[1]; runs = (Runs-Of $col 0) }
            uia = (Dialog-Summary @(G ([CellFormat.Ui]::Tree([long]$script:Gallery23.hwnd, 10, 200, 10000)) 'items'))
            crop = (Save-Crop $script:LastBmp ([ordered]@{ Left = $gr[0]; Top = $gr[1] - 50; Right = $gr[2]; Bottom = $gr[3] }) "$($script:CaseId)-theme-colours") }
    })
    $k = Keys '{ESC}' 800
    $script:NoCom = $false
    [void](Until-Ready)
    [void](Click-Item (Ribbon-Item '^Home$' 'TabItem'))
} 'uk' 400

# ---- Running a case -----------------------------------------------------------------------------

function Run-Case($C) {
    $id = $C.Id
    if ($Zoom -ne 100) { $id += "-z$Zoom" }
    if ($Pass) { $id += "-$Pass" }
    Say "case ${id}: $($C.Setup)"
    $script:CaseId = $id
    $script:Pics = @{}; $script:VkScan = [ordered]@{}; $script:Sent.Clear()
    $script:States = New-Object Collections.ArrayList
    $keyboard = Set-Up $C.Layout
    Set-CaseDue $C.Due
    $script:Geo = Get-Geometry
    $row = [ordered]@{ case = $C.Id; pass = $(if ($Pass) { $Pass } else { 'a' }); zoom = $Zoom; group = $C.Group; setup = $C.Setup; asked = $C.Asked; reading = $C.Reading
        time = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'); excelProcess = $script:OwnPid; keyboard = $keyboard; geometry = $script:Geo }
    & $C.Body
    $row.states = @($script:States)
    $row.vkKeyScan = $script:VkScan
    $row.keysSent = @($script:Sent)
    $row.ended = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss')
    Clear-CaseDue
    Write-Line $row
    Park-Mouse
}

# ---- The environment ----------------------------------------------------------------------------

# File > Account, opened with Alt, F and a click on Account (its place read through UI Automation).
# Nothing on this page is pictured: it shows the account's name and address.
function Open-Account {
    [void](Keys '{ALT}' 700); [void](Keys 'f' 1800)
    $root = [Windows.Automation.AutomationElement]::FromHandle($script:Hwnd)
    $account = $root.FindFirst([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, 'Account')))
    if (-not $account) { return $null }
    $r = $account.Current.BoundingRectangle
    [void](Hand-Click ([int]($r.Left + $r.Width / 2)) ([int]($r.Top + $r.Height / 2)))
    Start-Sleep -Milliseconds 2500
    return $root
}
function Theme-Value($Root) {
    $c = New-Object Windows.Automation.AndCondition @(
        (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, 'Office Theme')),
        (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::ComboBox)))
    $combo = $Root.FindFirst([Windows.Automation.TreeScope]::Descendants, $c)
    if (-not $combo) { return $null }
    try { return [string]$combo.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value } catch { return $null }
}
function Run-Environment {
    $script:CaseId = '0'; $script:Pics = @{}; $script:States = New-Object Collections.ArrayList
    $envRow = [ordered]@{ case = '0'; pass = $(if ($Pass) { $Pass } else { 'a' }); time = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss')
        screenshots = 'PrintWindow (PW_RENDERFULLCONTENT), each window of Excel''s laid at its place'; keys = 'SendInput (virtual-key, and the scan code of the layout of Excel''s window)'; mouse = 'SetCursorPos and mouse_event' }
    $keyboard = Set-Up 'uk'
    Set-CaseDue 150
    $c2r = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration' -ErrorAction SilentlyContinue
    $xl = $script:Xl
    $envRow.excel = [ordered]@{ version = [string]$xl.Version; build = [string]$xl.Build; versionToReport = [string]$c2r.VersionToReport; platform = [string]$c2r.Platform
        standardFont = [string]$xl.StandardFont; standardFontSize = [double]$xl.StandardFontSize; international = (Read-International) }
    $pers = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize'
    $hc = New-Object CellFormat.Native+HIGHCONTRAST; $hc.cbSize = [uint32][Runtime.InteropServices.Marshal]::SizeOf($hc)
    [void][CellFormat.Native]::SystemParametersInfo(0x0042, $hc.cbSize, [ref]$hc, 0)   # SPI_GETHIGHCONTRAST
    $intl = Get-ItemProperty 'HKCU:\Control Panel\International'
    $envRow.windows = [ordered]@{ appsUseLightTheme = [int]$pers.AppsUseLightTheme; systemUsesLightTheme = [int]$pers.SystemUsesLightTheme
        dpiOfExcelWindow = [int][CellFormat.Native]::GetDpiForWindow($script:Hwnd); displayScale = ('{0}%' -f [int]([CellFormat.Native]::GetDpiForWindow($script:Hwnd) * 100 / 96))
        workArea = "$($script:Area.Width)x$($script:Area.Height)"; highContrastOn = [bool]($hc.dwFlags -band 1)
        culture = (Get-Culture).Name; uiCulture = (Get-UICulture).Name; regionalFormat = [ordered]@{ localeName = [string]$intl.LocaleName; sShortDate = [string]$intl.sShortDate; sTimeFormat = [string]$intl.sTimeFormat; sCurrency = [string]$intl.sCurrency; sDecimal = [string]$intl.sDecimal; sThousand = [string]$intl.sThousand }
        languages = @(Get-WinUserLanguageList | ForEach-Object { "$($_.LanguageTag): $($_.InputMethodTips -join ', ')" })
        # Which key arrangement the Japanese layout uses: kbd106.dll is the Japanese keyboard, kbd101.dll the English one.
        japaneseLayerDriver = [string](Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Services\i8042prt\Parameters' -ErrorAction SilentlyContinue).'LayerDriver JPN'
        keyboardIdentifierOverride = [string](Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Services\i8042prt\Parameters' -ErrorAction SilentlyContinue).PSObject.Properties['OverrideKeyboardIdentifier'] }
    $theme = Get-ItemProperty 'HKCU:\Software\Microsoft\Office\16.0\Common' -ErrorAction SilentlyContinue
    $envRow.officeThemeRegistry = if ($theme -and $theme.PSObject.Properties['UI Theme']) { [string]$theme.'UI Theme' } else { 'no UI Theme value (Office default)' }
    $envRow.keyboard = $keyboard
    $envRow.geometry = Get-Geometry
    # The vkKeyScan of every character the cases send, on each layout.
    $tables = [ordered]@{}
    foreach ($l in 'uk', 'ja') {
        $hkl = [CellFormat.Native]::LoadKeyboardLayout($script:Layouts[$l], 0); $t = [ordered]@{}
        foreach ($c in @('~', '!', '@', '#', '$', '%', '^', '&', '_', '=', 'b', 'i', 'u', '1', '2', '3', '4', '5')) { $t[$c] = ('0x{0:x3}' -f [int][CellFormat.Native]::VkKeyScanEx([char]$c, $hkl)) }
        $tables[$l] = $t
    }
    $envRow.vkKeyScan = $tables
    $root = Open-Account
    if ($root) {
        $names = @()
        foreach ($el in $root.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition)) {
            $n = [string]$el.Current.Name
            if ($n -match 'Version|Build|Channel|Office Theme|Click-to-Run|Microsoft 365|About Excel' -and $n -notmatch '@') {
                $v = ''
                try { $p = $el.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern); $v = [string]$p.Current.Value } catch { }
                $names += [ordered]@{ name = $n; control = [string]$el.Current.ControlType.ProgrammaticName; value = $v }
            }
        }
        $envRow.fileAccount = $names
        $envRow.officeTheme = Theme-Value $root
    } else { $envRow.fileAccount = 'the Account entry was not found by UI Automation' }
    Send-KeysRaw '{ESC}' 900
    try { [void]$xl.Workbooks.Count; if (-not $xl.Ready) { Send-KeysRaw '{ESC}' 900 } } catch { Send-KeysRaw '{ESC}' 900 }
    # The keyboard checked end to end: every character the cases send typed into A1 as text and read back.
    $probe = "'~!@#$%^&_=1234.5abc"
    Select-Cell 'A1'
    $sent = Keys ($probe -replace '([+^%~(){}\[\]])', '{$1}') 500
    $seen = [CellFormat.Ui]::Bar([long]$script:Hwnd, 3000)
    Send-KeysRaw '~' 800
    $back = [string]$script:Ws.Range('A1').Formula
    $envRow.keyboardCheck = [ordered]@{ typed = $probe; sent = $sent; formulaBarBeforeEnter = $(if (G $seen 'value') { G $seen 'value' } else { G $seen 'document' }); readBack = $back; same = ($back -ceq $probe.Substring(1)) }
    Clear-CaseDue
    Write-Line $envRow
    Say ($envRow | ConvertTo-Json -Compress -Depth 6)
}

# ---- Exploring (for writing the cases; nothing is written to the log) --------------------------

# Steps separated by ';': keys:<spec> | click:<x>,<y> | hover:<x>,<y> | wait:<ms> | select:<A1> |
# clickname:<regex>[@<type>][@right|@left] (the first element of any window of Excel's so named) |
# find:<regex> (every element so named, in every window of Excel's) | dump:<label>[:<depth>] (every
# window but the main one, its tree printed and its picture kept on this machine) | dumpmain:<label>:<regex>
function Find-Named([string]$Pattern, [string]$Type = $null, [int]$Max = 40) {
    $all = @()
    foreach ($w in [CellFormat.Win]::Of($script:OwnPid)) {
        $r = [CellFormat.Ui]::FindAll([long]$w.hwnd, $Pattern, $Type, $Max, 20000, 20000)
        if ($script:CaseId -eq 'explore') { Say "    searched $($w.class): seen $(G $r 'seen'), $(G $r 'ms') ms $(G $r 'error')" }
        foreach ($i in @(G $r 'items')) { $i['window'] = $w.class; $all += $i }
    }
    return $all
}
function Show-Items($Items) {
    foreach ($i in $Items) { Say ('    {0}{1} [{2}] class={3}{4}{5}{6}{7}{8}' -f ('  ' * [Math]::Min([int]$i['depth'], 30)), $i['type'], $i['name'], $i['class'], $(if ($i.ContainsKey('rect')) { ' rect=' + ($i['rect'] -join ',') } else { '' }), $(if ($i.ContainsKey('selected')) { ' selected=' + $i['selected'] } else { '' }), $(if ($i.ContainsKey('toggle')) { ' toggle=' + $i['toggle'] } else { '' }), $(if ($i.ContainsKey('value')) { ' value=[' + $i['value'] + ']' } else { '' }), $(if ($i.ContainsKey('enabled')) { ' DISABLED' } else { '' })) }
}
function Explore([string]$Steps) {
    $script:CaseId = 'explore'; $script:Pics = @{}; $script:States = New-Object Collections.ArrayList
    [void](Set-Up 'uk')
    Set-CaseDue 600
    $script:Geo = Get-Geometry
    foreach ($step in ($Steps -split ';')) {
        $kind, $arg = $step -split ':', 2
        Say "step $step"
        switch ($kind) {
            'keys' { [void](Keys $arg 800) }
            'click' { $x, $y = $arg -split ','; [void](Hand-Click ([int]$x) ([int]$y)) }
            'hover' { $x, $y = $arg -split ','; Show-Excel $script:Xl; Hand-Move ([int]$x) ([int]$y) }
            'wait' { Start-Sleep -Milliseconds ([int]$arg) }
            'select' { Select-Cell $arg }
            'clickname' {
                $parts = $arg -split '@'; $type = if ($parts.Count -gt 1 -and $parts[1] -notin 'right', 'left') { $parts[1] } else { $null }
                $found = @(Find-Named $parts[0] $type 1)
                if (-not $found) { Say '    not found'; continue }
                $r = $found[0]['rect']; $x = [int](($r[0] + $r[2]) / 2); $y = [int](($r[1] + $r[3]) / 2)
                if ($parts -contains 'right') { $x = $r[2] - 6 }
                if ($parts -contains 'left') { $x = $r[0] + 8 }
                Show-Items $found
                [void](Hand-Click $x $y)
            }
            'find' { Show-Items (Find-Named $arg) }
            'dumpmain' {
                $label, $depth = $arg -split ':'
                $t = [CellFormat.Ui]::Tree([long]$script:Hwnd, [int]$depth, 3000, 60000)
                [IO.File]::WriteAllText((Join-Path $Raw "explore-$label-main.json"), ($t | ConvertTo-Json -Depth 8), $Utf8)
                if ($t.ContainsKey('error')) { Say "    $($t['error'])" }
                Show-Items @(G $t 'items')
            }
            'dump' {
                $label, $depth = $arg -split ':'; if (-not $depth) { $depth = 14 }
                $a = Grab-Scene; $a.bmp.Save((Join-Path $Raw "explore-$label.png"), [Drawing.Imaging.ImageFormat]::Png); $a.bmp.Dispose()
                foreach ($w in [CellFormat.Win]::Of($script:OwnPid)) {
                    if ($w.class -eq 'XLMAIN' -or $script:Shadows -contains $w.class) { continue }
                    Say "  window $($w.class) [$($w.title)] $($w.rect -join ',')"
                    $t = [CellFormat.Ui]::Tree([long]$w.hwnd, [int]$depth, 1500, 30000)
                    [IO.File]::WriteAllText((Join-Path $Raw "explore-$label-$($w.class).json"), ($t | ConvertTo-Json -Depth 8), $Utf8)
                    if ($t.ContainsKey('error')) { Say "    $($t['error'])" }
                    Show-Items @(G $t 'items')
                }
            }
        }
    }
    Clear-CaseDue
}

# ---- Main ---------------------------------------------------------------------------------------

. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel-2\case-guard.ps1')
Start-CaseGuard
Say "Excel processes before the run (left alone): $($script:ExcelsAtStart -join ', ')"
try {
    if ($Explore) {
        try { Explore $Explore } catch { Say "explore failed: $($_.Exception.Message) (line $($_.InvocationInfo.ScriptLineNumber))" }
    }
    foreach ($id in $Case) {
        if ($id -ne '0' -and -not $Cases.Contains($id)) { throw "no case $id" }
        try {
            if ($id -eq '0') { Run-Environment; continue }
            Run-Case $Cases[$id]
        }
        catch {
            Clear-CaseDue
            $script:NoCom = $false
            $row = [ordered]@{ case = $id; pass = $(if ($Pass) { $Pass } else { 'a' }); zoom = $Zoom; step = 'failed'; error = $_.Exception.Message; line = $_.InvocationInfo.ScriptLineNumber; statesSoFar = @($script:States | ForEach-Object { $_.state }) }
            Write-Line $row
            Say "case $id failed: $($_.Exception.Message) (line $($_.InvocationInfo.ScriptLineNumber))"
            try { [void](Until-Ready 4) } catch { }
        }
        if ($script:Guard.Ended) {
            Write-Line ([ordered]@{ case = $id; step = 'Excel ended'; error = $script:Guard.Ended })
            Say "case ${id}: $($script:Guard.Ended)"
            $script:Guard.Ended = ''
            $script:Xl = $null
        }
    }
}
finally {
    if ($script:Xl -and $script:FirstExcelLayout) {
        try { [void](Until-Ready 3) } catch { }
        try { $k = Restore-Keyboard; Say "keyboard restored: $($k | ConvertTo-Json -Compress)" } catch { Say "keyboard not restored: $($_.Exception.Message)" }
    }
    Stop-OwnExcel
    $left = @(Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id } | Where-Object { $script:ExcelsAtStart -notcontains $_ })
    Say "Excel processes now that were not there at the start: $($left -join ', ') (this run's own were $($script:OwnPids -join ', '))"
}
