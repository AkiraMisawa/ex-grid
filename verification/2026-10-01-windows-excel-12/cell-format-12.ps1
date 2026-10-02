<#
.SYNOPSIS
    docs/specs/exsheet/verify-on-windows-12.md, Part A: what Excel does to the line between two cells
    after other operations, what inserting and deleting rows and columns leave on the edges, outlines
    over whole rows, columns and the Sheet, and what the formatting keys do to a column's width and to
    the built-in date and time formats. ADR-0063's "Readings until the twelfth Windows run".

        powershell -File cell-format-12.ps1 -Case 0               the environment, and the keyboard checked
        powershell -File cell-format-12.ps1 -Case 1,2,3           cases of the procedure's tables
        powershell -File cell-format-12.ps1 -Case 5 -Pass b       a second pass of a case (files 5-b-...)
        powershell -File cell-format-12.ps1 -Explore <steps>      for writing cases (nothing recorded)

    This is the eleventh run's cell-format.ps1 (verification/2026-10-01-windows-excel-11/), copied as the
    procedure asks. Its keys, mouse, pictures, pixel readings, COM reads and Format Cells helpers are
    unchanged; its cases are replaced by the twelfth run's, with the helpers they need added: an edge
    read from both cells that share it, the Selection parked away from the cells read, the fill handle
    found by its cursor, and a state read safely after a key that may open an edit.

    Every key a case asks about is sent through SendInput (a virtual-key and a scan code per key, the
    scan code from the layout of Excel's window), and every click and drag is the real mouse. COM only
    sets a case up and reads the result. The script starts an Excel of its own for every case and ends
    only that one: it never attaches to a running Excel, because one of the user's may be open.

    Each case: a new workbook with one sheet, Sheet1, maximised, at 100% zoom, A1 in view and selected;
    Excel's window switched to the English (UK) keyboard with the IME off. Colours are sampled as hex
    from the pictures, never from COM.

    Pictures are the windows' own rendering (PrintWindow, PW_RENDERFULLCONTENT): the screen copy comes
    back empty on this machine since 2026-09-29. A dialog or a popup of Excel's is a window of its own,
    so it is captured on its own and laid over the window at its place.

    Outputs, beside this script:
      cell-format-12.jsonl  one line per case: the set-up, per state what COM read and what the pixels say
      shots\                per state: the cells (A1 to about H16, with the headings), any dialog or popup
                            of Excel's, and the crops a case names (enlarged two or four times)
    The full pictures stay on this machine, in %LOCALAPPDATA%\exgrid-layer3\cell-format-12\.
#>
#Requires -Version 5.1
param([string[]]$Case, [string]$Pass = '', [int]$Zoom = 100, [string]$Explore = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Case) { $Case = @($Case | ForEach-Object { $_ -split ',' } | Where-Object { $_ }) }

. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel\excel-driver.ps1')
$script:Shots = Join-Path $PSScriptRoot 'shots'
$Log = Join-Path $PSScriptRoot 'cell-format-12.jsonl'
$Raw = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\cell-format-12'
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
[DllImport("user32.dll")] public static extern bool GetCursorInfo(ref CURSORINFO ci);
[StructLayout(LayoutKind.Sequential)] public struct CURSORINFO { public int cbSize; public int flags; public IntPtr hCursor; public int x; public int y; }
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


# ---- Kept from the eleventh run's cases ---------------------------------------------------------

# Excel's International values (the eleventh run's case 20).
function Read-International {
    $x = $script:Xl
    return [ordered]@{ countryCode = $x.International(1); countrySetting = $x.International(2); decimalSeparator = $x.International(3); thousandsSeparator = $x.International(4)
        listSeparator = $x.International(5); dateOrder = $x.International(32); currencyCode = $x.International(25); dateSeparator = $x.International(17) }
}

# Format Cells. While a dialog of Excel's is open COM is not called ($script:NoCom): a COM call made
# while one is up can wait as long as it stays (2026-09-27).
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

# ---- The twelfth run: an edge read from both cells that share it --------------------------------

$script:Opposite = @{ top = 'bottom'; bottom = 'top'; left = 'right'; right = 'left' }
$script:AllEdges = [ordered]@{ left = 7; top = 8; bottom = 9; right = 10; insideVertical = 11; insideHorizontal = 12 }
# The cell on the other side of one of a cell's edges (B2 bottom: B3), or $null at the Sheet's edge.
function Neighbour-Of([string]$A1, [string]$Side) {
    $r = $script:Ws.Range($A1); $row = [int]$r.Row; $col = [int]$r.Column
    switch ($Side) { 'top' { $row-- } 'bottom' { $row++ } 'left' { $col-- } 'right' { $col++ } }
    if ($row -lt 1 -or $col -lt 1 -or $row -gt 1048576 -or $col -gt 16384) { return $null }
    return [string]$script:Ws.Cells.Item($row, $col).Address($false, $false)
}
function In-View([string]$A1) {
    $v = $script:Book.Windows.Item(1).VisibleRange; $r = $script:Ws.Range($A1)
    $top = [int]$v.Row; $left = [int]$v.Column; $bottom = $top + [int]$v.Rows.Count - 1; $right = $left + [int]$v.Columns.Count - 1
    return ([int]$r.Row -ge $top -and [int]$r.Row -le $bottom -and [int]$r.Column -ge $left -and [int]$r.Column -le $right)
}
# One edge, read from both cells that share it ("B2 bottom" and "B3 top"); with -Pixels, and the cell in
# view, the pixels across it in the state's picture.
function Shared-Edge([string]$A1, [string]$Side, [switch]$Pixels) {
    $o = [ordered]@{}
    $o["$A1 $Side"] = Edge-Of $script:Ws.Range($A1) $script:EdgeNames[$Side]
    $n = Neighbour-Of $A1 $Side
    if ($n) { $o["$n $($script:Opposite[$Side])"] = Edge-Of $script:Ws.Range($n) $script:EdgeNames[$script:Opposite[$Side]] }
    else { $o.neighbour = 'none: the edge of the Sheet' }
    if ($Pixels) { $o.across = $(if (In-View $A1) { Across-Edge $script:Img $A1 $Side } else { 'not in view' }) }
    return $o
}
function Shared-Edges([string[]]$Edges, [switch]$Pixels) {
    $o = @()
    foreach ($e in $Edges) { $c, $s = $e -split ' '; $o += (Shared-Edge $c $s -Pixels:$Pixels) }
    return , $o
}
# A whole row's, column's or Sheet's Borders (the level's own record).
function Level-Borders($Range) { $o = [ordered]@{}; foreach ($n in $script:AllEdges.Keys) { $o[$n] = Edge-Of $Range $script:AllEdges[$n] }; return $o }
function Values-Of([string[]]$Cells) { $o = [ordered]@{}; foreach ($c in $Cells) { $o[$c] = [string]$script:Ws.Range($c).Formula }; return $o }
# Each cell's ground in the picture: its most frequent pixel, 3 px in from its edges (a Fill, or the paper).
function Ground-Of([string[]]$Cells) { $o = [ordered]@{}; foreach ($c in $Cells) { $o[$c] = (Ink-Of $script:Img $c).ground }; return $o }
# A dialog that holds Excel (Format Cells, a message). Excel's Quick Analysis button is a NUIDialog
# too, and shows under any selection of more than one cell, so it is not counted.
function Modal-Dialog { foreach ($w in [CellFormat.Win]::Of($script:OwnPid)) { if ($w.class -in 'bosa_sdm_XL9', '#32770') { return $w } }; return $null }

# The Selection moved away from the cells a case reads, so that its outline covers none of their
# edges: Esc (which ends a copy's marquee), Ctrl+Home, then Down 13 and Right 6, to G14.
function Park-Selection { return (Keys '{ESC}^{HOME}{DOWN 13}{RIGHT 6}' 500) }
# A state, unless the key left an edit or a dialog open: then it is read from the picture and the
# Formula Bar, Esc follows, and the state is read again once Excel answers COM.
function Guarded-State([string]$GName, [string]$GDid, [scriptblock]$GThen) {
    Start-Sleep -Milliseconds 300
    $dlg = Modal-Dialog; $busy = $false
    if (-not $dlg) { try { [void]$script:Xl.ActiveCell.Address($false, $false) } catch { $busy = $true } }
    if (-not $dlg -and -not $busy) { [void](State $GName $GDid $GThen); return }
    $script:NoCom = $true
    [void](State $GName $GDid { @{ excelBusy = $(if ($dlg) { "a dialog: $($dlg.class) [$($dlg.title)]" } else { 'COM refused the call: an edit is open' }); bar = [CellFormat.Ui]::Bar([long]$script:Hwnd, 4000) } })
    Send-KeysRaw '{ESC}' 600
    $e = Wait-NoDialog
    $script:NoCom = $false
    $n = Until-Ready
    [void](State "$GName-esc" "{ESC} ($e more for a dialog, $n more for Excel)" $GThen)
}
# The state a case sets up through COM: each edge asked about, read from both cells, with the pixels
# across it; every edge of the ranges named; a crop.
function Before-Op([string[]]$OpEdges, [string[]]$OpRanges, [string]$OpFrom, [string]$OpTo, [scriptblock]$OpMore = $null) {
    [void](State 'set' 'COM set-up' {
        $e = Read-Edges $OpRanges
        $o = [ordered]@{ shared = (Shared-Edges $OpEdges -Pixels); edges = $e; lines = (Lines-In $e); crop = (Crop-Cells $OpFrom $OpTo 'set-x4' 8 4) }
        if ($OpMore) { $m = & $OpMore; foreach ($mk in @($m.Keys)) { $o[$mk] = $m[$mk] } }
        $o
    })
}
# The state an operation leaves, read twice: at once, with the Selection where the operation left it;
# then with the Selection parked at G14, when the pixels across each edge are read as well.
function After-Op([string]$OpDid, [string[]]$OpEdges, [string[]]$OpRanges, [string]$OpFrom, [string]$OpTo, [scriptblock]$OpMore = $null) {
    $dlg = Modal-Dialog
    if ($dlg) {
        $script:NoCom = $true
        [void](State 'dialog' "$OpDid; a dialog opened: $($dlg.class) [$($dlg.title)]" { @{ dialog = (Dialog-State 4) } })
        $c = Close-Dialog
        $OpDid += "; the dialog closed with $c"
    }
    [void](State 'done' $OpDid {
        $e = Read-Edges $OpRanges
        $o = [ordered]@{ selection = (Selection-Now); shared = (Shared-Edges $OpEdges); edges = $e; lines = (Lines-In $e); crop = (Crop-Cells $OpFrom $OpTo 'done-x2' 8 2) }
        if ($OpMore) { $m = & $OpMore; foreach ($mk in @($m.Keys)) { $o[$mk] = $m[$mk] } }
        $o
    })
    $pk = Park-Selection
    [void](State 'parked' $pk {
        $e = Read-Edges $OpRanges
        $o = [ordered]@{ selection = (Selection-Now); shared = (Shared-Edges $OpEdges -Pixels); lines = (Lines-In $e); crop = (Crop-Cells $OpFrom $OpTo 'parked-x4' 8 4) }
        if ($OpMore) { $m = & $OpMore; foreach ($mk in @($m.Keys)) { $o[$mk] = $m[$mk] } }
        $o
    })
}
# The window scrolled (COM) to show a far corner of the Sheet, with the edges there read and pictured.
function Far-State([string]$FName, [int]$Row, [int]$Col, [string[]]$FEdges, [string]$FFrom, [string]$FTo) {
    $win = $script:Book.Windows.Item(1)
    $win.ScrollRow = $Row; $win.ScrollColumn = $Col
    [void](State $FName "COM: the window scrolled to row $Row, column $Col, to picture the Sheet's far edges" {
        @{ visible = [string]$win.VisibleRange.Address($false, $false); shared = (Shared-Edges $FEdges -Pixels); crop = (Crop-Cells $FFrom $FTo "$FName-x4" 8 4) }
    })
}

# ---- Group 1: the line between two cells, after other operations --------------------------------

# Select E5, Ctrl+C, select B2, Ctrl+V, all with keys from A1.
$script:E1 = @('B2 right', 'B2 top', 'B2 bottom', 'B2 left', 'E5 right')
function Paste-E5-On-B2 {
    Before-Op $script:E1 @('B2:C2', 'E5:F5') 'A1' 'F6' { @{ values = (Values-Of 'B2', 'E5') } }
    $k = Keys '{DOWN 4}{RIGHT 4}'
    $k2 = Keys '^c' 700
    $k3 = Keys '{UP 3}{LEFT 3}'
    [void](State 'b2-selected' "$k; $k2; $k3" { @{ selection = (Selection-Now) } })
    $k4 = Keys '^v' 1200
    After-Op $k4 $script:E1 @('B2:C2', 'E5:F5') 'A1' 'F6' { @{ values = (Values-Of 'B2', 'E5') } }
}
Def '1' 'between-cells' 'C2''s left edge thin blue. E5''s right edge thick red; E5 = 1' 'Select E5, Ctrl+C, select B2, Ctrl+V (keys from A1: Down x4, Right x4, Ctrl+C, Up x3, Left x3, Ctrl+V): B2''s right and C2''s left, read from both cells; the line drawn' 'the paste writes C2''s left too: thick red, read from both' {
    Set-Edge 'C2' 7 1 2 $Blue; Set-Edge 'E5' 10 1 4 $Red; Put 'E5' '1'
    Paste-E5-On-B2
}
Def '2' 'between-cells' 'B2''s right edge thick red (so C2''s left reads it). E5 = 1, no borders' 'Select E5, Ctrl+C, select B2, Ctrl+V (as in 1): B2''s right and C2''s left; the line drawn' 'none, read from both' {
    Set-Edge 'B2' 10 1 4 $Red; Put 'E5' '1'
    Paste-E5-On-B2
}
$script:E3 = @('B2 right', 'B2 bottom', 'B2 top', 'B3 left', 'B3 right', 'B3 bottom', 'B4 left', 'B4 right', 'B4 bottom')
function Setup-3 { Put 'B2' '1'; Set-Edge 'B2' 10 1 4 $Red; Set-Edge 'B2' 9 1 4 $Black; Set-Edge 'C3' 7 1 2 $Blue }
Def '3' 'between-cells' 'B2 = 1 with its right edge thick red and its bottom edge thick black. C3''s left edge thin blue' 'Select B2:B4 (A1, Down, Right, Shift+Down x2), Ctrl+D: every edge of B3 and B4, C3''s and C4''s left edges, B5''s top edge; the lines drawn' 'B3 and B4 take B2''s right and bottom; C3''s and C4''s left read thick red' {
    Setup-3
    Before-Op $script:E3 @('A2:C5') 'A1' 'D6' { @{ values = (Values-Of 'B2', 'B3', 'B4') } }
    $k = Keys '{DOWN}{RIGHT}+{DOWN 2}'
    [void](State 'selected' $k { @{ selection = (Selection-Now) } })
    $k = Keys '^d' 1000
    After-Op $k $script:E3 @('A2:C5') 'A1' 'D6' { @{ values = (Values-Of 'B2', 'B3', 'B4', 'B5') } }
}
$script:E4 = @('B2 right', 'B2 bottom', 'B2 top', 'C2 top', 'C2 bottom', 'C2 right', 'D2 top', 'D2 bottom', 'D2 right')
Def '4' 'between-cells' 'B2 = 1 with its bottom edge thick black and its right edge thick red. E2''s left edge thin blue' 'Select B2:D2 (A1, Down, Right, Shift+Right x2), Ctrl+R: every edge of C2 and D2, C3''s and D3''s top edges, and E2''s left edge; the lines drawn' 'C2 and D2 take B2''s edges, the neighbours read them, and E2''s left reads thick red' {
    Put 'B2' '1'; Set-Edge 'B2' 9 1 4 $Black; Set-Edge 'B2' 10 1 4 $Red; Set-Edge 'E2' 7 1 2 $Blue
    Before-Op $script:E4 @('B1:E3') 'A1' 'F4' { @{ values = (Values-Of 'B2', 'C2', 'D2') } }
    $k = Keys '{DOWN}{RIGHT}+{RIGHT 2}'
    [void](State 'selected' $k { @{ selection = (Selection-Now) } })
    $k = Keys '^r' 1000
    After-Op $k $script:E4 @('B1:E3') 'A1' 'F4' { @{ values = (Values-Of 'B2', 'C2', 'D2', 'E2') } }
}
# The cursor's handle at a point. Excel sets its own: a white cross over a cell, a thin cross over the
# fill handle, arrows over the Selection's border.
function Cursor-At([int]$X, [int]$Y) {
    [void][ExcelDriver.Native]::SetCursorPos($X - 1, $Y); Start-Sleep -Milliseconds 60
    [void][ExcelDriver.Native]::SetCursorPos($X, $Y); Start-Sleep -Milliseconds 220
    $ci = New-Object CellFormat.Native+CURSORINFO; $ci.cbSize = [Runtime.InteropServices.Marshal]::SizeOf($ci)
    [void][CellFormat.Native]::GetCursorInfo([ref]$ci)
    return ('0x{0:x}' -f [long]$ci.hCursor)
}
# A cell's fill handle, found by its cursor: along the diagonal through the Selection's bottom-right
# corner (the right gridline lies 2 px left of the coordinate COM gives, the bottom one 1 px up), from
# 8 px inside to 5 px outside, the cursor at each point. The handle is the middle of the longest run of
# points whose cursor is not the one over the cell's middle. The drag goes from there to the middle of
# the target cell in 25 steps, as a hand moves; the values it leaves show whether it filled.
function Drag-FillHandle([string]$Cell, [string]$To) {
    Show-Excel $script:Xl
    $r = Cell-Rect $Cell; $t = Cell-Rect $To
    $cx = [int]$r.Right - 2; $cy = [int]$r.Bottom - 1
    $mid = Cursor-At ([int](($r.Left + $r.Right) / 2)) ([int](($r.Top + $r.Bottom) / 2))
    $scan = @(); $cursors = @()
    for ($d = -8; $d -le 5; $d++) { $c = Cursor-At ($cx + $d) ($cy + $d); $scan += "$d $c"; $cursors += $c }
    $best = -1; $bestLen = 0; $i = 0
    while ($i -lt $cursors.Count) {
        if ($cursors[$i] -eq $mid) { $i++; continue }
        $j = $i; while ($j + 1 -lt $cursors.Count -and $cursors[$j + 1] -eq $cursors[$i]) { $j++ }
        if ($j - $i + 1 -gt $bestLen) { $bestLen = $j - $i + 1; $best = $i }
        $i = $j + 1
    }
    $script:Handle5 = [ordered]@{ corner = @($cx, $cy); cursorOverCell = $mid; diagonal = $scan }
    if ($best -lt 0) { throw "no fill handle along the diagonal: $($scan -join '; ')" }
    $dd = -8 + $best + [int][Math]::Floor(($bestLen - 1) / 2)
    $x1 = $cx + $dd; $y1 = $cy + $dd
    $x2 = [int](($t.Left + $t.Right) / 2); $y2 = [int](($t.Top + $t.Bottom) / 2)
    $script:Handle5.pressedAt = @($x1, $y1); $script:Handle5.cursorThere = $cursors[$best]; $script:Handle5.runLength = $bestLen
    [void][ExcelDriver.Native]::SetCursorPos($x1, $y1); Start-Sleep -Milliseconds 200
    [ExcelDriver.Native]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 150
    for ($s = 1; $s -le 25; $s++) { [void][ExcelDriver.Native]::SetCursorPos([int]($x1 + ($x2 - $x1) * $s / 25), [int]($y1 + ($y2 - $y1) * $s / 25)); Start-Sleep -Milliseconds 25 }
    Start-Sleep -Milliseconds 250
    [ExcelDriver.Native]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 900
    return "a drag of ${Cell}'s fill handle with the mouse, from ($x1, $y1) to the middle of $To ($x2, $y2)"
}
Def '5' 'between-cells' 'As in 3: B2 = 1 with its right edge thick red and its bottom edge thick black. C3''s left edge thin blue' 'B2 selected (A1, Down, Right); with the mouse, B2''s fill handle dragged down to B4: as in 3' 'as Ctrl+D' {
    Setup-3
    Before-Op $script:E3 @('A2:C5') 'A1' 'D6' { @{ values = (Values-Of 'B2', 'B3', 'B4') } }
    $k = Keys '{DOWN}{RIGHT}'
    [void](State 'selected' $k { @{ selection = (Selection-Now) } })
    $d = Drag-FillHandle 'B2' 'B4'
    After-Op $d $script:E3 @('A2:C5') 'A1' 'D6' { @{ values = (Values-Of 'B2', 'B3', 'B4', 'B5'); fillHandle = $script:Handle5 } }
}

# ---- Group 2: two rows that meet when the row between them is deleted ---------------------------

Def '6' 'delete' 'B2''s bottom edge thick black (B3''s top edge reads it). B4''s top edge thin blue (B3''s bottom edge reads it)' 'Select row 3 (A3, Shift+Space), Ctrl+-: B2''s bottom and the new B3''s top, read from both; the line drawn' 'unknown: record. ADR-0063 reads the upper row''s line as winning until this answer' {
    Set-Edge 'B2' 9 1 4 $Black; Set-Edge 'B4' 8 1 2 $Blue
    Before-Op @('B2 bottom', 'B3 bottom', 'B4 bottom') @('A2:C4') 'A1' 'D5'
    $k = Keys '{DOWN 2}+{SPACE}'
    [void](State 'row-selected' $k { @{ selection = (Selection-Now) } })
    $k = Keys '^-' 1200
    After-Op $k @('B2 bottom', 'B3 bottom', 'B2 top') @('A2:C4') 'A1' 'D5'
}
Def '7' 'delete' 'B2''s right edge thick black, D2''s left edge thin blue' 'Select column C (C1, Ctrl+Space), Ctrl+-: B2''s right and the new C2''s left; the line drawn' 'unknown: record' {
    Set-Edge 'B2' 10 1 4 $Black; Set-Edge 'D2' 7 1 2 $Blue
    Before-Op @('B2 right', 'C2 right', 'D2 right') @('A1:D3') 'A1' 'E3'
    $k = Keys '{RIGHT 2}^{SPACE}'
    [void](State 'column-selected' $k { @{ selection = (Selection-Now) } })
    $k = Keys '^-' 1200
    After-Op $k @('B2 right', 'C2 right', 'B2 left') @('A1:D3') 'A1' 'E3'
}
Def '8' 'delete' 'B2''s bottom edge thick black only' 'Delete row 3 as in 6 (A3, Shift+Space, Ctrl+-): the new B3''s top edge, read from both' 'thick black, read from both' {
    Set-Edge 'B2' 9 1 4 $Black
    Before-Op @('B2 bottom', 'B3 bottom') @('A2:C4') 'A1' 'D5'
    $k = Keys '{DOWN 2}+{SPACE}'
    [void](State 'row-selected' $k { @{ selection = (Selection-Now) } })
    $k = Keys '^-' 1200
    After-Op $k @('B2 bottom', 'B3 bottom') @('A2:C4') 'A1' 'D5'
}
Def '9' 'delete' 'B3''s top edge thin blue, set from B3; nothing on B2' 'Delete row 2 (A2, Shift+Space, Ctrl+-): B1''s bottom and the new B2''s top' 'unknown: record' {
    Set-Edge 'B3' 8 1 2 $Blue
    Before-Op @('B1 bottom', 'B2 bottom', 'B3 bottom') @('A1:C3') 'A1' 'D4'
    $k = Keys '{DOWN}+{SPACE}'
    [void](State 'row-selected' $k { @{ selection = (Selection-Now) } })
    $k = Keys '^-' 1200
    After-Op $k @('B1 bottom', 'B2 bottom', 'B1 top') @('A1:C3') 'A1' 'D4'
}

# ---- Group 3: what insertion leaves on the edges ------------------------------------------------

function Row-Level([int]$R) {
    $x = $script:Ws.Rows($R)
    return [ordered]@{ fill = ('pattern {0} color {1}' -f $x.Interior.Pattern, (Hex-Of $x.Interior.Color)); bold = $x.Font.Bold; italic = $x.Font.Italic; fontColour = (Hex-Of $x.Font.Color); borders = (Level-Borders $x) }
}
Def '10' 'insert' 'B1''s top edge thick black (the Sheet''s edge)' 'Select row 1 (A1, Shift+Space), Ctrl+Shift+=: every edge of the new B1 and of B2 (the old B1)' 'the line goes; the new row 1 takes nothing' {
    Set-Edge 'B1' 8 1 4 $Black
    Before-Op @('B1 top', 'B1 bottom', 'B1 left', 'B1 right') @('A1:C2') 'A1' 'D3'
    $k = Keys '+{SPACE}'
    [void](State 'row-selected' $k { @{ selection = (Selection-Now) } })
    $k = Keys '^+{=}' 1200
    After-Op $k @('B1 top', 'B1 bottom', 'B1 left', 'B1 right', 'B2 bottom', 'B2 left', 'B2 right') @('A1:C3') 'A1' 'D4' { @{ row1 = (Row-Level 1); row2 = (Row-Level 2) } }
}
Def '11' 'insert' 'B2''s bottom edge thick black, filled yellow' 'Select rows 3:4 (A3, Shift+Space, Shift+Down), Ctrl+Shift+=: B3''s top and bottom, B4''s top and bottom, B5''s top edge; B3''s and B4''s Fill' 'B3''s top reads the thick line; the edges between the new rows are empty; both new rows are yellow' {
    Set-Edge 'B2' 9 1 4 $Black; Set-Fill 'B2' $Yellow
    Before-Op @('B2 bottom', 'B3 bottom') @('A2:C4') 'A1' 'D6' { @{ fill = (Read-Fill 'B2', 'B3') } }
    $k = Keys '{DOWN 2}+{SPACE}+{DOWN}'
    [void](State 'rows-selected' $k { @{ selection = (Selection-Now) } })
    $k = Keys '^+{=}' 1200
    After-Op $k @('B2 bottom', 'B3 bottom', 'B4 bottom', 'B5 bottom', 'B3 left', 'B3 right', 'B4 left', 'B4 right') @('A2:C6') 'A1' 'D7' {
        @{ fill = (Read-Fill 'B2', 'B3', 'B4', 'B5', 'A3', 'C3'); ground = (Ground-Of 'B2', 'B3', 'B4', 'B5'); row3 = (Row-Level 3); row4 = (Row-Level 4) }
    }
}
Def '12' 'insert' 'B2 = abc: bold, italic, red Font, yellow Fill' 'Insert a row at 3 as in the eleventh run''s case 12 (A3, Shift+Space, Ctrl+Shift+=): B3''s Font (bold, italic, colour) and Fill' 'B3 takes the Font and the Fill' {
    Put 'B2' 'abc'
    $f = $script:Ws.Range('B2').Font; Set-ComProperty $f 'Bold' $true; Set-ComProperty $f 'Italic' $true; Set-ComProperty $f 'Color' $Red
    Set-Fill 'B2' $Yellow
    [void](State 'set' 'COM set-up' { @{ font = (Read-Font 'B2', 'B3'); fill = (Read-Fill 'B2', 'B3'); crop = (Crop-Cells 'A1' 'D4' 'set-x4') } })
    $k = Keys '{DOWN 2}+{SPACE}'
    [void](State 'row-selected' $k { @{ selection = (Selection-Now) } })
    $k = Keys '^+{=}' 1200
    [void](State 'done' $k { @{ selection = (Selection-Now); font = (Read-Font 'B2', 'B3', 'B4', 'A3', 'C3'); fill = (Read-Fill 'B2', 'B3', 'B4', 'A3', 'C3'); row3 = (Row-Level 3); crop = (Crop-Cells 'A1' 'D5' 'done-x2' 8 2) } })
    $k = Park-Selection
    [void](State 'parked' $k { @{ selection = (Selection-Now); font = (Read-Font 'B3'); ground = (Ground-Of 'B2', 'B3', 'B4', 'A3', 'C3'); crop = (Crop-Cells 'A1' 'D5' 'parked-x4') } })
}
Def '13' 'insert' 'B2''s right edge thick black, filled yellow' 'Select column C (C1, Ctrl+Space), Ctrl+Shift+=: C2''s left and right edges, D2''s left edge (the old C2); C2''s Fill' 'C2''s left reads the thick line, its right is empty; C2 is yellow' {
    Set-Edge 'B2' 10 1 4 $Black; Set-Fill 'B2' $Yellow
    Before-Op @('B2 right', 'C2 right') @('A1:D3') 'A1' 'E3' { @{ fill = (Read-Fill 'B2', 'C2') } }
    $k = Keys '{RIGHT 2}^{SPACE}'
    [void](State 'column-selected' $k { @{ selection = (Selection-Now) } })
    $k = Keys '^+{=}' 1200
    After-Op $k @('B2 right', 'C2 right', 'D2 right', 'C2 top', 'C2 bottom') @('A1:E3') 'A1' 'F4' {
        @{ fill = (Read-Fill 'B2', 'C2', 'D2', 'C1', 'C3'); ground = (Ground-Of 'B2', 'C2', 'D2', 'C3') }
    }
}

# ---- Group 4: outlines over whole rows and the whole Sheet, inside lines over whole columns ------

Def '14' 'outline' 'Nothing set' 'Select row 3 (A3, Shift+Space), Ctrl+Shift+&: Rows(3).Borders (top, bottom, left, right); A3''s left edge; XFD3''s right edge; B3''s top and bottom edges' 'top and bottom only' {
    $k = Keys '{DOWN 2}+{SPACE}'
    [void](State 'row-selected' $k { @{ selection = (Selection-Now) } })
    $k = Ctrl-Char '&'
    After-Op $k @('B3 top', 'B3 bottom', 'B3 left', 'B3 right', 'A3 left', 'A3 top', 'XFD3 right', 'XFD3 bottom') @('A2:C4', 'XFC3:XFD3') 'A1' 'D5' {
        @{ rows3 = (Level-Borders $script:Ws.Rows(3)); rows2 = (Level-Borders $script:Ws.Rows(2)); rows4 = (Level-Borders $script:Ws.Rows(4)) }
    }
    Far-State 'right' 1 16376 @('XFD3 right', 'XFD3 top', 'XFD3 bottom', 'XFC3 right') 'XFB2' 'XFD4'
}
Def '15' 'outline' 'Nothing set' 'Select the whole Sheet (Ctrl+A twice from A1, which is empty), Ctrl+Shift+&: A1''s top and left edges, XFD1''s right edge, A1048576''s bottom edge, Columns(1).Borders(xlEdgeLeft), Columns(16384).Borders(xlEdgeRight), Rows(1).Borders(xlEdgeTop)' 'the left of A and the right of XFD only' {
    # Pass a: Ctrl+A twice, the key, then the reads. Pass b (added): the corner box clicked with the mouse
    # (the procedure's other way), and 2 s after the key before the reads. Pass c (added): pass a again
    # with those 2 s.
    if ($Pass -eq 'b') {
        $a1 = Cell-Rect 'A1'
        $d = Hand-Click ([int]$a1.Left - 20) ([int]$a1.Top - 14)
        [void](State 'corner-box' "the corner box: $d" { @{ selection = (Selection-Now) } })
    }
    else {
        $k = Keys '^a' 700
        [void](State 'ctrl-a-1' $k { @{ selection = (Selection-Now) } })
        $k = Keys '^a' 700
        [void](State 'ctrl-a-2' $k { @{ selection = (Selection-Now) } })
    }
    $k = Ctrl-Char '&'
    if ($Pass) { Start-Sleep -Milliseconds 2000; $k += ' (2 s before the reads)' }
    After-Op $k @('A1 top', 'A1 left', 'A1 right', 'A1 bottom', 'B1 top', 'A2 left', 'XFD1 right', 'XFD1 top', 'A1048576 bottom', 'A1048576 left', 'XFD1048576 right', 'XFD1048576 bottom') @('A1:B2') 'A1' 'C3' {
        @{ cells = (Level-Borders $script:Ws.Cells); columns1 = (Level-Borders $script:Ws.Columns(1)); columns16384 = (Level-Borders $script:Ws.Columns(16384)); rows1 = (Level-Borders $script:Ws.Rows(1)); rows1048576 = (Level-Borders $script:Ws.Rows(1048576)) }
    }
    Far-State 'bottom' 1048560 1 @('A1048576 bottom', 'A1048576 left', 'B1048576 bottom', 'A1048575 left') 'A1048572' 'C1048576'
    Far-State 'right' 1 16376 @('XFD1 right', 'XFD1 top', 'XFD2 right', 'XFC1 top') 'XFB1' 'XFD3'
    Far-State 'bottom-right' 1048560 16376 @('XFD1048576 right', 'XFD1048576 bottom') 'XFB1048572' 'XFD1048576'
}
Def '16' 'outline' 'Nothing set' 'Select columns B:C (B1, Ctrl+Space, Shift+Right). Ctrl+1, the Border tab, Inside (with the mouse), OK (with the mouse): B1''s top edge, B2''s top edge, B1048576''s bottom edge, and the B/C edge in row 5; a screenshot of rows 1 to 3' 'unknown at row 1 and row 1048576: record. Ticket 55 reads the inside line on the top of row 1 and the bottom of row 1048576 as well' {
    $k = Keys '{RIGHT}^{SPACE}+{RIGHT}'
    [void](State 'selected' $k { @{ selection = (Selection-Now) } })
    $k = Open-FormatCells
    [void](State 'opened' $k { @{ dialog = (Dialog-State 2) } })
    Visit-Tab 'Border'
    $d = Click-Item (Item-Named (Dialog-Items) '^Inside$' 'Button')
    Start-Sleep -Milliseconds 700
    [void](State 'inside-clicked' $d { @{ dialog = (Dialog-State) } })
    $d = Click-Item (Item-Named (Dialog-Items 2) '^OK$' 'Button')
    Start-Sleep -Milliseconds 900
    $e = Wait-NoDialog
    if ($e) { $d += " (the dialog was still open: $e Escapes)" }
    $script:NoCom = $false
    $n = Until-Ready
    After-Op "$d ($n Escapes needed for Excel)" @('B1 top', 'B2 top', 'B3 top', 'B1 left', 'B1 right', 'C1 top', 'C1 right', 'B5 right', 'B5 left', 'C5 right', 'B1048576 bottom', 'C1048576 bottom', 'B1048576 right') @('A1:D3', 'B5:C5') 'A1' 'D3' {
        @{ columnsBC = (Level-Borders $script:Ws.Range('B:C')); columns2 = (Level-Borders $script:Ws.Columns(2)); columns3 = (Level-Borders $script:Ws.Columns(3)); columns1 = (Level-Borders $script:Ws.Columns(1)); columns4 = (Level-Borders $script:Ws.Columns(4)) }
    }
    Far-State 'bottom' 1048560 1 @('B1048576 bottom', 'C1048576 bottom', 'B1048576 top', 'B1048576 right', 'B1048575 bottom') 'A1048572' 'D1048576'
}

# ---- Group 5: the formatting keys, a column's width, and the built-in date and time formats -----

$script:Keys17 = @(@('A1', '#', 'hash-date'), @('B1', '$', 'dollar'), @('C1', '!', 'bang'), @('D1', '%', 'percent'), @('E1', '^', 'caret'), @('F1', '@', 'at-time'))
function Row1-Now {
    $o = [ordered]@{ standardWidth = [double]$script:Ws.StandardWidth }
    foreach ($col in 'A', 'B', 'C', 'D', 'E', 'F', 'G') {
        $r = $script:Ws.Range("${col}1")
        $o[$col] = [ordered]@{ columnWidth = [double]$r.ColumnWidth; useStandardWidth = $r.EntireColumn.UseStandardWidth; text = [string]$r.Text; value = [string]$r.Formula; numberFormat = (Get-NF $r); numberFormatLocal = [string]$r.NumberFormatLocal }
    }
    return $o
}
# The values through COM (no column widens), then each cell's key from A1 rightwards, as the character
# on the UK layout (with the Shift that character needs; # needs none there).
function Keys-17([string]$Prefix, [string]$D1 = '0.123456', [string]$F1 = '46000') {
    Put 'A1' '46000.5'; Put 'B1' '1234567.5'; Put 'C1' '1234567.5'; Put 'D1' $D1; Put 'E1' '1234567.5'; Put 'F1' $F1
    Select-Cell 'A1'
    [void](State "$Prefix-set" 'COM set-up' { @{ row1 = (Row1-Now); crop = (Crop-Cells 'A1' 'H1' "$Prefix-set-x2" 8 2) } })
    $first = $true
    foreach ($kk in $script:Keys17) {
        $mv = ''
        if (-not $first) { $mv = (Keys '{RIGHT}' 300) + '; ' }
        $first = $false
        $k = Ctrl-Char $kk[1]
        Guarded-State "$Prefix-$($kk[2])" "$($kk[0]): $mv$k" { @{ selection = (Selection-Now); row1 = (Row1-Now); crop = (Crop-Cells 'A1' 'H1' "$Prefix-$($kk[2])-x2" 8 2) } }
    }
}
Def '17' 'keys-width' 'Columns A to G at the standard width (a new workbook). A1 = 46000.5, B1 = 1234567.5, C1 = 1234567.5, D1 = 0.123456, E1 = 1234567.5, F1 = 46000 (COM, as Formulas)' 'On each cell in turn (Right between them): A1 Ctrl+# (date), B1 Ctrl+Shift+$, C1 Ctrl+Shift+!, D1 Ctrl+Shift+%, E1 Ctrl+Shift+^, F1 Ctrl+Shift+@: each column''s width before and after, and the text shown' 'unknown: record which keys widen a standard-width column' {
    # Pass a: the procedure's values. In it, the texts of D1 (12%), E1 (1.23E+06) and F1 (00:00) fit the
    # standard width. Pass b (added): the Sheet's standard width set to 4 (COM StandardWidth; every column
    # still at the standard width), and D1 = 1234567.5, F1 = 46000.5, so that no key's text fits.
    if ($Pass -eq 'b') {
        Set-ComProperty $script:Ws 'StandardWidth' 4
        Keys-17 'narrow' '1234567.5' '46000.5'
    }
    else { Keys-17 'standard' }
}
Def '18' 'keys-width' 'As 17, with each column A to G set to width 12 first (COM ColumnWidth = 12, a width that is not the standard one)' 'The same keys: the same' 'no column widens' {
    Set-ComProperty $script:Ws.Range('A:G') 'ColumnWidth' 12
    Keys-17 'width12'
}

# Case 19: the built-in date and time formats under three regional formats. Set-Culture writes the
# user's regional format, and a new Excel reads it as it starts (the eleventh run's case 20). The format
# the run began with is set back at the end, and the registry key compared with the export taken
# before the run.
function Regional-Now {
    $i = Get-ItemProperty 'HKCU:\Control Panel\International'
    return [ordered]@{ localeName = [string]$i.LocaleName; sShortDate = [string]$i.sShortDate; sLongDate = [string]$i.sLongDate; sTimeFormat = [string]$i.sTimeFormat; sShortTime = [string]$i.sShortTime; iTLZero = [string]$i.iTLZero; s1159 = [string]$i.s1159; sDate = [string]$i.sDate; sCurrency = [string]$i.sCurrency }
}
function Dates-Now {
    $o = [ordered]@{}
    foreach ($c in 'A1', 'A2') {
        $r = $script:Ws.Range($c)
        $o[$c] = [ordered]@{ value2 = [string](Get-ComProperty $r 'Value2'); numberFormat = (Get-NF $r); numberFormatLocal = [string]$r.NumberFormatLocal; text = [string]$r.Text; columnWidth = [double]$r.ColumnWidth }
    }
    return $o
}
Def '19' 'keys-width' 'Under en-GB, en-US and ja-JP (Set-Culture, then a new Excel): A1 = the date 5 January 2026 (Value2 46027), A2 = the time 09:05 (Value2 545/1440), both General; the culture the run began with set back afterwards' 'Ctrl+# on A1, then Down and Ctrl+Shift+@ on A2, each as the character on the UK layout: NumberFormat / NumberFormatLocal, and the text shown' 'unknown: record whether Excel localises built-ins 15 and 20' {
    $intlKey = 'HKCU:\Control Panel\International'
    $orig = [string](Get-ItemProperty $intlKey).LocaleName
    $runDir = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\run-2026-10-01-12'
    try {
        foreach ($cul in 'en-GB', 'en-US', 'ja-JP') {
            $was = [string](Get-ItemProperty $intlKey).LocaleName
            $how = if ($cul -ne $was) { Set-Culture -CultureInfo $cul; Start-Sleep -Seconds 2; "Set-Culture $cul" } else { "already $cul (Set-Culture not called)" }
            $script:Keyboard19 = Set-Up 'uk'
            Set-CaseDue 240
            $script:Geo = Get-Geometry
            $child = (& powershell.exe -NoProfile -Command '(Get-Culture).Name' | Out-String).Trim()
            [void](State "$cul-start" "$how; a new Excel (process $($script:OwnPid))" {
                @{ culture = $cul; regional = (Regional-Now); cultureInANewPowerShell = $child; international = (Read-International); keyboard = $script:Keyboard19 }
            })
            Set-ComProperty $script:Ws.Range('A1') 'Value2' ([double]46027)
            Set-ComProperty $script:Ws.Range('A2') 'Value2' ([double]545 / 1440)
            Select-Cell 'A1'
            [void](State "$cul-set" 'COM set-up' { @{ cells = (Dates-Now); crop = (Crop-Cells 'A1' 'B2' "$cul-set-x4") } })
            $k = Ctrl-Char '#'
            Guarded-State "$cul-hash" "A1: $k" { @{ selection = (Selection-Now); cells = (Dates-Now); crop = (Crop-Cells 'A1' 'B2' "$cul-hash-x4") } }
            $mv = Keys '{DOWN}' 300
            $k = Ctrl-Char '@'
            Guarded-State "$cul-at" "$mv; A2: $k" { @{ selection = (Selection-Now); cells = (Dates-Now); crop = (Crop-Cells 'A1' 'B2' "$cul-at-x4") } }
        }
    }
    finally {
        if ($script:Xl) { Stop-OwnExcel }
        $now = [string](Get-ItemProperty $intlKey).LocaleName
        if ($now -ne $orig) { Set-Culture -CultureInfo $orig; Start-Sleep -Seconds 2 }
        $after = Join-Path $runDir 'international-after-19.reg'
        & reg.exe export 'HKCU\Control Panel\International' $after /y | Out-Null
        $b = @(Get-Content (Join-Path $runDir 'international-before.reg')); $a = @(Get-Content $after)
        $diff = @(Compare-Object $b $a | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" })
        $script:Restored19 = [ordered]@{ setBackTo = $orig; wasAtTheEnd = $now; regional = (Regional-Now); differencesFromTheExportBefore = $diff }
        Say "  19: culture set back to $orig (it was $now); differences from the export before: $($diff.Count)"
    }
    [void](State 'restored' "Set-Culture $orig" { @{ restored = $script:Restored19 } })
} 'uk' 240

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
