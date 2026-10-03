<#
.SYNOPSIS
    docs/specs/exsheet/verify-on-windows-14.md, Part A: when a formatting key widens a column that is no
    longer at the standard width, what else widens a column, how ja-JP shows mmm, what each cell records
    for itself under the edge model (read from the saved .xlsx), how Fills and lines meet on a gridline and
    the dash lengths at other zooms, a formatted cell while it is edited, and 0;[Red]@. ADR-0071's
    "Readings until the fourteenth Windows run".

        powershell -File cell-format-14.ps1 -Case 0                  the environment, and the keyboard checked
        powershell -File cell-format-14.ps1 -Case 1,2,4              cases of the procedure's tables
        powershell -File cell-format-14.ps1 -Case 5 -Pass b          a second pass of a case (files 5-b-...)
        powershell -File cell-format-14.ps1 -Case 3 -Culture en-US   cases under another regional format: Set-Culture
                                                                     before the first Excel starts, and the format
                                                                     the run began with set back at the end
        powershell -File cell-format-14.ps1 -Explore <steps>         for writing cases (nothing recorded)

    This is the twelfth run's cell-format-12.ps1 (verification/2026-10-01-windows-excel-12/), copied as the
    procedure asks. Its keys, mouse, pictures, pixel readings, COM reads, Format Cells helpers and the
    edge read from both cells are unchanged; its cases are replaced by the fourteenth run's, with the
    helpers they need added: the width read after every key, the ribbon's Fill Colour button (the
    eleventh run's ribbon search), the workbook saved as .xlsx and each named cell's own record read from
    the file, a dash's runs along a line, and -Culture for the cases under en-US and ja-JP.

    Every key a case asks about is sent through SendInput (a virtual-key and a scan code per key, the
    scan code from the layout of Excel's window), and every click is the real mouse. COM only sets a case
    up, saves the workbook, and reads the result. The script starts an Excel of its own for every case
    and ends only that one: it never attaches to a running Excel, because one of the user's may be open.

    Each case: a new workbook with one sheet, Sheet1, maximised, at 100% zoom, A1 in view and selected;
    Excel's window switched to the English (UK) keyboard with the IME off. Colours are sampled as hex
    from the pictures, never from COM.

    Pictures are the windows' own rendering (PrintWindow, PW_RENDERFULLCONTENT): the screen copy comes
    back empty on this machine since 2026-09-29. A dialog or a popup of Excel's is a window of its own,
    so it is captured on its own and laid over the window at its place.

    Outputs, beside this script:
      cell-format-14.jsonl  one line per case: the set-up, per state what COM read and what the pixels say
      shots\                per state: the cells (A1 to about H16, with the headings), any dialog or popup
                            of Excel's, and the crops a case names (enlarged two or four times)
      xlsx\                 group 4: xl/worksheets/sheet1.xml and xl/styles.xml of each saved workbook
                            (the .xlsx itself stays on this machine: its properties name the account)
    The full pictures and the .xlsx files stay on this machine, in %LOCALAPPDATA%\exgrid-layer3\cell-format-14\.
#>
#Requires -Version 5.1
param([string[]]$Case, [string]$Pass = '', [int]$Zoom = 100, [string]$Explore = '', [string]$Culture = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Case) { $Case = @($Case | ForEach-Object { $_ -split ',' } | Where-Object { $_ }) }

. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel\excel-driver.ps1')
$script:Shots = Join-Path $PSScriptRoot 'shots'
$Log = Join-Path $PSScriptRoot 'cell-format-14.jsonl'
$Raw = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\cell-format-14'
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

# ---- Kept from the twelfth run's cases ----------------------------------------------------------

function Row-Level([int]$R) {
    $x = $script:Ws.Rows($R)
    return [ordered]@{ fill = ('pattern {0} color {1}' -f $x.Interior.Pattern, (Hex-Of $x.Interior.Color)); bold = $x.Font.Bold; italic = $x.Font.Italic; fontColour = (Hex-Of $x.Font.Color); borders = (Level-Borders $x) }
}
function Regional-Now {
    $i = Get-ItemProperty 'HKCU:\Control Panel\International'
    return [ordered]@{ localeName = [string]$i.LocaleName; sShortDate = [string]$i.sShortDate; sLongDate = [string]$i.sLongDate; sTimeFormat = [string]$i.sTimeFormat; sShortTime = [string]$i.sShortTime; iTLZero = [string]$i.iTLZero; s1159 = [string]$i.s1159; sDate = [string]$i.sDate; sCurrency = [string]$i.sCurrency }
}

# ---- Kept from the eleventh run: the ribbon -----------------------------------------------------

function Ribbon-Item([string]$Pattern, [string]$Type) {
    $r = [CellFormat.Ui]::FindAll([long]$script:Hwnd, $Pattern, $Type, 1, 20000, 20000)
    $i = @(G $r 'items'); if ($i.Count) { return $i[0] }; return $null
}

# ---- The fourteenth run: widths read after every key --------------------------------------------

# Column A's width and whether it is at the standard width, and what each named cell holds and shows.
function Width-Now([string[]]$WCells) {
    $col = $script:Ws.Range('A1').EntireColumn
    $o = [ordered]@{ standardWidth = [double]$script:Ws.StandardWidth; columnWidth = [double]$col.ColumnWidth; widthPoints = [double]$col.Width; useStandardWidth = $col.UseStandardWidth }
    foreach ($c in $WCells) {
        $r = $script:Ws.Range($c); $f = $r.Font
        $o[$c] = [ordered]@{ value = [string]$r.Formula; text = [string]$r.Text; numberFormat = (Get-NF $r); numberFormatLocal = [string]$r.NumberFormatLocal
            bold = $f.Bold; strikethrough = $f.Strikethrough; fill = ('pattern {0} color {1}' -f $r.Interior.Pattern, (Hex-Of $r.Interior.Color)) }
    }
    return $o
}
# A state after a key: the width and the cells, read as soon as Excel answers COM (an edit or a dialog
# the key opened is pictured first, then closed with Esc, as the twelfth run's Guarded-State does).
function Width-State([string]$WName, [string]$WDid, [string[]]$WCells = @('A1', 'A2'), [string]$WTo = 'C3') {
    Guarded-State $WName $WDid { @{ selection = (Selection-Now); width = (Width-Now $WCells); crop = (Crop-Cells 'A1' $WTo "$WName-x2" 8 2) } }
}
# A case that must run under one regional format: the script is run with -Culture, which sets it
# before the first Excel starts.
function Need-Culture([string]$Name) {
    $now = [string](Get-ItemProperty 'HKCU:\Control Panel\International').LocaleName
    if ($now -ne $Name) { throw "this case runs under $Name (run the script with -Culture $Name); the regional format is $now" }
}
function Culture-Read { return [ordered]@{ regional = (Regional-Now); international = (Read-International) } }

# ---- Group 1: a key on a column that has already widened ----------------------------------------

Def '1' 'rewiden' 'Column A at the standard width. A1 = 46000.5, A2 = 1234567.5 (COM, as Formulas)' 'Ctrl+# on A1 (the date key; # is a key of its own on the UK layout). Then Down, and Ctrl+Shift+$ on A2: column A''s width and UseStandardWidth after each key; A2''s text' 'A widens again to fit £1,234,567.50. The eleventh run''s case 20 hints that it does not: record' {
    Put 'A1' '46000.5'; Put 'A2' '1234567.5'; Select-Cell 'A1'
    Width-State 'set' 'COM set-up'
    $k = Ctrl-Char '#'
    Width-State 'hash' "A1: $k"
    $mv = Keys '{DOWN}' 300
    $k = Ctrl-Char '$'
    Width-State 'dollar' "$mv; A2: $k"
}
Def '2' 'rewiden' 'Column A at the standard width. 123456789012 typed into A1 with real keys and Enter (an entry that widens the column). Then A2 = 1234567.5 (COM)' 'Ctrl+Shift+$ on A2: column A''s width and UseStandardWidth after each key; A2''s text' 'A widens again (SH-26: a width widened by an entry is widened again)' {
    Select-Cell 'A1'
    Width-State 'set' 'a new workbook, A1 selected'
    $k = Keys '123456789012~' 800
    Width-State 'typed' "A1: $k"
    Put 'A2' '1234567.5'
    $at = [string]$script:Xl.ActiveCell.Address($false, $false)
    $note = "COM: A2 = 1234567.5; the active cell after Enter: $at"
    if ($at -ne 'A2') { Select-Cell 'A2'; $note += ' (A2 selected through COM)' }
    Width-State 'a2-set' $note
    $k = Ctrl-Char '$'
    Width-State 'dollar' "A2: $k"
}
$script:Names3 = [ordered]@{ '~' = 'tilde'; '!' = 'bang'; '@' = 'at'; '#' = 'hash'; '$' = 'dollar'; '%' = 'percent'; '^' = 'caret' }
Def '3' 'rewiden' 'Under en-US (Set-Culture before a new Excel; the script run with -Culture en-US): A1 = 1234.5, column A at the standard width' 'On A1, in this order: Ctrl+Shift+~, Ctrl+Shift+!, Ctrl+Shift+@, Ctrl+# (no Shift on the UK layout), Ctrl+Shift+$, Ctrl+Shift+%, Ctrl+Shift+^, each as the character on the UK layout: after each key, NumberFormat, the text, the width, UseStandardWidth' 'This repeats the eleventh run''s case 20 under en-US with the width read after each key. It settles why $ showed ######## there' {
    Need-Culture 'en-US'
    Put 'A1' '1234.5'; Select-Cell 'A1'
    $script:Culture3 = Culture-Read
    Guarded-State 'set' 'COM set-up' { @{ culture = $script:Culture3; width = (Width-Now @('A1')); crop = (Crop-Cells 'A1' 'C2' 'set-x2' 8 2) } }
    foreach ($ch in $script:Names3.Keys) {
        $k = Ctrl-Char $ch
        Width-State $script:Names3[$ch] "A1: $k" @('A1') 'C2'
    }
} 'uk' 240
Def '4' 'rewiden' 'Column A set to width 12 by hand (COM ColumnWidth = 12), then A1 = 1234567890.5' 'Ctrl+Shift+$ on A1: the width and the text' 'A stays 12 and shows ######## (run 12''s case 18)' {
    Set-ComProperty $script:Ws.Range('A:A') 'ColumnWidth' 12
    Put 'A1' '1234567890.5'; Select-Cell 'A1'
    Width-State 'set' 'COM set-up' @('A1') 'D2'
    $k = Ctrl-Char '$'
    Width-State 'dollar' "A1: $k" @('A1') 'D2'
}

# ---- Group 2: what else widens ------------------------------------------------------------------

Def '5' 'what-widens' 'A1 = 123456789 in 0.00E+00 (shows 1.23E+08), column A at the standard width. Pass b (added): a new workbook, and 123456789 typed into A1 with real keys and Enter, for the width that typing gives' 'Ctrl+Shift+~ (General) on A1: the width, the text' 'General widens as typing 123456789 would. Record what Excel shows if it does not widen' {
    if ($Pass -eq 'b') {
        Select-Cell 'A1'
        Width-State 'set' 'a new workbook, A1 selected' @('A1')
        $k = Keys '123456789~' 800
        Width-State 'typed' "A1: $k" @('A1')
        return
    }
    Put 'A1' '123456789'; Set-NF $script:Ws.Range('A1') '0.00E+00'; Select-Cell 'A1'
    Width-State 'set' 'COM set-up' @('A1')
    $k = Ctrl-Char '~'
    Width-State 'tilde' "A1: $k" @('A1')
}
Def '6' 'what-widens' 'A1 = 12345678 (General, fits at the standard width), column A at the standard width' 'Ctrl+B on A1: the width, the text' 'A bold number that no longer fits shows ####. A Font never widens. Record whether it fits bold at all' {
    Put 'A1' '12345678'; Select-Cell 'A1'
    Width-State 'set' 'COM set-up' @('A1')
    $k = Keys '^b'
    Width-State 'bold' "A1: $k" @('A1')
    $k = Park-Selection
    [void](State 'parked' $k { @{ selection = (Selection-Now); width = (Width-Now @('A1')); ink = (Ink-Of $script:Img 'A1'); crop = (Crop-Cells 'A1' 'C2' 'parked-x4') } })
}
Def '7' 'what-widens' 'A1 = 1234567.5 in 0.00 (shows 1234567.50, which does not fit), column A at the standard width. Pass b (added): the Number Format set on the empty A1 first and the value after it, because in pass a the Number Format set through COM on A1 holding the value widened column A to 9.73. Passes c and d (added): as b, with the three in another order (c: Ctrl+5, Ctrl+B, the Fill; d: the Fill, Ctrl+B, Ctrl+5)' 'Ctrl+B, then Ctrl+5 (strikethrough), then a yellow Fill from the ribbon (a click with the mouse on the face of Home''s Fill Colour button, which fills yellow in a new Excel): the width after each' 'no change from any of them' {
    if ($Pass -in 'b', 'c', 'd') { Set-NF $script:Ws.Range('A1') '0.00'; Put 'A1' '1234567.5' } else { Put 'A1' '1234567.5'; Set-NF $script:Ws.Range('A1') '0.00' }
    Select-Cell 'A1'
    Width-State 'set' 'COM set-up' @('A1')
    $order = switch ($Pass) { 'c' { @('strike', 'bold', 'fill') } 'd' { @('fill', 'bold', 'strike') } default { @('bold', 'strike', 'fill') } }
    foreach ($step in $order) {
        switch ($step) {
            'bold' { $k = Keys '^b'; Width-State 'bold' "A1: $k" @('A1') }
            'strike' { $k = Keys '^5'; Width-State 'strikethrough' "A1: $k" @('A1') }
            'fill' {
                $fill = Ribbon-Item '^Fill Colou?r$' 'SplitButton'
                if (-not $fill) { throw 'the Fill Colour button was not found' }
                $r = $fill['rect']
                # The face is the button's part left of its arrow (the arrow takes the right 22 px at this scale).
                $x = [int]($r[0] + ($r[2] - 22 - $r[0]) / 2); $y = [int](($r[1] + $r[3]) / 2)
                $d = "a click on the face of $($fill['name']) (rect $($r -join ','))" + ': ' + (Hand-Click $x $y)
                Start-Sleep -Milliseconds 500
                Park-Mouse
                Width-State 'fill' "A1: $d" @('A1')
            }
        }
    }
}
Def '8' 'what-widens' 'A1 = 1234567.5 (General), column A at the standard width' 'With the mouse: Ctrl+1, the Number tab, the category Number (2 decimal places), Use 1000 Separator ticked, OK: the width, the text' 'widens to fit 1,234,567.50, as the key does' {
    Put 'A1' '1234567.5'; Select-Cell 'A1'
    Width-State 'set' 'COM set-up' @('A1')
    $k = Open-FormatCells
    [void](State 'opened' $k { @{ dialog = (Dialog-State 2) } })
    Visit-Tab 'Number'
    $d = Click-Item (Item-Named (Dialog-Items) '^Number$' 'ListItem')
    Start-Sleep -Milliseconds 700
    [void](State 'category-number' $d { @{ dialog = (Dialog-State) } })
    $cb = Item-Named (Dialog-Items) '^Use 1000 Separator' 'CheckBox'
    if (-not $cb) { throw 'Use 1000 Separator was not found' }
    $d = Click-Item $cb
    Start-Sleep -Milliseconds 600
    [void](State 'separator-ticked' "$d (it was $($cb['toggle']))" { @{ dialog = (Dialog-State) } })
    $d = Click-Item (Item-Named (Dialog-Items 2) '^OK$' 'Button')
    Start-Sleep -Milliseconds 900
    $e = Wait-NoDialog
    $script:NoCom = $false
    $n = Until-Ready
    Width-State 'ok' "$d ($e Escapes for the dialog, $n for Excel)" @('A1')
} 'uk' 300
Def '9' 'what-widens' 'A1 = 1234567.5 (General), column A at the standard width' 'With the mouse: Ctrl+1, the Border tab, Outline, OK: the width' 'no change' {
    Put 'A1' '1234567.5'; Select-Cell 'A1'
    Width-State 'set' 'COM set-up' @('A1')
    $k = Open-FormatCells
    [void](State 'opened' $k { @{ dialog = (Dialog-State 2) } })
    Visit-Tab 'Border'
    $d = Click-Item (Item-Named (Dialog-Items) '^Outline$' 'Button')
    Start-Sleep -Milliseconds 700
    [void](State 'outline-clicked' $d { @{ dialog = (Dialog-State) } })
    $d = Click-Item (Item-Named (Dialog-Items 2) '^OK$' 'Button')
    Start-Sleep -Milliseconds 900
    $e = Wait-NoDialog
    $script:NoCom = $false
    $n = Until-Ready
    Guarded-State 'ok' "$d ($e Escapes for the dialog, $n for Excel)" { @{ selection = (Selection-Now); width = (Width-Now @('A1')); edges = (Read-Edges 'A1'); crop = (Crop-Cells 'A1' 'C3' 'ok-x2' 8 2) } }
} 'uk' 300

# ---- Group 3: mmm under ja-JP --------------------------------------------------------------------

$script:Codes10 = @('dd-mmm-yy', 'd-mmm-yy', 'mmm d, yyyy', 'mmmm', 'yyyy/mmm/dd')
function Dates5 {
    $o = [ordered]@{}
    for ($i = 1; $i -le 5; $i++) {
        $r = $script:Ws.Range("A$i")
        $o["A$i"] = [ordered]@{ asked = $script:Codes10[$i - 1]; value2 = [string](Get-ComProperty $r 'Value2'); numberFormat = (Get-NF $r); numberFormatLocal = [string]$r.NumberFormatLocal; text = [string]$r.Text }
    }
    return $o
}
Def '10' 'mmm' 'Under ja-JP (Set-Culture before a new Excel; the script run with -Culture ja-JP): column A widened to 20 first (COM). A1:A5 = the date 5 January 2026 (Value2 46027), formatted dd-mmm-yy, d-mmm-yy, mmm d, yyyy, mmmm, yyyy/mmm/dd (COM NumberFormat, en-US codes)' 'The text of each, and NumberFormatLocal' 'Built-in 15 shows 05-1-26 (run 12''s case 19). Whether the others show the month as a number, as 1月, or otherwise: record' {
    Need-Culture 'ja-JP'
    Set-ComProperty $script:Ws.Range('A:A') 'ColumnWidth' 20
    for ($i = 1; $i -le 5; $i++) { $r = $script:Ws.Range("A$i"); Set-ComProperty $r 'Value2' ([double]46027); Set-NF $r $script:Codes10[$i - 1] }
    $k = Park-Selection
    $script:Culture10 = Culture-Read
    [void](State 'set' "COM set-up; $k" { @{ culture = $script:Culture10; cells = (Dates5); crop = (Crop-Cells 'A1' 'A5' 'set-x2' 8 2) } })
} 'uk' 240
Def '11' 'mmm' 'As 10 (under ja-JP; column A at 20; A1:A5 = 5 January 2026, General), but each code typed into Format Cells, Custom: Ctrl+1, the Number tab and the category Custom with the mouse, the Type box clicked, its text selected (Home, Shift+End) and the code typed, OK with the mouse; then Down to the next cell' 'The text of each, and NumberFormatLocal' 'the same as 10' {
    Need-Culture 'ja-JP'
    Set-ComProperty $script:Ws.Range('A:A') 'ColumnWidth' 20
    for ($i = 1; $i -le 5; $i++) { Set-ComProperty $script:Ws.Range("A$i") 'Value2' ([double]46027) }
    Select-Cell 'A1'
    $script:Culture11 = Culture-Read
    [void](State 'set' 'COM set-up' { @{ culture = $script:Culture11; cells = (Dates5); crop = (Crop-Cells 'A1' 'A5' 'set-x2' 8 2) } })
    for ($i = 1; $i -le 5; $i++) {
        $code = $script:Codes10[$i - 1]
        $mv = if ($i -gt 1) { (Keys '{DOWN}' 300) + '; ' } else { '' }
        $k = Open-FormatCells
        $items = Dialog-Items 2
        if ((Selected-Tab $items) -ne 'Number') { $k += '; ' + (Click-Item (Item-Named $items '^Number$' 'TabItem')); Start-Sleep -Milliseconds 600 }
        $d = Click-Item (Item-Named (Dialog-Items) '^Custom$' 'ListItem')
        Start-Sleep -Milliseconds 700
        $box = Item-Named (Dialog-Items) '^Type' 'Edit'
        $how = if ($box) { Click-Item $box } else { Keys '%t' 400 }
        $t = Keys '{HOME}+{END}' 300
        $typed = Keys ($code -replace '([+^%~(){}\[\]])', '{$1}') 500
        [void](State "a$i-typed" "$mv$k; $d; the Type box: $how; $t; typed $typed" { @{ dialog = (Dialog-State) } })
        $ok = Click-Item (Item-Named (Dialog-Items 2) '^OK$' 'Button')
        Start-Sleep -Milliseconds 900
        $e = Wait-NoDialog
        $script:NoCom = $false
        $n = Until-Ready
        Guarded-State "a$i-ok" "$ok ($e Escapes for the dialog, $n for Excel)" { @{ selection = (Selection-Now); cells = (Dates5); crop = (Crop-Cells 'A1' 'A5' "a$i-ok-x2" 8 2) } }
    }
} 'uk' 420

# ---- Group 4: what each cell records, read from the saved .xlsx ---------------------------------

Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$script:Xlsx = Join-Path $PSScriptRoot 'xlsx'
if (-not (Test-Path $script:Xlsx)) { [void](New-Item -ItemType Directory $script:Xlsx) }
# The workbook saved through COM as .xlsx (xlOpenXMLWorkbook) on this machine, the case's name.
function Save-Xlsx {
    $p = Join-Path $Raw ('{0}.xlsx' -f $script:CaseId)
    if (Test-Path $p) { Remove-Item $p }
    [void]$script:Book.SaveAs($p, 51)
    return $p
}
# The file is read while Excel still holds it open (after SaveAs it keeps the workbook's file open), so
# it is opened for reading with every sharing allowed.
function Xlsx-Part([string]$XPath, [string]$Part) {
    $fs = New-Object IO.FileStream($XPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    $z = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Read)
    try { $sr = New-Object IO.StreamReader($z.GetEntry($Part).Open()); $t = $sr.ReadToEnd(); $sr.Close(); return $t } finally { $z.Dispose(); $fs.Dispose() }
}
function Column-Number([string]$Letters) { $n = 0; foreach ($ch in $Letters.ToCharArray()) { $n = $n * 26 + ([int]$ch - 64) }; return $n }
function Attributes-Of($Node) { if (-not $Node) { return $null }; return (@($Node.Attributes | ForEach-Object { '{0}="{1}"' -f $_.Name, $_.Value }) -join ' ') }
# What the file records: each named cell's s (from its own c element, else its row's s when the row has
# customFormat, else the style of the col element covering it, else 0), the cellXfs entry at that index
# and the border it points to; the rows named; every col element. The two parts are kept in xlsx\.
function Read-Xlsx([string]$XPath, [string[]]$XCells, [int[]]$XRows = @()) {
    $sheetText = Xlsx-Part $XPath 'xl/worksheets/sheet1.xml'; $stylesText = Xlsx-Part $XPath 'xl/styles.xml'
    [IO.File]::WriteAllText((Join-Path $script:Xlsx "$($script:CaseId)-sheet1.xml"), $sheetText, $Utf8)
    [IO.File]::WriteAllText((Join-Path $script:Xlsx "$($script:CaseId)-styles.xml"), $stylesText, $Utf8)
    $sheet = New-Object Xml.XmlDocument; $sheet.LoadXml($sheetText)
    $styles = New-Object Xml.XmlDocument; $styles.LoadXml($stylesText)
    $ns = New-Object Xml.XmlNamespaceManager $sheet.NameTable; $ns.AddNamespace('x', 'http://schemas.openxmlformats.org/spreadsheetml/2006/main')
    $sns = New-Object Xml.XmlNamespaceManager $styles.NameTable; $sns.AddNamespace('x', 'http://schemas.openxmlformats.org/spreadsheetml/2006/main')
    $xfs = @($styles.SelectNodes('/x:styleSheet/x:cellXfs/x:xf', $sns))
    $borders = @($styles.SelectNodes('/x:styleSheet/x:borders/x:border', $sns))
    $cols = @($sheet.SelectNodes('/x:worksheet/x:cols/x:col', $ns))
    $o = [ordered]@{ file = (Split-Path $XPath -Leaf); parts = @("xlsx/$($script:CaseId)-sheet1.xml", "xlsx/$($script:CaseId)-styles.xml")
        dimension = (Attributes-Of $sheet.SelectSingleNode('/x:worksheet/x:dimension', $ns)); sheetFormatPr = (Attributes-Of $sheet.SelectSingleNode('/x:worksheet/x:sheetFormatPr', $ns))
        cols = @($cols | ForEach-Object { Attributes-Of $_ }); rowsInFile = @($sheet.SelectNodes('/x:worksheet/x:sheetData/x:row', $ns)).Count
        borderCount = $borders.Count; cellXfsCount = $xfs.Count; rows = [ordered]@{}; cells = [ordered]@{} }
    foreach ($r in $XRows) { $o.rows["$r"] = Attributes-Of $sheet.SelectSingleNode("/x:worksheet/x:sheetData/x:row[@r='$r']", $ns) }
    foreach ($a in $XCells) {
        $m = [regex]::Match($a, '^([A-Z]+)(\d+)$'); $colN = Column-Number $m.Groups[1].Value; $rowN = [int]$m.Groups[2].Value
        $c = $sheet.SelectSingleNode("/x:worksheet/x:sheetData/x:row/x:c[@r='$a']", $ns)
        $s = 0; $from = 'nothing: the default (0)'
        if ($c) { $s = $(if ($c.HasAttribute('s')) { [int]$c.GetAttribute('s') } else { 0 }); $from = "its own c element ($(Attributes-Of $c))" }
        else {
            $rowNode = $sheet.SelectSingleNode("/x:worksheet/x:sheetData/x:row[@r='$rowN']", $ns)
            $colNode = $null; foreach ($k in $cols) { if ([int]$k.GetAttribute('min') -le $colN -and [int]$k.GetAttribute('max') -ge $colN) { $colNode = $k } }
            if ($rowNode -and $rowNode.GetAttribute('customFormat') -eq '1') { $s = [int]$rowNode.GetAttribute('s'); $from = "its row ($(Attributes-Of $rowNode))" }
            elseif ($colNode -and $colNode.HasAttribute('style')) { $s = [int]$colNode.GetAttribute('style'); $from = "its col element ($(Attributes-Of $colNode))" }
        }
        $xf = $xfs[$s]; $b = [int]$xf.GetAttribute('borderId')
        $o.cells[$a] = [ordered]@{ from = $from; s = $s; xf = $xf.OuterXml; borderId = $b; border = $borders[$b].OuterXml }
    }
    return $o
}
function Saved-State([string[]]$SCells, [int[]]$SRows = @()) {
    $p = Save-Xlsx
    $script:SavedPath = $p; $script:SavedCells = $SCells; $script:SavedRows = $SRows
    [void](State 'saved' "COM: the workbook saved as $(Split-Path $p -Leaf) (SaveAs, xlOpenXMLWorkbook)" { @{ file = (Read-Xlsx $script:SavedPath $script:SavedCells $script:SavedRows) } })
}
# Format Cells' Border tab: a preset clicked with the mouse, then OK with the mouse.
function Border-Preset([string]$Preset) {
    $k = Open-FormatCells
    [void](State 'opened' $k { @{ dialog = (Dialog-State 2) } })
    Visit-Tab 'Border'
    $d = Click-Item (Item-Named (Dialog-Items) "^$Preset$" 'Button')
    Start-Sleep -Milliseconds 700
    [void](State "$($Preset.ToLowerInvariant())-clicked" $d { @{ dialog = (Dialog-State) } })
    $d = Click-Item (Item-Named (Dialog-Items 2) '^OK$' 'Button')
    Start-Sleep -Milliseconds 900
    $e = Wait-NoDialog
    $script:NoCom = $false
    $n = Until-Ready
    return "$k; the Border tab; $Preset; $d ($e Escapes for the dialog, $n for Excel)"
}

Def '12' 'own-record' 'As the twelfth run''s case 1: C2''s left edge thin blue; E5''s right edge thick red, E5 = 1' 'Select E5, Ctrl+C, select B2, Ctrl+V (keys from A1: Down x4, Right x4, Ctrl+C, Up x3, Left x3, Ctrl+V). Save as .xlsx: B2''s and C2''s own border in the file; the line drawn' 'B2 records a thick red right; C2 still records its thin blue left; the thick red line is drawn' {
    Set-Edge 'C2' 7 1 2 $Blue; Set-Edge 'E5' 10 1 4 $Red; Put 'E5' '1'
    Paste-E5-On-B2
    Saved-State @('B2', 'C2', 'E5', 'F5', 'A2', 'B1', 'B3', 'D2')
}
Def '13' 'own-record' 'A1''s bottom edge thick black; A2 plain' 'Select A2 alone (the Selection parked at G14 first; then Ctrl+Home, Down), Ctrl+1, the Border tab (a click): a picture of the preview: does it show a top line? Then Cancel (a click). Added: the same over A1, whose own bottom the line is, for comparison; and the workbook saved as .xlsx' 'no top: the dialog shows A2''s own sides' {
    Set-Edge 'A1' 9 1 4 $Black
    [void](Park-Selection)
    Before-Op @('A1 bottom') @('A1:A2') 'A1' 'B3'
    $k = Keys '^{HOME}{DOWN}'
    [void](State 'a2-selected' $k { @{ selection = (Selection-Now) } })
    $k = Open-FormatCells
    [void](State 'a2-opened' $k { @{ dialog = (Dialog-State 2) } })
    Visit-Tab 'Border'
    $d = Click-Item (Item-Named (Dialog-Items 2) '^Cancel$' 'Button')
    Start-Sleep -Milliseconds 900
    $e = Wait-NoDialog; $script:NoCom = $false; $n = Until-Ready
    [void](State 'a2-cancelled' "$d ($e Escapes for the dialog, $n for Excel)" { @{ selection = (Selection-Now); edges = (Read-Edges 'A1:A2') } })
    $k = Keys '{UP}'
    $k2 = Open-FormatCells
    $items = Dialog-Items 2
    if ((Selected-Tab $items) -ne 'Border') { Visit-Tab 'Border' }
    [void](State 'a1-opened' "$k; $k2 (added)" { @{ dialog = (Dialog-State) } })
    $d = Click-Item (Item-Named (Dialog-Items 2) '^Cancel$' 'Button')
    Start-Sleep -Milliseconds 900
    $e = Wait-NoDialog; $script:NoCom = $false; $n = Until-Ready
    [void](State 'a1-cancelled' "$d ($e Escapes for the dialog, $n for Excel)" { @{ selection = (Selection-Now); edges = (Read-Edges 'A1:A2') } })
    Saved-State @('A1', 'A2')
} 'uk' 300
Def '14' 'own-record' 'Nothing set' 'Select rows 3:4 (A3, Shift+Space, Shift+Down), Ctrl+1, the Border tab, Inside (with the mouse), OK (with the mouse). Save as .xlsx: A3''s and A4''s left edges, XFD3''s right edge, B3''s bottom edge (COM and the file)' 'the line between rows 3 and 4 and between columns; XFD''s right set; A''s left not set' {
    $k = Keys '{DOWN 2}+{SPACE}+{DOWN}'
    [void](State 'rows-selected' $k { @{ selection = (Selection-Now) } })
    $d = Border-Preset 'Inside'
    After-Op $d @('A3 left', 'A4 left', 'A3 top', 'A3 bottom', 'A3 right', 'B3 bottom', 'B3 right', 'B4 bottom', 'XFD3 right', 'XFD4 right', 'XFD3 bottom', 'XFD3 left') @('A2:C5') 'A1' 'D6' {
        @{ rows3 = (Level-Borders $script:Ws.Rows(3)); rows4 = (Level-Borders $script:Ws.Rows(4)); rows3to4 = (Level-Borders $script:Ws.Range('3:4')) }
    }
    Far-State 'right' 1 16376 @('XFD3 right', 'XFD4 right', 'XFD3 bottom', 'XFC3 right', 'XFD3 left') 'XFB2' 'XFD5'
    Saved-State @('A3', 'A4', 'B3', 'B4', 'XFD3', 'XFD4', 'A2', 'A5', 'B2', 'B5') @(2, 3, 4, 5)
} 'uk' 300
Def '15' 'own-record' 'Nothing set' 'Select the whole Sheet (the corner box, a click), Ctrl+1, the Border tab, Inside (with the mouse), OK (with the mouse): A1''s left and top, XFD1''s right, B2''s top, A1048576''s bottom (COM). Added: the workbook saved as .xlsx' 'every side, including A''s left and XFD''s right. Record the top of row 1 and the bottom of row 1048576' {
    $a1 = Cell-Rect 'A1'
    $d = Hand-Click ([int]$a1.Left - 20) ([int]$a1.Top - 14)
    [void](State 'corner-box' "the corner box: $d" { @{ selection = (Selection-Now) } })
    $d = Border-Preset 'Inside'
    After-Op $d @('A1 left', 'A1 top', 'A1 right', 'A1 bottom', 'B1 top', 'B2 top', 'A2 left', 'XFD1 right', 'XFD1 top', 'A1048576 bottom', 'B1048576 bottom', 'A1048576 left', 'XFD1048576 right', 'XFD1048576 bottom') @('A1:C3') 'A1' 'D4' {
        @{ cells = (Level-Borders $script:Ws.Cells); columns1 = (Level-Borders $script:Ws.Columns(1)); columns16384 = (Level-Borders $script:Ws.Columns(16384)); rows1 = (Level-Borders $script:Ws.Rows(1)); rows1048576 = (Level-Borders $script:Ws.Rows(1048576)) }
    }
    Far-State 'bottom' 1048560 1 @('A1048576 bottom', 'B1048576 bottom', 'A1048576 left', 'A1048575 left', 'A1048576 top') 'A1048572' 'C1048576'
    Far-State 'right' 1 16376 @('XFD1 right', 'XFD1 top', 'XFD2 right', 'XFC1 top', 'XFD1 left') 'XFB1' 'XFD3'
    Far-State 'bottom-right' 1048560 16376 @('XFD1048576 right', 'XFD1048576 bottom') 'XFB1048572' 'XFD1048576'
    Saved-State @('A1', 'B1', 'B2', 'C3', 'XFD1', 'A1048576', 'XFD1048576', 'A2') @(1, 2, 1048576)
} 'uk' 360

# ---- Group 5: lines and Fills where two cells meet, and the dashes at other zooms -----------------

# A line's dashes along the bottom of a cell: for each pixel row from 4 above to 4 below the edge that
# has any dark pixel, the pattern over the cell's width less 2 px at each end, and its runs (+n on,
# -n off; on is luminance below 128).
function Pattern-Runs([string]$P) {
    $out = @(); $i = 0
    while ($i -lt $P.Length) {
        $on = ($P[$i] -eq [char]'#' -or $P[$i] -eq [char]'=')
        $j = $i; while ($j + 1 -lt $P.Length -and (($P[$j + 1] -eq [char]'#' -or $P[$j + 1] -eq [char]'=') -eq $on)) { $j++ }
        $out += ('{0}{1}' -f $(if ($on) { '+' } else { '-' }), ($j - $i + 1)); $i = $j + 1
    }
    return ($out -join ' ')
}
function Dash-Of([string]$A1) {
    $r = Cell-Rect $A1
    $y0 = IY $r.Bottom; $x0 = (IX $r.Left) + 2; $len = [int]($r.Right - $r.Left) - 4
    $rows = [ordered]@{}
    for ($d = -4; $d -le 4; $d++) {
        $p = [CellFormat.Px]::Pattern($script:Img, $true, $y0 + $d, $x0, $len)
        if ($p.IndexOf('#') -ge 0 -or $p.IndexOf('=') -ge 0) { $rows["$d"] = [ordered]@{ pattern = $p; runs = (Pattern-Runs $p); colours = @([CellFormat.Px]::Colours($script:Img, $true, $y0 + $d, $x0, $len, 4)) } }
    }
    return [ordered]@{ edge = "$A1 bottom"; at = $y0; from = $x0; length = $len; levels = "'#' luminance < 64, '=' < 128, '-' < 192, '.' otherwise"; rows = $rows }
}

Def '16' 'meeting' 'B2 filled yellow (#FFFF00), B3 filled light blue (#00B0F0), no borders. The Selection parked at G14 (keys)' 'The pixel row on the gridline between B2 and B3, and the column on the gridline between B2 and C2 (C2 unfilled)' 'the gridline between B2 and B3 is yellow (the upper cell''s); between B2 and C2 it is yellow too (a Fill covers its gridlines)' {
    Set-Fill 'B2' $Yellow; Set-Fill 'B3' 0xF0B000
    $k = Park-Selection
    [void](State 'set' "COM set-up; $k" { @{ fill = (Read-Fill 'B2', 'B3', 'C2'); ground = (Ground-Of 'B2', 'B3', 'C2', 'C3')
        across = @((Across-Edge $script:Img 'B2' 'bottom'), (Across-Edge $script:Img 'B2' 'right'), (Across-Edge $script:Img 'B2' 'top'), (Across-Edge $script:Img 'B2' 'left'), (Across-Edge $script:Img 'B3' 'right'), (Across-Edge $script:Img 'B3' 'bottom'), (Across-Edge $script:Img 'C2' 'bottom'))
        crop = (Crop-Cells 'A1' 'D4' 'set-x4') } })
}
Def '17' 'meeting' 'B2 filled yellow with a double bottom border, black (Double, Thick). The Selection parked at G14 (keys)' 'The three pixels across the gridline under B2' 'dark, then the gridline pixel, then dark. Record whether that middle pixel is white or yellow' {
    Set-Fill 'B2' $Yellow; Set-Edge 'B2' 9 -4119 4 $Black
    $k = Park-Selection
    [void](State 'set' "COM set-up; $k" { @{ edges = (Read-Edges 'B2'); shared = (Shared-Edges @('B2 bottom')); fill = (Read-Fill 'B2', 'B3'); ground = (Ground-Of 'B2', 'B3')
        across = @((Across-Edge $script:Img 'B2' 'bottom'), (Across-Edge $script:Img 'C2' 'bottom'), (Across-Edge $script:Img 'B2' 'right')); pattern = (Line-Pattern $script:Img 'B2' 'bottom' 40 6)
        crop = (Crop-Cells 'A1' 'D4' 'set-x4') } })
}
Def '18' 'meeting' 'B2 with a medium dashed bottom (Dash, Medium) and B4 with a dashed bottom (Dash, Thin), black. The Selection parked at G14 (keys). The zoom set through COM (ActiveWindow.Zoom), the window scrolled back to A1. Pass b (added): zoom 50%, 60%, 75% and 90%, which on this display at 150% draw at 0.75 to 1.35 device pixels per pixel of zoom 100% at 96 DPI' 'The dash lengths at zoom 100%, 125%, 175% and 200% (150% added)' '8 on below 150%, 9 from 150%. Record each zoom''s pattern' {
    Set-Edge 'B2' 9 -4115 -4138 $Black; Set-Edge 'B4' 9 -4115 2 $Black
    $k = Park-Selection
    $win = $script:Book.Windows.Item(1)
    $zooms = if ($Pass -eq 'b') { @(50, 60, 75, 90) } else { @(100, 125, 150, 175, 200) }
    foreach ($z in $zooms) {
        $win.Zoom = $z; $win.ScrollRow = 1; $win.ScrollColumn = 1
        $script:RectCache = @{}
        Start-Sleep -Milliseconds 500
        $script:Zoom18 = $z
        [void](State "zoom-$z" "$(if ($z -eq $zooms[0]) { "COM set-up; $k; " })COM: zoom $z%$(if ($z -eq 150 -or $Pass -eq 'b') { ' (added)' })" { @{ zoom = [int]$script:Book.Windows.Item(1).Zoom; edges = (Read-Edges 'B2', 'B4')
            b2 = (Dash-Of 'B2'); b4 = (Dash-Of 'B4'); crop = (Crop-Cells 'A1' 'C5' "zoom-$($script:Zoom18)-x4" 8 4) } })
    }
    $win.Zoom = 100
}

# ---- Group 6: a formatted cell while it is edited, and 0;[Red]@ ---------------------------------

# A cell's state for an edit: its text's ink, the lines across its bottom and right edges, and a crop.
# While the edit is open COM is not called: the places come from the reads made before it.
function Edit-Read([string]$ECell, [string]$EName) {
    return [ordered]@{ ink = (Ink-Of $script:Img $ECell); bottom = (Across-Edge $script:Img $ECell 'bottom'); right = (Across-Edge $script:Img $ECell 'right'); crop = (Crop-Cells 'A1' 'D4' "$EName-x4") }
}
function Edit-Case([string]$ECell) {
    $k = Park-Selection
    foreach ($c in 'A1', 'D4', $ECell) { [void](Cell-Rect $c) }
    [void](State 'set' "COM set-up; $k" { $o = Edit-Read 'B2' 'set'; $o.font = (Read-Font 'B2'); $o.fill = (Read-Fill 'B2'); $o.edges = (Read-Edges 'B2'); $o })
    $k = Keys '^{HOME}{DOWN}{RIGHT}' 500
    [void](State 'selected' $k { $o = Edit-Read 'B2' 'selected'; $o.selection = (Selection-Now); $o })
    $k = Keys '{F2}' 700
    $script:NoCom = $true
    [void](State 'editing' $k { Edit-Read 'B2' 'editing' })
    $k = Keys '{ESC}' 600
    $script:NoCom = $false
    $n = Until-Ready
    [void](State 'esc' "$k ($n more for Excel)" { $o = Edit-Read 'B2' 'esc'; $o.values = (Values-Of 'B2'); $o.selection = (Selection-Now); $o })
}
Def '19' 'editing' 'B2 = 12345: Font bold, italic, single underline, red; Fill yellow; a thick black bottom and right edge (COM). Read parked (the Selection at G14), then with B2 selected (Ctrl+Home, Down, Right)' 'F2 on B2. A picture with the edit open, then Esc: the edit''s ground, the text''s colour, weight, slant and underline, and whether the bottom and right lines still show while editing' 'yellow ground, red bold italic underlined text; the lines hidden under the edit. Record' {
    Put 'B2' '12345'
    $f = $script:Ws.Range('B2').Font
    Set-ComProperty $f 'Bold' $true; Set-ComProperty $f 'Italic' $true; Set-ComProperty $f 'Underline' 2; Set-ComProperty $f 'Color' $Red
    Set-Fill 'B2' $Yellow; Set-Edge 'B2' 9 1 4 $Black; Set-Edge 'B2' 10 1 4 $Black
    Edit-Case 'B2'
}
Def '20' 'editing' 'B2 = -5 in 0;[Red]-0, Font blue (COM). Read parked, then with B2 selected (Ctrl+Home, Down, Right)' 'F2 on B2. A picture, then Esc: the colour of -5 while edited' 'blue (the Font''s), not red' {
    Put 'B2' '-5'; Set-NF $script:Ws.Range('B2') '0;[Red]-0'; Set-ComProperty $script:Ws.Range('B2').Font 'Color' $Blue
    Edit-Case 'B2'
}
Def '21' 'editing' 'A1:A4 = 5, -5, 0, abc, each in 0;[Red]@ (COM). The Selection parked at G14 (keys)' 'Each cell''s text and colour (pixels)' '5 black, -5 and 0 black, abc red. Record' {
    Put 'A1' '5'; Put 'A2' '-5'; Put 'A3' '0'; Put 'A4' 'abc'
    foreach ($c in 'A1', 'A2', 'A3', 'A4') { Set-NF $script:Ws.Range($c) '0;[Red]@' }
    $k = Park-Selection
    [void](State 'set' "COM set-up; $k" { @{ cells = (Read-Font 'A1', 'A2', 'A3', 'A4'); ink = @((Ink-Of $script:Img 'A1'), (Ink-Of $script:Img 'A2'), (Ink-Of $script:Img 'A3'), (Ink-Of $script:Img 'A4')); crop = (Crop-Cells 'A1' 'B5' 'set-x4') } })
}

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
        time = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'); excelProcess = $script:OwnPid; keyboard = $keyboard; geometry = $script:Geo
        regionalFormat = [string](Get-ItemProperty 'HKCU:\Control Panel\International').LocaleName; international = (Read-International) }
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
# -Culture: the user's regional format set with Set-Culture before the first Excel starts (a new Excel
# reads it as it starts, the eleventh run's case 20), and the format the run began with set back at the
# end; the registry key is exported before and after and the two compared.
$script:IntlKey = 'HKCU:\Control Panel\International'
$script:CultureWas = [string](Get-ItemProperty $script:IntlKey).LocaleName
if ($Culture) {
    & reg.exe export 'HKCU\Control Panel\International' (Join-Path $Raw "international-before-$Culture.reg") /y | Out-Null
    if ($Culture -ne $script:CultureWas) { Set-Culture -CultureInfo $Culture; Start-Sleep -Seconds 2 }
    $child = (& powershell.exe -NoProfile -Command '(Get-Culture).Name' | Out-String).Trim()
    Say "regional format: Set-Culture $Culture (it was $($script:CultureWas)); a new PowerShell reads $child"
    Write-Line ([ordered]@{ case = 'culture'; step = 'set'; asked = $Culture; was = $script:CultureWas; time = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'); regional = (Regional-Now); cultureInANewPowerShell = $child })
}
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
    if ($Culture) {
        $now = [string](Get-ItemProperty $script:IntlKey).LocaleName
        if ($now -ne $script:CultureWas) { Set-Culture -CultureInfo $script:CultureWas; Start-Sleep -Seconds 2 }
        $before = Join-Path $Raw "international-before-$Culture.reg"; $after = Join-Path $Raw "international-after-$Culture.reg"
        & reg.exe export 'HKCU\Control Panel\International' $after /y | Out-Null
        $diff = @(Compare-Object @(Get-Content $before) @(Get-Content $after) | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" })
        $child = (& powershell.exe -NoProfile -Command '(Get-Culture).Name' | Out-String).Trim()
        Write-Line ([ordered]@{ case = 'culture'; step = 'set back'; setBackTo = $script:CultureWas; wasAtTheEnd = $now; time = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'); regional = (Regional-Now); cultureInANewPowerShell = $child; differencesFromTheExportBefore = $diff })
        Say "regional format set back to $($script:CultureWas) (it was $now at the end); a new PowerShell reads $child; differences from the export before: $($diff.Count)"
    }
}
