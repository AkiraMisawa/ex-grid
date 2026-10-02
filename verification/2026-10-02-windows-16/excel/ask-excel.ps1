<#
.SYNOPSIS
    docs/specs/exsheet/verify-on-windows-16.md on claude/exsheet-keyboard-field (bf13de1): Excel asked the
    same keys as ExSheet, with a real Japanese IME, where the procedure's Excel column is not "-": Part A's
    k1-k4, k6, k7, k8, k9, k10, k12, k15 and k16, and Part D's d1 and d3; and the additions k6x, k6y and
    k8x, as ExSheet's. Asked with real keys and the real mouse.

        powershell -File ask-excel.ps1 -Case 0                the environment (UI Automation used)
        powershell -File ask-excel.ps1 -Case k1,k2 -NoUia     cases, with no UI Automation at all

    This is the fifteenth run's script (verification/2026-10-01-windows-15/excel/ask-excel.ps1) with this
    run's cases, and as its pass b ran: no UI Automation between keys (in the fifteenth run its reads
    moved the IME's composition to the Name Box), the places of the Formula Bar, the Name Box and the
    status bar taken from the fifteenth run's i1 (checked again by case 0 of this run), and what Excel
    shows read from its pictures. Added: Alt+Tab to a window of the script's own and back (k15), a drag
    inside the Name Box's text (d3), a click on a column's heading (k10) and at a place in a cell (k16).

    Each case: an Excel of the script's own (New-Object, never an Excel already running), a new
    workbook, Book1, one sheet, Sheet1, at 100% zoom, maximised; A1:B4 a Table named Positions; D10
    selected; Excel's window switched to the Japanese keyboard (0x04110411, Microsoft IME) with the IME
    off. Then each state's keys and clicks; 600 ms; a picture (PrintWindow); 300 ms; another; the IME's
    open status and mode. Then Escape until Excel is Ready, and D10 (and D8, D11, D12) read through COM.

    The IME's candidate and prediction windows are the Windows Input Experience's (TextInputHost): no
    picture this machine gives holds them (the fifteenth run's report).

    Outputs, beside this script:
      ask-excel.jsonl   one line per case: the geometry, and per state what was read
      shots\            per state: the work area's top left (the account's initials blanked), crops of
                         the Formula Bar, Name Box, mode and cells
    The full pictures (600 ms, 900 ms) stay on this machine, in %LOCALAPPDATA%\exgrid-layer3\ask-excel-16\.
#>
#Requires -Version 5.1
param([string[]]$Case, [string]$Pass = '', [switch]$NoUia)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Case) { $Case = @($Case | ForEach-Object { $_ -split ',' } | Where-Object { $_ }) }

. (Join-Path $PSScriptRoot '..\..\2026-09-27-windows-excel\excel-driver.ps1')
$script:Shots = Join-Path $PSScriptRoot 'shots'
$Log = Join-Path $PSScriptRoot 'ask-excel.jsonl'
$Raw = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\ask-excel-16'
if (-not (Test-Path $Raw)) { [void](New-Item -ItemType Directory $Raw) }
if (-not (Test-Path $script:Shots)) { [void](New-Item -ItemType Directory $script:Shots) }
$Utf8 = New-Object Text.UTF8Encoding $false
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

Add-Type -Namespace Pointing -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
[DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint thread);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr LoadKeyboardLayout(string id, uint flags);
[DllImport("user32.dll")] public static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint flags);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern short VkKeyScan(char c);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
[DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
[DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint action, uint param, ref HIGHCONTRAST value, uint winIni);
[StructLayout(LayoutKind.Sequential)] public struct HIGHCONTRAST { public uint cbSize; public uint dwFlags; public IntPtr lpszDefaultScheme; }
'@

# Keys through SendInput, a key down and a key up with its virtual-key code and its scan code, as a
# keyboard sends them (the eighth run: SendKeys and keybd_event reached Excel garbled on this
# machine, SendInput intact).
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices; using System.Threading;
namespace Pointing {
public static class Keys {
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit, Size = 40)] struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public KEYBDINPUT ki; }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] i, int size);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern short VkKeyScan(char c);
    public static void One(ushort vk, bool up, bool extended) {
        var i = new INPUT[1]; i[0].type = 1; i[0].ki.wVk = vk; i[0].ki.wScan = (ushort)MapVirtualKey(vk, 0);
        i[0].ki.dwFlags = (up ? 2u : 0u) | (extended ? 1u : 0u);
        if (SendInput(1, i, Marshal.SizeOf(typeof(INPUT))) != 1) throw new Exception("SendInput failed: " + Marshal.GetLastWin32Error());
    }
    public static void Tap(ushort vk, bool extended) { One(vk, false, extended); Thread.Sleep(5); One(vk, true, extended); }
    // A named key with a modifier held (Shift 0x10, Ctrl 0x11): Shift+Down for "+{DOWN}".
    public static void Chord(ushort mod, ushort vk, bool extended) {
        One(mod, false, false); Thread.Sleep(20); Tap(vk, extended); Thread.Sleep(20); One(mod, true, false);
    }
    // A character, through this thread's keyboard layout, with Shift, Ctrl or Alt as it needs.
    public static void Char(char c) {
        short k = VkKeyScan(c);
        if (k == -1) throw new Exception("no key for character " + ((int)c).ToString("x4"));
        ushort vk = (ushort)(k & 0xff); bool shift = (k & 0x100) != 0, ctrl = (k & 0x200) != 0, alt = (k & 0x400) != 0;
        if (shift) { One(0x10, false, false); Thread.Sleep(5); }
        if (ctrl) { One(0x11, false, false); Thread.Sleep(5); }
        if (alt) { One(0x12, false, false); Thread.Sleep(5); }
        Tap(vk, false);
        if (alt) { Thread.Sleep(5); One(0x12, true, false); }
        if (ctrl) { Thread.Sleep(5); One(0x11, true, false); }
        if (shift) { Thread.Sleep(5); One(0x10, true, false); }
    }
}
}
'@

# Excel's windows, and what UI Automation reads in them, in C#: each read runs on a thread of its
# own with a time limit (on 2026-09-27 a UI Automation search in a dialog of Excel's hung for 50 s).
Add-Type -ReferencedAssemblies UIAutomationClient, UIAutomationTypes, WindowsBase -TypeDefinition @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Text; using System.Threading;
using System.Windows.Automation; using System.Windows.Automation.Text;
namespace Pointing {
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
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, StringBuilder l, uint flags, uint ms, out IntPtr result);
    // A window's own text (WM_GETTEXT), for an element that is a window of its own.
    static string WindowText(IntPtr h) {
        var sb = new StringBuilder(1024); IntPtr res;
        if (SendMessageTimeout(h, 0x000D, new IntPtr(1024), sb, 0x0002, 1000, out res) == IntPtr.Zero) return null;   // SMTO_ABORTIFHUNG
        return sb.ToString();
    }
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

    // Every element under a window, depth first, in the order UI Automation gives them: its type,
    // name, class, place, and whether it is selected or holds a value.
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
        var d = new Dictionary<string, object>();
        d["depth"] = depth; d["type"] = Short(e.Current.ControlType); d["name"] = e.Current.Name; d["class"] = e.Current.ClassName;
        var r = Rect(e); if (r != null) d["rect"] = r;
        object p;
        if (e.TryGetCurrentPattern(SelectionItemPattern.Pattern, out p)) d["selected"] = ((SelectionItemPattern)p).Current.IsSelected;
        if (e.TryGetCurrentPattern(ValuePattern.Pattern, out p)) d["value"] = ((ValuePattern)p).Current.Value;
        items.Add(d);
        if (depth >= maxDepth) return;
        var w = TreeWalker.RawViewWalker;
        var c = w.GetFirstChild(e);
        while (c != null && items.Count < maxCount) { Walk(c, depth + 1, maxDepth, maxCount, items); c = w.GetNextSibling(c); }
    }

    // An element of a window found by its name and class (the Formula Bar, the Name Box, the status
    // bar), once, while Excel is Ready. The search walks the raw view: the Name Box and the status
    // bar's panes are not in the control view that FindFirst searches.
    public static AutomationElement Find(long hwnd, string name, string cls, int ms) {
        AutomationElement found = null;
        Timed(() => {
            var w = TreeWalker.RawViewWalker; var stack = new Stack<AutomationElement>(); int seen = 0;
            stack.Push(AutomationElement.FromHandle(new IntPtr(hwnd)));
            while (stack.Count > 0 && seen < 6000) {
                var e = stack.Pop(); seen++;
                if (e.Current.Name == name && e.Current.ClassName == cls) { found = e; break; }
                var kids = new List<AutomationElement>(); var c = w.GetFirstChild(e);
                while (c != null) { kids.Add(c); c = w.GetNextSibling(c); }
                for (int i = kids.Count - 1; i >= 0; i--) stack.Push(kids[i]);
            }
            return null;
        }, ms);
        return found;
    }
    // What an element holds: its value, and through its TextPattern the selected text and the whole.
    // A combo box without a value of its own is read through its first edit.
    public static Dictionary<string, object> Read(AutomationElement e, int ms) {
        if (e == null) { var n = new Dictionary<string, object>(); n["error"] = "not found"; return n; }
        return Timed(() => {
            var o = new Dictionary<string, object>();
            o["type"] = Short(e.Current.ControlType); o["class"] = e.Current.ClassName;
            var r = Rect(e); if (r != null) o["rect"] = r;
            object p;
            AutomationElement v = e;
            if (!e.TryGetCurrentPattern(ValuePattern.Pattern, out p)) {
                var edit = e.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
                if (edit != null) { v = edit; o["readThrough"] = "its first Edit, class " + edit.Current.ClassName; }
            }
            if (v.TryGetCurrentPattern(ValuePattern.Pattern, out p)) o["value"] = ((ValuePattern)p).Current.Value;
            if (e.TryGetCurrentPattern(TextPattern.Pattern, out p)) {
                var tp = (TextPattern)p; var sel = new List<string>(); var at = new List<object>();
                var doc = tp.DocumentRange;
                foreach (var s in tp.GetSelection()) {
                    string st = s.GetText(-1); sel.Add(st);
                    // Where the selection lies: the text before its start, counted, gives its
                    // offset (a caret is a selection of length 0).
                    try {
                        var pre = doc.Clone();
                        pre.MoveEndpointByRange(TextPatternRangeEndpoint.End, s, TextPatternRangeEndpoint.Start);
                        int start = pre.GetText(-1).Length;
                        at.Add(new object[] { start, start + st.Length });
                    } catch (Exception x) { at.Add("not read: " + x.Message.Split('\n')[0]); }
                }
                o["selection"] = sel; o["selectionAt"] = at; o["document"] = doc.GetText(-1);
            }
            int nh = e.Current.NativeWindowHandle;
            if (nh != 0) { o["hwnd"] = (long)nh; o["windowText"] = WindowText(new IntPtr(nh)); }
            if (v != e && v.Current.NativeWindowHandle != 0) o["editWindowText"] = WindowText(new IntPtr(v.Current.NativeWindowHandle));
            // The text: the value where there is one, else the TextPattern's whole.
            o["text"] = o.ContainsKey("value") ? o["value"] : (o.ContainsKey("document") ? o["document"] : null);
            return o;
        }, ms);
    }
    // The names of an element's children, in order (the status bar's panes: its first is the mode).
    public static Dictionary<string, object> Children(AutomationElement e, int max, int ms) {
        if (e == null) { var n = new Dictionary<string, object>(); n["error"] = "not found"; return n; }
        return Timed(() => {
            var names = new List<string>(); var o = new Dictionary<string, object>(); o["names"] = names;
            var w = TreeWalker.RawViewWalker; var c = w.GetFirstChild(e);
            while (c != null && names.Count < max) { names.Add(c.Current.Name); c = w.GetNextSibling(c); }
            return o;
        }, ms);
    }
    // The element with the keyboard focus, and the top-level window it lies in.
    public static Dictionary<string, object> Focused(int ms) {
        return Timed(() => {
            var o = new Dictionary<string, object>();
            var f = AutomationElement.FocusedElement;
            o["type"] = Short(f.Current.ControlType); o["class"] = f.Current.ClassName; o["processId"] = f.Current.ProcessId;
            var w = TreeWalker.RawViewWalker; var e = f; var root = AutomationElement.RootElement;
            for (int i = 0; i < 40; i++) { var p = w.GetParent(e); if (p == null || p == root) break; e = p; }
            o["topLevelHwnd"] = (long)e.Current.NativeWindowHandle; o["topLevelClass"] = e.Current.ClassName;
            return o;
        }, ms);
    }
}
}
'@

# The IME of Excel's focused window: its open status (IMC_GETOPENSTATUS, 5) and conversion mode
# (IMC_GETCONVERSIONMODE, 1), through WM_IME_CONTROL (0x283) to the default IME window, with a time
# limit (Excel may be busy).
Add-Type -TypeDefinition @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices;
namespace Pointing {
public static class Ime {
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct GUITHREADINFO { public int cbSize; public int flags; public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret; public RECT rcCaret; }
    [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint thread, ref GUITHREADINFO info);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("imm32.dll")] static extern IntPtr ImmGetDefaultIMEWnd(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint ms, out IntPtr result);
    public static string State(IntPtr top) {
        uint pid; uint tid = GetWindowThreadProcessId(top, out pid);
        var gi = new GUITHREADINFO(); gi.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
        IntPtr focus = GetGUIThreadInfo(tid, ref gi) && gi.hwndFocus != IntPtr.Zero ? gi.hwndFocus : top;
        var cls = new System.Text.StringBuilder(128); GetClassName(focus, cls, 128);
        IntPtr ime = ImmGetDefaultIMEWnd(focus);
        if (ime == IntPtr.Zero) return "focus " + cls + ": no default IME window";
        IntPtr open, mode;
        if (SendMessageTimeout(ime, 0x283, new IntPtr(5), IntPtr.Zero, 2, 1000, out open) == IntPtr.Zero) return "focus " + cls + ": the IME window did not answer";
        SendMessageTimeout(ime, 0x283, new IntPtr(1), IntPtr.Zero, 2, 1000, out mode);
        long m = (long)mode;
        var names = new List<string>();
        if ((m & 1) != 0) names.Add((m & 2) != 0 ? "katakana" : "hiragana"); else names.Add("alphanumeric");
        names.Add((m & 8) != 0 ? "full-width" : "half-width");
        if ((m & 0x10) != 0) names.Add("romaji");
        return string.Format("open={0} mode=0x{1:x} ({2}); focus {3}", (long)open != 0, m, string.Join(", ", names), cls);
    }
}
}
'@

# The pixel readings, in C# (from the eighth run's range-finder.ps1: Mode, EdgeReport, Diff).
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Pointing {
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
    static int R(int c) { return (c >> 16) & 255; }
    static int G(int c) { return (c >> 8) & 255; }
    static int B(int c) { return c & 255; }
    public static int Dist(int a, int b) {
        if (a < 0 || b < 0) return 999;
        return Math.Max(Math.Abs(R(a) - R(b)), Math.Max(Math.Abs(G(a) - G(b)), Math.Abs(B(a) - B(b))));
    }
    public static int Sat(int c) {
        if (c < 0) return 0;
        int mx = Math.Max(R(c), Math.Max(G(c), B(c))), mn = Math.Min(R(c), Math.Min(G(c), B(c)));
        return mx - mn;
    }
    public static double Hue(int c) {
        double r = R(c) / 255.0, g = G(c) / 255.0, b = B(c) / 255.0;
        double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)), d = mx - mn;
        if (d == 0) return 0;
        double h;
        if (mx == r) h = ((g - b) / d) % 6; else if (mx == g) h = (b - r) / d + 2; else h = (r - g) / d + 4;
        h *= 60; if (h < 0) h += 360;
        return h;
    }
    static double HueDiff(int a, int b) { double d = Math.Abs(Hue(a) - Hue(b)); return d > 180 ? 360 - d : d; }

    static List<object> Top(Dictionary<int, int> counts, int n) {
        var l = new List<KeyValuePair<int, int>>(counts);
        l.Sort((a, b) => b.Value.CompareTo(a.Value));
        var o = new List<object>();
        for (int i = 0; i < l.Count && i < n; i++) o.Add(Hex(l[i].Key) + ":" + l[i].Value);
        return o;
    }
    static int ArgMax(Dictionary<int, int> counts, out int n) {
        int best = -1; n = 0;
        foreach (var kv in counts) if (kv.Value > n) { n = kv.Value; best = kv.Key; }
        return best;
    }

    // The most frequent colour in a box, x0..x1-1, y0..y1-1.
    public static int Mode(Img img, int x0, int y0, int x1, int y1) {
        var c = new Dictionary<int, int>();
        for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) { int p = img.At(x, y); if (p < 0) continue; int n; c.TryGetValue(p, out n); c[p] = n + 1; }
        int m; return ArgMax(c, out m);
    }
    public static int CountNear(Img img, int x0, int y0, int x1, int y1, int colour, int tol) {
        int n = 0;
        for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) if (Dist(img.At(x, y), colour) <= tol) n++;
        return n;
    }

    // One pixel of an edge: t runs along the edge, d is the distance inside it (negative: outside).
    static int Edge(Img img, string side, int L, int T, int R_, int B_, int t, int d) {
        switch (side) {
            case "top": return img.At(t, T + d);
            case "bottom": return img.At(t, B_ - d);
            case "left": return img.At(L + d, t);
            default: return img.At(R_ - d, t);
        }
    }

    // One edge of a cell: the pixels 1 to 3 px inside it (offsets), counted three ways (all of them;
    // those neither the ground nor the gridline; those that changed since the "before" shot), and
    // for the colour found, how much of the edge it covers at each distance from -2 to 6 and in how
    // many runs (a dashed line is many runs).
    public static Dictionary<string, object> EdgeReport(Img open, Img before, string side, int L, int T, int R_, int B_,
                                                      int[] offsets, int ground, int grid, int tol, int trim) {
        bool horizontal = side == "top" || side == "bottom";
        int t0 = (horizontal ? L : T) + trim, t1 = (horizontal ? R_ : B_) - trim;
        var all = new Dictionary<int, int>(); var cand = new Dictionary<int, int>(); var changed = new Dictionary<int, int>();
        int samples = 0;
        foreach (int d in offsets) {
            for (int t = t0; t < t1; t++) {
                int c = Edge(open, side, L, T, R_, B_, t, d); if (c < 0) continue;
                int b = before == null ? -1 : Edge(before, side, L, T, R_, B_, t, d);
                samples++;
                int n;
                all.TryGetValue(c, out n); all[c] = n + 1;
                if (Dist(c, ground) > tol && Dist(c, grid) > tol) { cand.TryGetValue(c, out n); cand[c] = n + 1; }
                if (b >= 0 && Dist(c, b) > tol) { changed.TryGetValue(c, out n); changed[c] = n + 1; }
            }
        }
        int outlineN, changedN;
        int outline = ArgMax(cand, out outlineN);
        int changedMode = ArgMax(changed, out changedN);
        var r = new Dictionary<string, object>();
        r["samples"] = samples;
        r["inside1to3"] = Hex(outline); r["inside1to3Count"] = outlineN;
        r["changed"] = Hex(changedMode); r["changedCount"] = changedN;
        r["histogram"] = Top(all, 6);
        r["candidates"] = Top(cand, 4);
        int mid = (t0 + t1) / 2;
        var prof = new List<object>(); var profB = new List<object>();
        for (int d = -4; d <= 6; d++) { prof.Add(Hex(Edge(open, side, L, T, R_, B_, mid, d))); if (before != null) profB.Add(Hex(Edge(before, side, L, T, R_, B_, mid, d))); }
        r["profile"] = prof; r["profileBefore"] = profB;
        // The line itself: the saturated colour most often found across the edge, 3 px either side.
        var line = new Dictionary<int, int>(); var lineAt = new Dictionary<int, int>();
        for (int d = -3; d <= 3; d++) {
            int here = 0;
            for (int t = t0; t < t1; t++) {
                int c = Edge(open, side, L, T, R_, B_, t, d);
                if (Sat(c) > 60) { int n; line.TryGetValue(c, out n); line[c] = n + 1; here++; }
            }
            lineAt[d] = here;
        }
        int lineN; int lineColour = ArgMax(line, out lineN);
        r["line"] = Hex(lineColour); r["lineCount"] = lineN; r["lineColours"] = Top(line, 4);
        if (lineColour >= 0) {
            var at = new List<object>();
            for (int d = -3; d <= 3; d++) {
                int n = 0;
                for (int t = t0; t < t1; t++) if (Dist(Edge(open, side, L, T, R_, B_, t, d), lineColour) <= tol) n++;
                at.Add(d + ":" + n);
            }
            r["lineAt"] = at;
            // Along the edge, at the distance where the line is densest: in how many runs (a dashed
            // line is many).
            int bestD = 0, bestN = -1;
            for (int d = -3; d <= 3; d++) {
                int n = 0;
                for (int t = t0; t < t1; t++) if (Dist(Edge(open, side, L, T, R_, B_, t, d), lineColour) <= tol) n++;
                if (n > bestN) { bestN = n; bestD = d; }
            }
            int runs = 0, longest = 0, cur = 0; bool inRun = false;
            for (int t = t0; t < t1; t++) {
                bool on = Dist(Edge(open, side, L, T, R_, B_, t, bestD), lineColour) <= tol;
                if (on) { cur++; if (!inRun) { runs++; inRun = true; } if (cur > longest) longest = cur; } else { inRun = false; cur = 0; }
            }
            r["lineBestOffset"] = bestD; r["lineRuns"] = runs; r["lineLongestRun"] = longest;
        }
        int colour = changedMode >= 0 ? changedMode : outline;
        if (colour >= 0) {
            var cover = new List<object>(); int bestD = 0; int bestN = -1;
            for (int d = -2; d <= 6; d++) {
                int n = 0;
                for (int t = t0; t < t1; t++) if (Dist(Edge(open, side, L, T, R_, B_, t, d), colour) <= tol) n++;
                cover.Add(d + ":" + n);
                if (n > bestN) { bestN = n; bestD = d; }
            }
            r["coverOf"] = Hex(colour); r["cover"] = cover; r["length"] = t1 - t0;
            int runs = 0; bool inRun = false; int longest = 0, cur = 0;
            for (int t = t0; t < t1; t++) {
                bool on = Dist(Edge(open, side, L, T, R_, B_, t, bestD), colour) <= tol;
                if (on) { cur++; if (!inRun) { runs++; inRun = true; } if (cur > longest) longest = cur; } else { inRun = false; cur = 0; }
            }
            r["bestOffset"] = bestD; r["runsAtBest"] = runs; r["longestRun"] = longest;
        }
        return r;
    }

    // Text, left to right, as the eighth run read it (range-finder.ps1, TextByDarkest): each column
    // stands for the darkest pixel in it (the core of a stroke wears the text's colour; ClearType's
    // coloured fringes are lighter). A column is "colour" when that pixel is saturated (max - min >
    // thr), "dark" when it is not and is dark, else empty. Neighbouring columns of one kind (and, for
    // colour, within 30 degrees of hue) make a run; a gap of up to `gap` empty columns is bridged.
    // Runs of colour two columns wide or less are ClearType fringes beside dark strokes, counted but
    // not listed.
    public static Dictionary<string, object> TextByDarkest(Img img, int x0, int y0, int x1, int y1, int thr, int ox, int gap) {
        return TextRuns(img, x0, y0, x1, y1, thr, ox, gap, false);
    }
    // The same for any ground, light or dark: each column's ground is its most frequent pixel, and
    // the column stands for the pixel furthest from that ground. A column is "colour" when that pixel
    // is saturated, "plain" when it is not but lies more than 60 from the ground, else empty.
    public static Dictionary<string, object> TextByContrast(Img img, int x0, int y0, int x1, int y1, int thr, int ox, int gap) {
        return TextRuns(img, x0, y0, x1, y1, thr, ox, gap, true);
    }
    static int ColumnMode(Img img, int x, int y0, int y1) {
        var c = new Dictionary<int, int>();
        for (int y = y0; y < y1; y++) { int p = img.At(x, y); if (p < 0) continue; int n; c.TryGetValue(p, out n); c[p] = n + 1; }
        int m; return ArgMax(c, out m);
    }
    static Dictionary<string, object> TextRuns(Img img, int x0, int y0, int x1, int y1, int thr, int ox, int gap, bool contrast) {
        var runs = new List<object>(); int fringes = 0;
        string kind = null; int start = -1, last = -1, refColour = -1; var counts = new Dictionary<int, int>(); var grounds = new Dictionary<int, int>();
        Action close = () => {
            if (kind == null) return;
            int width = last - start + 1;
            if (kind == "colour" && width <= 2) { fringes++; }
            else {
                int m; int mode = ArgMax(counts, out m); int gm; int g = ArgMax(grounds, out gm);
                var r = new Dictionary<string, object>();
                r["kind"] = kind; r["x0"] = start + ox; r["x1"] = last + ox; r["colour"] = Hex(mode); r["columns"] = m + "/" + counts.Count;
                if (contrast) r["ground"] = Hex(g);
                runs.Add(r);
            }
            kind = null; counts = new Dictionary<int, int>(); grounds = new Dictionary<int, int>();
        };
        for (int x = x0; x < x1; x++) {
            int pick = -1; string k = null; int ground = -1;
            if (contrast) {
                ground = ColumnMode(img, x, y0, y1); int far = -1;
                for (int y = y0; y < y1; y++) { int c = img.At(x, y); if (c < 0) continue; int d = Dist(c, ground); if (d > far) { far = d; pick = c; } }
                k = pick < 0 ? null : Sat(pick) > thr ? "colour" : far > 60 ? "plain" : null;
            } else {
                int lum = 999;
                for (int y = y0; y < y1; y++) { int c = img.At(x, y); if (c < 0) continue; int l = (R(c) * 30 + G(c) * 59 + B(c) * 11) / 100; if (l < lum) { lum = l; pick = c; } }
                k = pick < 0 ? null : Sat(pick) > thr ? "colour" : lum < 140 ? "dark" : null;
            }
            if (k == null) { if (kind != null && x - last > gap) close(); continue; }
            bool same = kind == k && (k != "colour" || HueDiff(pick, refColour) < 30);
            if (!same) { close(); kind = k; start = x; refColour = pick; }
            last = x;
            int n; counts.TryGetValue(pick, out n); counts[pick] = n + 1;
            if (contrast) { grounds.TryGetValue(ground, out n); grounds[ground] = n + 1; }
        }
        close();
        // Runs of one kind and one colour with nothing else between them are one run.
        var merged = new List<object>();
        foreach (Dictionary<string, object> r in runs) {
            if (merged.Count > 0) {
                var q = (Dictionary<string, object>)merged[merged.Count - 1];
                if ((string)q["kind"] == (string)r["kind"] && ((string)q["colour"] == (string)r["colour"] || (string)q["kind"] != "colour")) {
                    q["x1"] = r["x1"]; q["merged"] = (q.ContainsKey("merged") ? (int)q["merged"] : 1) + 1; continue;
                }
            }
            merged.Add(r);
        }
        var o = new Dictionary<string, object>(); o["runs"] = merged; o["fringeRuns"] = fringes;
        return o;
    }

    // The ground behind text, left to right, as the eighth run read it (Grounds): each column stands
    // for its lightest pixel, which on a light ground is the ground whatever strokes cross it.
    // Columns whose ground differs from `ground` by more than tol, next to each other, make a run: a
    // highlight. Runs three columns wide or less (a caret, a gridline, a border) are counted but not
    // listed.
    public static Dictionary<string, object> Grounds(Img img, int x0, int y0, int x1, int y1, int ground, int tol, int ox) {
        return GroundRuns(img, x0, y0, x1, y1, ground, tol, ox, false);
    }
    // The same for any ground: each column stands for its most frequent pixel.
    public static Dictionary<string, object> GroundsByMode(Img img, int x0, int y0, int x1, int y1, int ground, int tol, int ox) {
        return GroundRuns(img, x0, y0, x1, y1, ground, tol, ox, true);
    }
    static Dictionary<string, object> GroundRuns(Img img, int x0, int y0, int x1, int y1, int ground, int tol, int ox, bool byMode) {
        var runs = new List<object>(); int narrow = 0;
        int start = -1, last = -1; var counts = new Dictionary<int, int>();
        Action close = () => {
            if (start < 0) return;
            if (last - start + 1 <= 3) narrow++;
            else {
                int m; int mode = ArgMax(counts, out m);
                var r = new Dictionary<string, object>();
                r["x0"] = start + ox; r["x1"] = last + ox; r["colour"] = Hex(mode); r["columns"] = m + "/" + (last - start + 1);
                runs.Add(r);
            }
            start = -1; counts = new Dictionary<int, int>();
        };
        for (int x = x0; x < x1; x++) {
            int g = -1;
            if (byMode) g = ColumnMode(img, x, y0, y1);
            else { int lum = -1; for (int y = y0; y < y1; y++) { int c = img.At(x, y); if (c < 0) continue; int l = (R(c) * 30 + G(c) * 59 + B(c) * 11) / 100; if (l > lum) { lum = l; g = c; } } }
            if (g < 0 || Dist(g, ground) <= tol) { close(); continue; }
            if (start < 0) start = x;
            last = x;
            int n; counts.TryGetValue(g, out n); counts[g] = n + 1;
        }
        close();
        var o = new Dictionary<string, object>(); o["ground"] = Hex(ground); o["runs"] = runs; o["narrowRuns"] = narrow;
        return o;
    }

    // Every saturated pixel in a box (max - min > thr), counted by colour: the n most frequent, and of
    // those seen at least `min` times the darkest, the lightest and the most saturated.
    public static Dictionary<string, object> SatColours(Img img, int x0, int y0, int x1, int y1, int thr, int n, int min) {
        var c = new Dictionary<int, int>(); int total = 0;
        for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) { int p = img.At(x, y); if (Sat(p) > thr) { total++; int k; c.TryGetValue(p, out k); c[p] = k + 1; } }
        var o = new Dictionary<string, object>(); o["pixels"] = total; o["top"] = Top(c, n);
        int dark = -1, light = -1, sat = -1, dl = 999, ll = -1, ss = -1;
        foreach (var kv in c) {
            if (kv.Value < min) continue;
            int p = kv.Key, l = (R(p) * 30 + G(p) * 59 + B(p) * 11) / 100;
            if (l < dl) { dl = l; dark = p; }
            if (l > ll) { ll = l; light = p; }
            if (Sat(p) > ss) { ss = Sat(p); sat = p; }
        }
        o["darkest"] = Hex(dark); o["lightest"] = Hex(light); o["mostSaturated"] = Hex(sat);
        return o;
    }

    // Pixels that differ between two shots in a box, and the box around them.
    public static Dictionary<string, object> Diff(Img a, Img b, int x0, int y0, int x1, int y1, int tol, int ox, int oy) {
        int n = 0, minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) {
            if (Dist(a.At(x, y), b.At(x, y)) > tol) { n++; if (x < minX) minX = x; if (y < minY) minY = y; if (x > maxX) maxX = x; if (y > maxY) maxY = y; }
        }
        var r = new Dictionary<string, object>();
        r["changed"] = n;
        if (n > 0) r["box"] = new int[] { minX + ox, minY + oy, maxX + ox + 1, maxY + oy + 1 };
        return r;
    }
}
}
'@

# A window of the script's own for Alt+Tab to go to (k15): a form on a thread of its own, with its own
# message loop. Opened and brought to the front before Excel's window comes to the front, so that it
# stands next in the order Alt+Tab follows; nothing of the user's is brought forward.
Add-Type -ReferencedAssemblies System.Windows.Forms, System.Drawing -TypeDefinition @'
using System; using System.Threading; using System.Windows.Forms; using System.Drawing;
namespace Pointing {
public static class AltTarget {
    static Form form; static Thread thread;
    public static IntPtr Open() {
        if (form != null) return form.Handle;
        var ready = new ManualResetEvent(false);
        thread = new Thread(() => {
            form = new Form(); form.Text = "exgrid-alttab-16"; form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(3200, 1500); form.Size = new Size(420, 260); form.BackColor = Color.FromArgb(230, 230, 230);
            form.Shown += (s, e) => ready.Set();
            Application.Run(form);
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        ready.WaitOne(5000);
        return form.Handle;
    }
    public static void Close() {
        if (form == null) return;
        try { form.Invoke((Action)(() => form.Close())); } catch { }
        thread.Join(3000); form = null; thread = null;
    }
}
}
'@
function Front-Window {
    $h = [ExcelDriver.Native]::GetForegroundWindow(); [uint32]$p = 0; [void][Pointing.Native]::GetWindowThreadProcessId($h, [ref]$p)
    $n = '?'; try { $n = (Get-Process -Id $p).ProcessName } catch { }
    $title = if ([int]$p -eq $script:OwnPid) { "Excel (this script's)" } elseif ($n -eq 'powershell') { 'a window of a PowerShell (the script''s Alt+Tab target)' } else { 'another window' }
    return ('{0}, process {1}' -f $title, $n)
}

# ---- Keys ---------------------------------------------------------------------------------------

$script:Named = @{ DOWN = @(0x28, 1); UP = @(0x26, 1); LEFT = @(0x25, 1); RIGHT = @(0x27, 1); HOME = @(0x24, 1); END = @(0x23, 1)
    DEL = @(0x2E, 1); DELETE = @(0x2E, 1); F2 = @(0x71, 0); F3 = @(0x72, 0); ESC = @(0x1B, 0); ENTER = @(0x0D, 0); TAB = @(0x09, 0); BS = @(0x08, 0); SPACE = @(0x20, 0); KEYC = @(0x43, 0); KEYV = @(0x56, 0) }
# SendKeys' notation, as the procedure writes keys: {NAME} or {NAME n} for a named key, +{NAME} for
# it with Shift held, ^{NAME} with Ctrl and ^+{NAME} with both, {c} for a character SendKeys
# reserves, ~ for Enter, % alone for a tap of Alt; any other character as itself.
function Type-Keys([string]$Spec, [int]$GapMs = 30) {
    $i = 0
    while ($i -lt $Spec.Length) {
        $c = $Spec[$i]
        $mods = @()
        $j0 = $i
        while ($j0 -lt $Spec.Length -and ($Spec[$j0] -eq '+' -or $Spec[$j0] -eq '^')) { $j0++ }
        if ($j0 -gt $i -and $j0 + 1 -lt $Spec.Length -and $Spec[$j0] -eq '{' -and $Spec.IndexOf('}', $j0 + 2) -gt $j0 + 2) {
            for ($m = $i; $m -lt $j0; $m++) { $mods += $(if ($Spec[$m] -eq '+') { 0x10 } else { 0x11 }) }
            $i = $j0; $c = $Spec[$i]
        }
        if ($c -eq '{') {
            $end = $Spec.IndexOf('}', $i + 2)
            $inner = $Spec.Substring($i + 1, $end - $i - 1)
            $i = $end + 1
            if ($inner.Length -eq 1 -and $mods.Count -eq 0) { [Pointing.Keys]::Char($inner[0]); Start-Sleep -Milliseconds $GapMs; continue }
            $parts = $inner -split ' '; $n = if ($parts.Count -gt 1) { [int]$parts[1] } else { 1 }
            $k = $script:Named[$parts[0].ToUpperInvariant()]
            if ($null -eq $k) { throw "no key named $($parts[0])" }
            for ($j = 0; $j -lt $n; $j++) {
                # The modifiers go down in the order written and come up in reverse, 20 ms apart.
                foreach ($m in $mods) { [Pointing.Keys]::One([uint16]$m, $false, $false); Start-Sleep -Milliseconds 20 }
                [Pointing.Keys]::Tap([uint16]$k[0], [bool]$k[1])
                for ($m = $mods.Count - 1; $m -ge 0; $m--) { Start-Sleep -Milliseconds 20; [Pointing.Keys]::One([uint16]$mods[$m], $true, $false) }
                Start-Sleep -Milliseconds $GapMs
            }
            continue
        }
        if ($c -eq '~') { [Pointing.Keys]::Tap(0x0D, $false) }
        elseif ($c -eq '%') { [Pointing.Keys]::Tap(0x12, $false) }
        else { [Pointing.Keys]::Char($c) }
        Start-Sleep -Milliseconds $GapMs
        $i++
    }
}
# The driver's Show-Excel, without maximising the window (two are arranged side by side), and with
# the Alt tap through SendInput. Excel is in front when any window of this run's Excel is.
function Show-Excel($xl) {
    $hwnd = $script:Hwnd
    [uint32]$frontPid = 0
    [void][ExcelDriver.Native]::GetWindowThreadProcessId([ExcelDriver.Native]::GetForegroundWindow(), [ref]$frontPid)
    if ($script:OwnPid -ne 0 -and $frontPid -eq $script:OwnPid) { return }
    [Pointing.Keys]::Tap(0x12, $false)
    if ([Pointing.Native]::IsIconic($hwnd)) { [void][ExcelDriver.Native]::ShowWindow($hwnd, 9) }   # SW_RESTORE
    [void][ExcelDriver.Native]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 400
    [void][ExcelDriver.Native]::GetWindowThreadProcessId([ExcelDriver.Native]::GetForegroundWindow(), [ref]$frontPid)
    if ($frontPid -ne $script:OwnPid) { throw 'Excel is not in the foreground; keys would go elsewhere.' }
}
function Send-Keys($xl, [string]$Keys, [int]$Settle = 250) {
    Show-Excel $xl
    Type-Keys $Keys
    Start-Sleep -Milliseconds $Settle
}
# Literal text for SendKeys: + ^ % ~ ( ) [ ] { } braced.
function Lit([string]$Text) { return ($Text -replace '([+^%~(){}\[\]])', '{$1}') }

# ---- The log ------------------------------------------------------------------------------------

function Write-Line($Row) { [IO.File]::AppendAllText($Log, (($Row | ConvertTo-Json -Compress -Depth 16) + "`n"), $Utf8) }
function Say([string]$Text) { Write-Host ('{0} {1}' -f (Get-Date -Format 'HH:mm:ss.fff'), $Text) }

# ---- Excel: one of this script's own, for each case --------------------------------------------

$script:Xl = $null
$script:OwnPid = 0
$script:Books = [ordered]@{}   # by role: Book1, Book2
$script:ExcelsAtStart = @(Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$script:OwnPids = @()

function Start-OwnExcel {
    $before = @(Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
    $xl = New-Object -ComObject Excel.Application
    [uint32]$p = 0
    [void][Pointing.Native]::GetWindowThreadProcessId([IntPtr][long]$xl.Hwnd, [ref]$p)
    if ($before -contains [int]$p) {
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl)
        throw "New-Object gave an Excel that was already running (process $p); nothing was changed in it"
    }
    $script:OwnPid = [int]$p
    $script:OwnPids += [int]$p
    $xl.Visible = $true
    $xl.DisplayAlerts = $false
    $script:Xl = $xl
    $script:Books = [ordered]@{}
    # A workbook Excel opened by itself (a recovered one) is never closed by this script: closing
    # a recovered workbook deletes its file. Its Excel is then ended by Stop-Process instead.
    $script:OpenedByItself = @($xl.Workbooks | ForEach-Object { [string]$_.Name })
    Say "own Excel: process $($script:OwnPid); Excel processes left alone: $($before -join ', '); workbooks it opened by itself: $($script:OpenedByItself.Count)"
    return $xl
}

function Stop-OwnExcel {
    if ($null -eq $script:Xl) { return }
    $others = @()
    try {
        $ours = @($script:Books.Values | ForEach-Object { [string]$_.Name })
        $others = @($script:Xl.Workbooks | ForEach-Object { [string]$_.Name } | Where-Object { $ours -notcontains $_ })
    } catch { }
    if ($others.Count -eq 0) {
        foreach ($k in @($script:Books.Keys)) { try { $script:Books[$k].Close($false) } catch { } }
        try { $script:Xl.Quit() } catch { }
    }
    $script:Books = [ordered]@{}
    try { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($script:Xl) } catch { }
    $script:Xl = $null
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    for ($i = 0; $i -lt 20; $i++) { if (-not (Get-Process -Id $script:OwnPid -ErrorAction SilentlyContinue)) { break }; Start-Sleep -Milliseconds 500 }
    if (Get-Process -Id $script:OwnPid -ErrorAction SilentlyContinue) { Stop-Process -Id $script:OwnPid -Force; Say "own Excel $($script:OwnPid) ended by Stop-Process (workbooks not its own: $($others.Count))" }
    else { Say "own Excel $($script:OwnPid) quit" }
}

# A new workbook with one sheet, Sheet1, at 100% zoom, A1:B4 a Table: headers Id and PV; R-1, R-2,
# R-3 under Id; 10, 20, 30 under PV.
function New-Book([string]$Role, [string]$Table) {
    $xl = $script:Xl
    $wb = $xl.Workbooks.Add()
    $wb.EnableAutoRecover = $false
    while ($wb.Worksheets.Count -gt 1) { $wb.Worksheets.Item($wb.Worksheets.Count).Delete() }
    $ws = $wb.Worksheets.Item(1)
    if ($ws.Name -ne 'Sheet1') { $ws.Name = 'Sheet1' }
    $ws.Range('A1').Value2 = 'Id'; $ws.Range('B1').Value2 = 'PV'
    $ws.Range('A2').Value2 = 'R-1'; $ws.Range('A3').Value2 = 'R-2'; $ws.Range('A4').Value2 = 'R-3'
    $ws.Range('B2').Value2 = 10; $ws.Range('B3').Value2 = 20; $ws.Range('B4').Value2 = 30
    $lo = $ws.ListObjects.Add(1, $ws.Range('A1:B4'), [Type]::Missing, 1)   # xlSrcRange, xlYes
    $lo.Name = $Table
    $script:Books[$Role] = $wb
    return $wb
}

# ---- The keyboard -------------------------------------------------------------------------------

function Get-ExcelLayout { [uint32]$p = 0; $t = [Pointing.Native]::GetWindowThreadProcessId($script:Hwnd, [ref]$p); return ('0x{0:x8}' -f [long][Pointing.Native]::GetKeyboardLayout($t)) }
function Get-OwnLayout { return ('0x{0:x8}' -f [long][Pointing.Native]::GetKeyboardLayout(0)) }
$script:FirstExcelLayout = $null
# Excel's window to the English (UK) keyboard, as Win+Space would; and this thread too, because a
# character becomes a key through the sending thread's layout (VkKeyScan), and Excel turns the key
# back into a character through its own.
function Set-EnglishKeyboard {
    $was = Get-ExcelLayout; $ownWas = Get-OwnLayout
    if ($null -eq $script:FirstExcelLayout) { $script:FirstExcelLayout = $was }
    Show-Excel $script:Xl
    [void][Pointing.Native]::PostMessage($script:Hwnd, 0x0050, [IntPtr]::Zero, [IntPtr]0x08090809)   # WM_INPUTLANGCHANGEREQUEST
    Start-Sleep -Milliseconds 600
    $uk = [Pointing.Native]::LoadKeyboardLayout('00000809', 1)   # KLF_ACTIVATE, this thread
    [void][Pointing.Native]::ActivateKeyboardLayout($uk, 0)
    $vk = [ordered]@{}
    foreach ($c in @('=', '+', '(', ')', '[', ']', ',', ':', '-', '1')) { $vk[$c] = ('0x{0:x3}' -f [int][Pointing.Native]::VkKeyScan([char]$c)) }
    return [ordered]@{ excelWas = $was; excelNow = Get-ExcelLayout; ownWas = $ownWas; ownNow = Get-OwnLayout; vkKeyScan = $vk }
}
function Restore-Keyboard {
    Show-Excel $script:Xl
    [void][Pointing.Native]::PostMessage($script:Hwnd, 0x0050, [IntPtr]::Zero, [IntPtr][long]([Convert]::ToInt64($script:FirstExcelLayout, 16)))
    Start-Sleep -Milliseconds 600
    [Pointing.Native]::keybd_event(0x1A, 0, 0, [UIntPtr]::Zero); [Pointing.Native]::keybd_event(0x1A, 0, 2, [UIntPtr]::Zero)   # VK_IME_OFF
    Start-Sleep -Milliseconds 200
    return [ordered]@{ restoredTo = $script:FirstExcelLayout; excelNow = Get-ExcelLayout }
}

# ---- Geometry, read through COM while Excel is Ready --------------------------------------------

function Cell-Rect($Win, $Ws, [string]$A1) {
    $r = $Ws.Range($A1); $p = $Win.ActivePane
    $l = $p.PointsToScreenPixelsX($r.Left); $t = $p.PointsToScreenPixelsY($r.Top)
    return [ordered]@{ Left = [int]$l; Top = [int]$t; Right = [int]$p.PointsToScreenPixelsX($r.Left + $r.Width); Bottom = [int]$p.PointsToScreenPixelsY($r.Top + $r.Height) }
}
$script:CellNames = @('A1', 'B1', 'C1', 'D1', 'A2', 'B2', 'C2', 'A3', 'B3', 'C3', 'A4', 'B4', 'C4', 'A5', 'B5', 'C5', 'B6', 'D8', 'D10', 'D11', 'D12', 'E10')
# Each window's place, its cells' places, and the UI Automation elements read later (found once
# here: a search mid-edit costs time the 600 ms does not have).
function Get-Geometry {
    $geo = [ordered]@{ windows = [ordered]@{} }
    $script:El = @{}
    foreach ($name in $script:Books.Keys) {
        $wb = $script:Books[$name]; $win = $wb.Windows.Item(1); $ws = $wb.Worksheets.Item('Sheet1')
        $h = [IntPtr][long]$win.Hwnd
        $w = New-Object ExcelDriver.Native+RECT; [void][ExcelDriver.Native]::GetWindowRect($h, [ref]$w)
        $cells = [ordered]@{}
        foreach ($a in $script:CellNames) { $cells[$a] = Cell-Rect $win $ws $a }
        $fb = $null; $nb = $null; $sb = $null
        if (-not $script:NoUia) {
            $fb = [Pointing.Ui]::Find([long]$h, 'Formula Bar', 'XLFormulaBarEditor', 20000)
            $nb = [Pointing.Ui]::Find([long]$h, 'Name Box', 'Edit', 20000)
            $sb = [Pointing.Ui]::Find([long]$h, 'Status Bar', 'NetUInetpane', 20000)
        }
        $script:El[$name] = @{ fb = $fb; nb = $nb; sb = $sb }
        $v = $win.VisibleRange
        $geo.windows[$name] = [ordered]@{
            workbook = [string]$wb.Name; hwnd = [long]$h; caption = [string]$win.Caption; rect = [ordered]@{ Left = $w.Left; Top = $w.Top; Right = $w.Right; Bottom = $w.Bottom }
            windowState = [int]$win.WindowState; zoom = [int]$win.Zoom; visible = [string]$v.Address($false, $false)
            table = [string]$ws.ListObjects.Item(1).Name; tableRange = [string]$ws.ListObjects.Item(1).Range.Address($false, $false)
            names = @($wb.Names | ForEach-Object { [string]$_.Name + $(if ($_.Visible) { '' } else { ' (hidden)' }) })
            activeCell = [string]$win.ActiveCell.Address($false, $false)
            dpi = [int][Pointing.Native]::GetDpiForWindow($h)
            cells = $cells
            formulaBar = $(if ($script:NoUia) { $script:Known.formulaBar } else { Rect-Of $fb })
            nameBox = $(if ($script:NoUia) { $script:Known.nameBox } elseif ($nb) { Rect-Of $nb } else { NameBox-Beside (Rect-Of $fb) })
            statusBar = $(if ($script:NoUia) { $script:Known.statusBar } else { Rect-Of $sb })
        }
    }
    $geo.activeWorkbook = [string]$script:Xl.ActiveWorkbook.Name
    $geo.activeCell = [string]$script:Xl.ActiveCell.Address($false, $false)
    $geo.excelLayout = Get-ExcelLayout
    return $geo
}
# Without UI Automation (-NoUia), the places UI Automation found for the Formula Bar, the Name Box
# and the status bar in this run's first case (i1, 22:28, Book1 maximised at 100% on this display; the
# same in e1): the clicks and the crops use them.
$script:Known = @{
    formulaBar = [ordered]@{ Left = 328; Top = 267; Right = 3840; Bottom = 324; class = 'XLFormulaBarEditor (the place found in i1; not searched for)' }
    nameBox = [ordered]@{ Left = 16; Top = 283; Right = 134; Bottom = 311; class = 'Edit (the place found in i1; not searched for)' }
    statusBar = [ordered]@{ Left = 0; Top = 2055; Right = 3840; Bottom = 2088; class = 'NetUInetpane (the place found in i1; not searched for)' } }
# UI Automation does not always find the Name Box (the ninth run, pass a). Its place is then taken
# beside the Formula Bar, where the ninth run's pass b found it in a maximised window: (16,283)-
# (134,311) with the Formula Bar's text at (328,267)-(…,324).
function NameBox-Beside($Fb) {
    if ($null -eq $Fb) { return $null }
    return [ordered]@{ Left = $Fb.Left - 312; Top = $Fb.Top + 16; Right = $Fb.Left - 194; Bottom = $Fb.Bottom - 13; class = 'Edit (not found this time; placed beside the Formula Bar as the ninth run found it)' }
}
function Rect-Of($El) {
    if ($null -eq $El) { return $null }
    $r = $El.Current.BoundingRectangle
    return [ordered]@{ Left = [int]$r.Left; Top = [int]$r.Top; Right = [int]$r.Right; Bottom = [int]$r.Bottom; class = [string]$El.Current.ClassName }
}

# Escape until Excel answers COM again (it refuses every call while an edit or a dialog is open).
function Until-Ready([int]$Tries = 8) {
    for ($i = 0; $i -lt $Tries; $i++) {
        Send-KeysRaw '{ESC}' 400
        # Workbooks.Count and Ready answer while the edit is still open (seen 13:01); these do not.
        try { [void]$script:Xl.ActiveWorkbook.Name; [void]$script:Xl.ActiveCell.Address($false, $false); if ($script:Xl.Ready) { return $i + 1 } } catch { }
    }
    throw 'Excel did not come back to Ready'
}
function Send-KeysRaw([string]$Keys, [int]$Settle = 400) { Send-Keys $null $Keys $Settle }

# ---- Screenshots --------------------------------------------------------------------------------

# One picture of the work area: every visible window of this run's Excel drawn by itself
# (PrintWindow), back to front, at its place. Returns the picture and the windows it holds.
$script:Area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
# Office's glow around a window is a set of windows of its own, drawn half transparent: left out.
$script:Shadows = @('MSO_BORDEREFFECT_WINDOW_CLASS')
function Grab-Scene {
    $wins = @([Pointing.Win]::Of($script:OwnPid))
    $bmp = New-Object Drawing.Bitmap $script:Area.Width, $script:Area.Height
    $g = [Drawing.Graphics]::FromImage($bmp); $g.Clear([Drawing.Color]::FromArgb(255, 64, 64, 64))
    for ($i = $wins.Count - 1; $i -ge 0; $i--) {
        $r = $wins[$i].rect; $w = $r[2] - $r[0]; $h = $r[3] - $r[1]
        if ($w -le 0 -or $h -le 0 -or $script:Shadows -contains $wins[$i].class) { continue }
        $one = New-Object Drawing.Bitmap $w, $h
        $og = [Drawing.Graphics]::FromImage($one); $hdc = $og.GetHdc()
        $ok = [Pointing.Native]::PrintWindow([IntPtr][long]$wins[$i].hwnd, $hdc, 2)   # PW_RENDERFULLCONTENT
        $og.ReleaseHdc($hdc); $og.Dispose()
        $wins[$i].printed = [bool]$ok
        if ($ok) { $g.DrawImage($one, $r[0] - $script:Area.Left, $r[1] - $script:Area.Top, $w, $h) }
        $one.Dispose()
    }
    $g.Dispose()
    return @{ bmp = $bmp; windows = $wins }
}
# The account's initials sit at the right of each title bar, left of the window's buttons.
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
function Save-Crop($Bmp, $Rect, [string]$Name, [int]$Pad = 0) {
    $l = [Math]::Max([int]$Rect.Left - $Pad - $script:Area.Left, 0); $t = [Math]::Max([int]$Rect.Top - $Pad - $script:Area.Top, 0)
    $r = [Math]::Min([int]$Rect.Right + $Pad - $script:Area.Left, $Bmp.Width); $b = [Math]::Min([int]$Rect.Bottom + $Pad - $script:Area.Top, $Bmp.Height)
    if ($r -le $l -or $b -le $t) { return $null }
    $crop = $Bmp.Clone((New-Object Drawing.Rectangle $l, $t, ($r - $l), ($b - $t)), $Bmp.PixelFormat)
    $crop.Save((Join-Path $script:Shots ($Name + '.png')), [Drawing.Imaging.ImageFormat]::Png); $crop.Dispose()
    return "shots/$Name.png"
}

# ---- One state of a case ------------------------------------------------------------------------

# A value of a dictionary, or nothing when it has no such key.
function G($D, [string]$K) { if ($null -ne $D -and (@($D.Keys) -contains $K)) { return $D[$K] }; return $null }
# A click as a hand makes it: the pointer moves from where it is in 15 steps of 15 ms, then presses for
# 100 ms.
function Hand-Click([int]$X, [int]$Y) {
    Show-Excel $script:Xl
    $from = [System.Windows.Forms.Cursor]::Position
    for ($i = 1; $i -le 15; $i++) { [void][ExcelDriver.Native]::SetCursorPos([int]($from.X + ($X - $from.X) * $i / 15), [int]($from.Y + ($Y - $from.Y) * $i / 15)); Start-Sleep -Milliseconds 15 }
    Start-Sleep -Milliseconds 100
    [ExcelDriver.Native]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 100
    [ExcelDriver.Native]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60
    return "the pointer moved there from ($($from.X), $($from.Y)) in 15 steps"
}
function Do-Step($Step) {
    switch ($Step.do) {
        'keys' { if ($Step.ContainsKey('gap')) { Show-Excel $script:Xl; Type-Keys $Step.keys $Step.gap; return "the keys $($Step.keys), $($Step.gap) ms apart" }; Send-Keys $script:Xl $Step.keys 0 }
        'click' {
            $c = $script:Geo.windows['Book1'].cells[$Step.cell]
            $x = [int]$c.Left + $Step.dx; $y = [int](($c.Top + $c.Bottom) / 2)
            $how = Hand-Click $x $y
            return "a click at ($x, $y), $($Step.dx) px right of $($Step.cell)'s left edge and half way down; $how"
        }
        'cell' {
            # The middle of a cell of either workbook's window.
            $c = $script:Geo.windows[$Step.book].cells[$Step.cell]
            $x = [int](($c.Left + $c.Right) / 2); $y = [int](($c.Top + $c.Bottom) / 2)
            $how = Hand-Click $x $y
            return "a click at ($x, $y), the middle of $($Step.book)'s $($Step.cell); $how"
        }
        'heading' {
            # A column's heading: the middle of the column, 15 px above its row 1 (the heading row is
            # about 30 px tall at 100% zoom and 150%).
            $c = $script:Geo.windows[$Step.book].cells[$Step.cell]
            $x = [int](($c.Left + $c.Right) / 2); $y = [int]$c.Top - 15
            $how = Hand-Click $x $y
            return "a click at ($x, $y), on $($Step.book)'s column heading above $($Step.cell); $how"
        }
        'drag' {
            $f = $script:Geo.windows[$Step.book].cells[$Step.from]; $t = $script:Geo.windows[$Step.book].cells[$Step.to]
            $x1 = [int](($f.Left + $f.Right) / 2); $y1 = [int](($f.Top + $f.Bottom) / 2); $x2 = [int](($t.Left + $t.Right) / 2); $y2 = [int](($t.Top + $t.Bottom) / 2)
            [void](Hand-Move $x1 $y1)
            Drag-From $script:Xl $x1 $y1 $x2 $y2 20
            return "a drag from ($x1, $y1) to ($x2, $y2), the middles of $($Step.book)'s $($Step.from) and $($Step.to), in 20 steps"
        }
        'wait' { Start-Sleep -Milliseconds $Step.ms; return "a wait of $($Step.ms) ms" }
        'ime' {
            # The IME's own key, as a keyboard sends it: VK_IME_ON (0x16) or VK_IME_OFF (0x1A).
            Show-Excel $script:Xl
            [Pointing.Keys]::Tap([uint16]$(if ($Step.on) { 0x16 } else { 0x1A }), $false)
            Start-Sleep -Milliseconds 150
            return "the IME $(if ($Step.on) { 'on (VK_IME_ON)' } else { 'off (VK_IME_OFF)' })"
        }
        'namebox' {
            $n = $script:Geo.windows['Book1'].nameBox
            $x = [int](($n.Left + $n.Right) / 2); $y = [int](($n.Top + $n.Bottom) / 2)
            $how = Hand-Click $x $y
            return "a click at ($x, $y), the middle of the Name Box; $how"
        }
        'alttab' {
            # Alt+Tab as a keyboard sends it (Alt held, Tab, Alt released); Show-Excel is not called,
            # so nothing brings Excel forward by itself.
            [Pointing.Keys]::One(0x12, $false, $false); Start-Sleep -Milliseconds 60
            [Pointing.Keys]::Tap(0x09, $false); Start-Sleep -Milliseconds 120
            [Pointing.Keys]::One(0x12, $true, $false); Start-Sleep -Milliseconds 700
            return "Alt+Tab; in front now: $(Front-Window)"
        }
        'nbdrag' {
            # A press inside the Name Box's text, dragged to another place in it, then released, as a
            # hand drags: 8 steps of 15 ms. The places are screen pixels between the glyphs of D10 read
            # from the fifteenth run's picture of the Name Box (D 21-31, 1 35-42, 0 45-53).
            Show-Excel $script:Xl
            $n = $script:Geo.windows['Book1'].nameBox; $y = [int](($n.Top + $n.Bottom) / 2)
            [void](Hand-Move $Step.x1 $y)
            [ExcelDriver.Native]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 80
            for ($i = 1; $i -le 8; $i++) { [void][ExcelDriver.Native]::SetCursorPos([int]($Step.x1 + ($Step.x2 - $Step.x1) * $i / 8), $y); Start-Sleep -Milliseconds 15 }
            Start-Sleep -Milliseconds 100
            [ExcelDriver.Native]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60
            return "a press in the Name Box at ($($Step.x1), $y), between 1 and 0 of D10, dragged to ($($Step.x2), $y), between D and 1, in 8 steps, and released"
        }
        'bar' {
            $f = $script:Geo.windows['Book1'].formulaBar
            $x = [int]$f.Left + $Step.dx; $y = [int](($f.Top + $f.Bottom) / 2)
            $how = Hand-Click $x $y
            return "a click in the Formula Bar at ($x, $y), $($Step.dx) px right of its left edge and half way down; $how"
        }
    }
    return "the keys $($Step.keys)"
}
# The pointer moved as a hand moves it, without a press.
function Hand-Move([int]$X, [int]$Y) {
    Show-Excel $script:Xl
    $from = [System.Windows.Forms.Cursor]::Position
    for ($i = 1; $i -le 15; $i++) { [void][ExcelDriver.Native]::SetCursorPos([int]($from.X + ($X - $from.X) * $i / 15), [int]($from.Y + ($Y - $from.Y) * $i / 15)); Start-Sleep -Milliseconds 15 }
    Start-Sleep -Milliseconds 100
}
# The list under the edit: the rows of the visible list (DataItem), in order, and the one selected.
function List-Of($Pops) {
    foreach ($p in $Pops) {
        if ($p.class -ne '__XLACOOUTER') { continue }
        $items = @(@(G $p.uia 'items') | Where-Object { $_['type'] -eq 'DataItem' })
        return [ordered]@{ items = @($items | ForEach-Object { [string]$_['name'] }); selected = @($items | Where-Object { (G $_ 'selected') -eq $true } | ForEach-Object { [string]$_['name'] }) }
    }
    return $null
}
function Book-Of([long]$Hwnd) {
    foreach ($n in $script:Geo.windows.Keys) { if ($script:Geo.windows[$n].hwnd -eq $Hwnd) { return $n } }
    return $null
}
function Take-State($C, $S, [string]$Name) {
    $done = @()
    foreach ($step in $S.steps) { $done += (Do-Step $step) }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    Start-Sleep -Milliseconds 600
    $a = Grab-Scene; $atA = $sw.ElapsedMilliseconds
    while ($sw.ElapsedMilliseconds -lt $atA + 300) { Start-Sleep -Milliseconds 5 }
    $b = Grab-Scene; $atB = $sw.ElapsedMilliseconds
    # What UI Automation reads, after both shots: in each workbook's window.
    $front = [long][ExcelDriver.Native]::GetForegroundWindow()
    $row = [ordered]@{ state = $Name; addition = [bool]($S.ContainsKey('addition') -and $S.addition); did = $done; shotAtMs = $atA; secondShotAtMs = $atB
        foregroundIsExcel = ($null -ne (Book-Of $front)); foregroundBook = (Book-Of $front)
        focused = $(if ($script:NoUia) { @{ skipped = 'no UI Automation in this case' } } else { [Pointing.Ui]::Focused(3000) }); books = [ordered]@{} }
    $row.focused['book'] = Book-Of ([long](G $row.focused 'topLevelHwnd'))
    $row.ime = [Pointing.Ime]::State($script:Hwnd)
    $row.excelLayout = Get-ExcelLayout
    foreach ($n in $script:Geo.windows.Keys) {
        $el = $script:El[$n]
        $bk = [ordered]@{ formulaBar = [Pointing.Ui]::Read($el.fb, 3000); nameBox = [Pointing.Ui]::Read($el.nb, 3000); statusBar = [Pointing.Ui]::Children($el.sb, 6, 3000) }
        $bk.mode = (@(G $bk.statusBar 'names') | Select-Object -First 1)
        $row.books[$n] = $bk
    }
    # Book1's, as the tenth run recorded them.
    $row.formulaBar = $row.books['Book1'].formulaBar; $row.nameBox = $row.books['Book1'].nameBox; $row.statusBar = $row.books['Book1'].statusBar; $row.mode = $row.books['Book1'].mode
    $pops = @()
    $k = 0
    foreach ($w in $a.windows) {
        if ($w.class -eq 'XLMAIN' -or $script:Shadows -contains $w.class) { continue }
        $k++
        $p = [ordered]@{ class = $w.class; title = $w.title; rect = $w.rect; printed = $w.printed; uia = $(if ($script:NoUia) { @{ items = @() } } else { [Pointing.Ui]::Tree([long]$w.hwnd, 7, 250, 4000) }) }
        $p.shot = Save-Crop $a.bmp ([ordered]@{ Left = $w.rect[0]; Top = $w.rect[1]; Right = $w.rect[2]; Bottom = $w.rect[3] }) ("{0}-{1}-popup-{2}" -f $C.Id, $Name, $k) 2
        $pops += $p
    }
    $row.popups = $pops
    $row.list = List-Of $pops
    $row.windowsShown = @($a.windows | ForEach-Object { [ordered]@{ class = $_.class; rect = $_.rect } })
    $base = Join-Path $Raw ("{0}-{1}" -f $C.Id, $Name)
    $a.bmp.Save("$base-600.png", [Drawing.Imaging.ImageFormat]::Png)
    $b.bmp.Save("$base-900.png", [Drawing.Imaging.ImageFormat]::Png)
    $two = $script:Geo.windows.Count -gt 1
    if (-not $two) { $row.text = Read-Text "$base-600.png" }
    # The committed pictures, blanked: the window's top left (one workbook) or the work area (two);
    # for each workbook, crops of its Formula Bar, Name Box, status bar's mode and cells.
    Blank-Accounts $a.bmp $a.windows
    $id = "{0}-{1}" -f $C.Id, $Name
    $g1 = $script:Geo.windows['Book1']
    $scene = if ($two) { [ordered]@{ Left = $script:Area.Left; Top = $script:Area.Top; Right = $script:Area.Right; Bottom = $script:Area.Bottom } }
             else { [ordered]@{ Left = $script:Area.Left; Top = $script:Area.Top; Right = [Math]::Min($g1.cells.D10.Right + 1400, $script:Area.Right); Bottom = $g1.cells.D12.Bottom + 500 } }
    $shots = [ordered]@{ scene = (Save-Crop $a.bmp $scene $id) }
    foreach ($n in $script:Geo.windows.Keys) {
        $g = $script:Geo.windows[$n]; $tag = if ($two) { '-' + $n.ToLowerInvariant() } else { '' }
        $right = if ($two) { $g.rect.Right } else { $script:Area.Right }
        if ($g.formulaBar) { $shots["$n formulaBar"] = Save-Crop $a.bmp ([ordered]@{ Left = $g.formulaBar.Left; Top = $g.formulaBar.Top; Right = [Math]::Min($g.formulaBar.Left + 1400, $right); Bottom = $g.formulaBar.Bottom }) "$id$tag-formula-bar" 4 }
        if ($g.nameBox) { $shots["$n nameBox"] = Save-Crop $a.bmp $g.nameBox "$id$tag-name-box" 4 }
        if ($g.statusBar) { $shots["$n mode"] = Save-Crop $a.bmp ([ordered]@{ Left = $g.statusBar.Left; Top = $g.statusBar.Top; Right = $g.statusBar.Left + 500; Bottom = $g.statusBar.Bottom }) "$id$tag-mode" 2 }
        $shots["$n cells"] = Save-Crop $a.bmp ([ordered]@{ Left = $g.cells.A1.Left - 60; Top = $g.cells.A1.Top - 40; Right = [Math]::Min($g.cells.D10.Right + 700, $right); Bottom = $g.cells.D12.Bottom + 40 }) "$id$tag-cells"
    }
    if (-not $two) {
        $shots.cellTextX4 = Save-Enlarged $a.bmp ([ordered]@{ Left = $g1.cells.D10.Left - 8; Top = $g1.cells.D10.Top - 6; Right = $g1.cells.D10.Left + 560; Bottom = $g1.cells.D10.Bottom + 6 }) "$id-cell-text-x4" 4
        if ($g1.formulaBar) { $shots.barTextX4 = Save-Enlarged $a.bmp ([ordered]@{ Left = $g1.formulaBar.Left; Top = $g1.formulaBar.Top; Right = $g1.formulaBar.Left + 560; Bottom = $g1.formulaBar.Bottom }) "$id-bar-text-x4" 4 }
    }
    $row.shots = $shots
    $a.bmp.Dispose(); $b.bmp.Dispose()
    $row.outlines = Read-Outlines "$base-600.png" "$base-900.png"
    $parts = foreach ($n in $script:Geo.windows.Keys) {
        $bk = $row.books[$n]
        $sel = @(G $bk.formulaBar 'selectionAt') | ForEach-Object { if ($_ -is [array]) { '[' + ($_ -join ',') + ']' } else { $_ } }
        "{0} bar [{1}] sel {2} box [{3}] {4}" -f $n, (G $bk.formulaBar 'text'), ($sel -join ' '), (G $bk.nameBox 'text'), $bk.mode
    }
    Say ("  {0} {1} at {2} ms; front {3}: {4}; IME {5}; list {6}" -f $C.Id, $Name, $atA, $row.foregroundBook, ($parts -join '; '), $row.ime,
        $(if ($row.list) { (@($row.list.items | ForEach-Object { if ($row.list.selected -contains $_) { "*$_" } else { $_ } }) -join ' / ') } else { 'none' }))
    foreach ($p in $pops) { if ($p.class -ne '__XLACOOUTER') { Say ("    popup {0} {1}: {2}" -f $p.class, ($p.rect -join ','), ((@(G $p.uia 'items') | Where-Object { $_['name'] } | ForEach-Object { [string]$_['name'] }) -join ' / ')) } }
    return $row
}
function Save-Enlarged($Bmp, $Rect, [string]$Name, [int]$Scale) {
    $l = [Math]::Max([int]$Rect.Left - $script:Area.Left, 0); $t = [Math]::Max([int]$Rect.Top - $script:Area.Top, 0)
    $r = [Math]::Min([int]$Rect.Right - $script:Area.Left, $Bmp.Width); $b = [Math]::Min([int]$Rect.Bottom - $script:Area.Top, $Bmp.Height)
    if ($r -le $l -or $b -le $t) { return $null }
    $w = $r - $l; $h = $b - $t
    $big = New-Object Drawing.Bitmap ($w * $Scale), ($h * $Scale)
    $gr = [Drawing.Graphics]::FromImage($big)
    $gr.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $gr.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
    $gr.DrawImage($Bmp, (New-Object Drawing.Rectangle 0, 0, ($w * $Scale), ($h * $Scale)), (New-Object Drawing.Rectangle $l, $t, $w, $h), [Drawing.GraphicsUnit]::Pixel)
    $gr.Dispose(); $big.Save((Join-Path $script:Shots ($Name + '.png')), [Drawing.Imaging.ImageFormat]::Png); $big.Dispose()
    return "shots/$Name.png"
}

# The text of the in-cell edit (row 10 from D10 rightwards, as the eighth run read it) and of the
# Formula Bar's line: each run of text and its colour, and the ground behind it; read both as the
# eighth run did (darkest pixel, lightest ground) and for any ground (furthest from the column's most
# frequent pixel, and that pixel). For each run of ground that is not the surface's own (a highlight),
# the saturated colours inside it.
function Read-Surface($Img, [int]$L, [int]$T, [int]$R_, [int]$B_) {
    $ox = $script:Area.Left
    $x0 = $L - $ox; $x1 = [Math]::Min($R_ - $ox, $Img.W); $y0 = $T - $script:Area.Top; $y1 = $B_ - $script:Area.Top
    $ground = [Pointing.Px]::Mode($Img, $x0, $y0, $x1, $y1)
    $o = [ordered]@{ box = @($L, $T, $R_, $B_); ground = [Pointing.Px]::Hex($ground)
        textByDarkest = [Pointing.Px]::TextByDarkest($Img, $x0, $y0, $x1, $y1, 60, $ox, 6)
        groundsByLightest = [Pointing.Px]::Grounds($Img, $x0, $y0, $x1, $y1, $ground, 12, $ox)
        textByContrast = [Pointing.Px]::TextByContrast($Img, $x0, $y0, $x1, $y1, 60, $ox, 6)
        groundsByMode = [Pointing.Px]::GroundsByMode($Img, $x0, $y0, $x1, $y1, $ground, 12, $ox) }
    $inside = @()
    foreach ($run in @($o.groundsByMode['runs'])) {
        $rx0 = [int]$run['x0'] - $ox; $rx1 = [int]$run['x1'] - $ox + 1
        $inside += [ordered]@{ x0 = $run['x0']; x1 = $run['x1']; ground = $run['colour']
            saturated = [Pointing.Px]::SatColours($Img, $rx0, $y0, $rx1, $y1, 60, 8, 3)
            textByDarkest = [Pointing.Px]::TextByDarkest($Img, $rx0, $y0, $rx1, $y1, 60, $ox, 6)
            textByContrast = [Pointing.Px]::TextByContrast($Img, $rx0, $y0, $rx1, $y1, 60, $ox, 6) }
    }
    $o.highlights = $inside
    return $o
}
function Read-Text([string]$File) {
    $img = [Pointing.Img]::Load($File)
    $g = $script:Geo.windows['Book1']; $d = $g.cells.D10
    $o = [ordered]@{ cell = (Read-Surface $img ($d.Left + 3) ($d.Top + 3) ($d.Left + 1500) ($d.Bottom - 3)) }
    if ($g.formulaBar) {
        # The line of text only: the box's own border runs 12 px inside the element's top, 11 px
        # inside its bottom and 5 px inside its left, at 150% (the eighth run).
        $f = $g.formulaBar
        $o.formulaBar = Read-Surface $img ($f.Left + 8) ($f.Top + 16) ($f.Left + 1500) ($f.Bottom - 15)
    }
    return $o
}

# Where a line was drawn: for every cell named in $CellNames, each edge's pixels 1 to 3 px inside it
# that changed since the "before" shot, and the colour they changed to; and the box around every
# pixel of the cells A1:E12 or so that changed.
function Read-Outlines([string]$OpenFile, [string]$LaterFile) {
    $before = [Pointing.Img]::Load((Join-Path $Raw ("{0}-before.png" -f $script:CaseId)))
    $open = [Pointing.Img]::Load($OpenFile); $later = [Pointing.Img]::Load($LaterFile)
    $out = [ordered]@{}
    foreach ($n in $script:Geo.windows.Keys) {
        $g = $script:Geo.windows[$n]
        $ox = $script:Area.Left; $oy = $script:Area.Top
        $box = [Pointing.Px]::Diff($before, $open, $g.cells.A1.Left - $ox, $g.cells.A1.Top - $oy, $g.cells.D11.Right + 200 - $ox, $g.cells.D12.Bottom + 40 - $oy, 12, $ox, $oy)
        $moved = [Pointing.Px]::Diff($open, $later, $g.cells.A1.Left - $ox, $g.cells.A1.Top - $oy, $g.cells.D11.Right + 200 - $ox, $g.cells.D12.Bottom + 40 - $oy, 12, $ox, $oy)
        $cells = [ordered]@{}
        foreach ($c in $script:CellNames) {
            # (PowerShell's names ignore case: $cr, not $r beside $R.)
            $cr = $g.cells[$c]; $x0 = $cr.Left - $ox; $y0 = $cr.Top - $oy; $x1 = $cr.Right - $ox; $y1 = $cr.Bottom - $oy
            $ground = [Pointing.Px]::Mode($before, $x0 + 6, $y0 + 6, $x1 - 6, $y1 - 6)
            $sides = [ordered]@{}
            foreach ($side in 'top', 'right', 'bottom', 'left') {
                $e = [Pointing.Px]::EdgeReport($open, $before, $side, $x0, $y0, $x1, $y1, [int[]]@(1, 2, 3), $ground, 0xd4d4d4, 12, 4)
                $len = if ($side -eq 'top' -or $side -eq 'bottom') { $x1 - $x0 - 8 } else { $y1 - $y0 - 8 }
                if ([int]$e['changedCount'] -gt 0.3 * $len) { $sides[$side] = ('#{0} over {1} of {2} px at offset {3}, {4} run(s), longest {5}' -f $e['coverOf'], $e['changedCount'], (3 * $len), $e['bestOffset'], $e['runsAtBest'], $e['longestRun']) }
            }
            if ($sides.Count -gt 0) { $cells[$c] = $sides }
        }
        $out[$n] = [ordered]@{ changedSinceBefore = $box; changedFrom600To900 = $moved; edgesChanged = $cells }
    }
    return $out
}

# ---- The cases ----------------------------------------------------------------------------------

$Cases = [ordered]@{}
function Def([string]$Id, [string]$Group, [bool]$Book2, [string]$Keys, [string]$Asked, [string]$Reading, [object[]]$States) {
    $Cases[$Id] = [pscustomobject]@{ Id = $Id; Group = $Group; Book2 = $Book2; Keys = $Keys; Asked = $Asked; Reading = $Reading; States = $States }
}
function K([string]$Keys) { return @{ do = 'keys'; keys = $Keys } }
function ClickCell([string]$Cell, [int]$Dx) { return @{ do = 'click'; cell = $Cell; dx = $Dx } }
function ClickBar([int]$Dx) { return @{ do = 'bar'; dx = $Dx } }
function Wait([int]$Ms) { return @{ do = 'wait'; ms = $Ms } }
function Cell([string]$Book, [string]$Cell) { return @{ do = 'cell'; book = $Book; cell = $Cell } }
function Heading([string]$Book, [string]$Cell) { return @{ do = 'heading'; book = $Book; cell = $Cell } }
function Drag([string]$Book, [string]$From, [string]$To) { return @{ do = 'drag'; book = $Book; from = $From; to = $To } }

function Ime([bool]$On) { return @{ do = 'ime'; on = $On } }
function NameBox { return @{ do = 'namebox' } }
# A case of this run: Ime says whether Excel's window takes the Japanese keyboard.
function Def15([string]$Id, [string]$Group, [bool]$Ime, [string]$Keys, [string]$Asked, [string]$Reading, [object[]]$States) {
    $Cases[$Id] = [pscustomobject]@{ Id = $Id; Group = $Group; Book2 = $false; Ime = $Ime; Keys = $Keys; Asked = $Asked; Reading = $Reading; States = $States }
}
$kana = @(@{ state = 'k'; steps = @((K 'k')) }, @{ state = 'a'; steps = @((K 'a')) }, @{ state = 'n'; steps = @((K 'n')) }, @{ state = 'a2'; steps = @((K 'a')) })
$ends = @(@{ state = 'space'; steps = @((K '{SPACE}')) }, @{ state = 'enter-1'; steps = @((K '{ENTER}')) }, @{ state = 'enter-2'; steps = @((K '{ENTER}')) })
$imeOn = @{ state = 'ime-on'; steps = @((Ime $true)) }
function St([string]$Name, [object[]]$Steps) { return @{ state = $Name; steps = $Steps } }
function KG([string]$Keys, [int]$Gap) { return @{ do = 'keys'; keys = $Keys; gap = $Gap } }

# Part A: Excel with the same keys as ExSheet, D10 selected, where the procedure's Excel column is not
# "-". The IME's key and Excel's own: VK_IME_ON with the cell selected (k1).
Def15 'k1' 'A' $true 'D10: VK_IME_ON' 'The IME''s open status afterwards' 'ExSheet (ADR-0080): the IME''s open status reads on' @($imeOn)
Def15 'k2' 'A' $true 'D10: the IME on; kana, Space, Enter, Enter (the fifteenth run''s i1-b)' 'After each key: the composition, the edit, the active cell' 'ExSheet: composed in the Keyboard Field over D10; the first Enter opens the Cell Editor holding the text; the second commits D10 and moves to D11' (@($imeOn) + $kana + $ends)
Def15 'k3' 'A' $true 'D10: the IME on; kana, Escape, Escape (the fifteenth run''s i2-b)' 'What each Escape does' 'ExSheet: the first Escape ends the composition, the Cell Editor open and empty; the second cancels it' (@($imeOn) + $kana + @((St 'escape-1' @((K '{ESC}'))), (St 'escape-2' @((K '{ESC}')))))
Def15 'k4' 'A' $true 'D10: the IME on; kana, Space, Down, Down, Enter, Enter (the fifteenth run''s i3-b)' 'What Down does while composing' 'ExSheet: each Down the IME''s; no Focus move; then as k2' (@($imeOn) + $kana + @((St 'space' @((K '{SPACE}'))), (St 'down-1' @((K '{DOWN}'))), (St 'down-2' @((K '{DOWN}'))), (St 'enter-1' @((K '{ENTER}'))), (St 'enter-2' @((K '{ENTER}')))))
$kanji = @((St 'kanji-k' @((K 'k'))), (St 'kanji-a' @((K 'a'))), (St 'kanji-n' @((K 'n'))), (St 'kanji-j' @((K 'j'))), (St 'kanji-i' @((K 'i'))))
Def15 'k6' 'A' $true 'D10: the IME on; kana, Space, kanji (a reading after each key), Space, Enter, Enter' 'The two clauses, and what D10 holds' 'ExSheet: one composition of two clauses, converted together; D10 holds both' (@($imeOn) + $kana + @((St 'space-1' @((K '{SPACE}')))) + $kanji + @((St 'space-2' @((K '{SPACE}'))), (St 'enter-1' @((K '{ENTER}'))), (St 'enter-2' @((K '{ENTER}')))))
Def15 'k6x' 'A' $true 'An addition: k6 with kanji typed 150 ms between its keys, in one state' 'The same' 'none (an addition, as ExSheet''s k6x)' (@($imeOn) + $kana + @((St 'space-1' @((K '{SPACE}'))), (St 'kanji' @((KG 'kanji' 150))), (St 'space-2' @((K '{SPACE}'))), (St 'enter-1' @((K '{ENTER}'))), (St 'enter-2' @((K '{ENTER}')))))
Def15 'k6y' 'A' $true 'An addition: k6 with kanji typed 30 ms between its keys, in one state' 'The same' 'none (an addition, as ExSheet''s k6y)' (@($imeOn) + $kana + @((St 'space-1' @((K '{SPACE}'))), (St 'kanji' @((KG 'kanji' 30))), (St 'space-2' @((K '{SPACE}'))), (St 'enter-1' @((K '{ENTER}'))), (St 'enter-2' @((K '{ENTER}')))))
Def15 'k7' 'A' $true 'D10: the IME on; kana, Space, Enter, desu, Space, Enter, Enter with no pause between keys (the procedure: the same keys at 0 ms)' 'What D10 holds; nothing lost, doubled or reordered' 'ExSheet behind 150 ms: D10 かなです, D11' @($imeOn, (St 'typed' @((KG 'kana{SPACE}{ENTER}desu{SPACE}{ENTER}{ENTER}' 0))), (St 'later' @((Wait 2000))))
Def15 'k8' 'A' $true 'D10: the IME on; kana composed; a click on D12' 'Where the composition goes, the active cell, whether an edit is open' 'ExSheet: D10 かな, committed by the press; the Focus D12; no edit open' (@($imeOn) + $kana + @((St 'clicked-d12' @((Cell 'Book1' 'D12')))))
Def15 'k8x' 'A' $true 'An addition: k8 with the click on D8, above D10, clear of the IME''s own window' 'The same' 'none (an addition, as ExSheet''s k8x)' (@($imeOn) + $kana + @((St 'clicked-d8' @((Cell 'Book1' 'D8')))))
Def15 'k9' 'A' $true 'D10: the IME on; kana composed; a click on the Formula Bar''s text' 'Where the composition goes, and where the keyboard is' 'ExSheet: record' (@($imeOn) + $kana + @((St 'clicked-bar' @((ClickBar 40)))))
Def15 'k10' 'A' $true 'D10: the IME on; kana composed; a click on column D''s heading' 'What is selected, and where the composition goes' 'ExSheet (ADR-0080): the press ends the composition first, so かな goes to D10' (@($imeOn) + $kana + @((St 'clicked-heading-d' @((Heading 'Book1' 'D1')))))
Def15 'k12' 'A' $true 'D10: the IME on, no composition: Space' 'What is typed' 'ExSheet: record the keydown and what is typed' @($imeOn, (St 'space' @((K '{SPACE}'))))
Def15 'k15' 'A' $true 'D10: the IME on; kana composed; Alt+Tab to another window (the script''s own) and back; then Enter, Enter' 'Whether the composition survives, and where its text goes' 'ExSheet: record' (@($imeOn) + $kana + @((St 'alt-tab-away' @(@{ do = 'alttab' })), (St 'alt-tab-back' @(@{ do = 'alttab' })), (St 'enter-1' @((K '{ENTER}'))), (St 'enter-2' @((K '{ENTER}')))))
$Cases['k15'] | Add-Member AltTab $true
Def15 'k16' 'A' $true 'D10: the IME on; kana composed; a click on the composition''s own text (20 px right of D10''s left edge, half way down)' 'What happens' 'ExSheet: record' (@($imeOn) + $kana + @((St 'clicked-on-text' @((ClickCell 'D10' 20)))))

# Part D: the Name Box.
Def15 'd1' 'D' $true 'The Name Box: a click on it, the IME on, kana, Enter, then Escape (the fifteenth run''s i7-b)' 'What the Name Box reads while composing, and what Enter does' 'ExSheet: the Name Box reads かな while composing, not D10かな; the first Enter ends the composition and goes nowhere' (@((St 'clicked' @((NameBox))), $imeOn) + $kana + @((St 'enter' @((K '{ENTER}'))), (St 'escape' @((K '{ESC}')))))
Def15 'd3' 'D' $true 'The Name Box: with the keyboard on the sheet (D10 selected), a press inside D10 (between 1 and 0) dragged to between D and 1, then released; then Escape' 'The selection in the Name Box after the release' 'ExSheet: the whole text selected at the release' @((St 'dragged' @(@{ do = 'nbdrag'; x1 = 44; x2 = 33 })), (St 'escape' @((K '{ESC}'))))

# The case's workbook, maximised at 100% zoom; D10 selected; the English keyboard for the new Excel.
function Set-Up($C) {
    if ($script:Xl) { Stop-OwnExcel }
    if ($C.PSObject.Properties['AltTab'] -and $C.AltTab) {
        # The window Alt+Tab goes to (k15), in front before this case's Excel starts, so that it
        # stands next to Excel in the order Alt+Tab follows.
        $fh = [Pointing.AltTarget]::Open(); Start-Sleep -Milliseconds 300
        if ([ExcelDriver.Native]::GetForegroundWindow() -ne $fh) { [Pointing.Keys]::Tap(0x12, $false); [void][ExcelDriver.Native]::SetForegroundWindow($fh); Start-Sleep -Milliseconds 300 }
        Say "Alt+Tab's window open; in front: $(Front-Window)"
    }
    [void](Start-OwnExcel)
    $b1 = New-Book 'Book1' 'Positions'
    $script:Hwnd = [IntPtr][long]$b1.Windows.Item(1).Hwnd
    Set-Content $script:HwndFile ([long]$script:Hwnd)
    if ($C.PSObject.Properties['Book2'] -and $C.Book2) {
        # The ninth run's Part A: Book2 with Trades, the two windows side by side, Book1 on the left.
        $b2 = New-Book 'Book2' 'Trades'
        [void]$b1.Activate(); [void]$b1.Windows.Item(1).Activate()
        [void]$script:Xl.Windows.Arrange(-4166)   # xlArrangeStyleVertical: View > Arrange All > Vertical
        Start-Sleep -Milliseconds 500
        foreach ($wb in @($b2, $b1)) {
            [void]$wb.Activate(); $wb.Windows.Item(1).Zoom = 100
            [void]$script:Xl.Goto($wb.Worksheets.Item('Sheet1').Range('A1'), $true)
        }
    } else {
        $b1.Windows.Item(1).WindowState = -4137   # xlMaximized
        $b1.Windows.Item(1).Zoom = 100
        [void]$script:Xl.Goto($b1.Worksheets.Item('Sheet1').Range('A1'), $true)
    }
    [void]$b1.Activate()
    [void]$b1.Worksheets.Item('Sheet1').Range('D10').Select()
    Show-Excel $script:Xl
    $k = Set-EnglishKeyboard
    if ($C.PSObject.Properties['Ime'] -and $C.Ime) {
        # Excel's window to the Japanese keyboard, as Win+Space would; the IME off to begin with. This
        # thread stays English (UK): every character typed with the IME off is the same key on both.
        [void][Pointing.Native]::PostMessage($script:Hwnd, 0x0050, [IntPtr]::Zero, [IntPtr]0x04110411)   # WM_INPUTLANGCHANGEREQUEST
        Start-Sleep -Milliseconds 600
        [Pointing.Keys]::Tap(0x1A, $false); Start-Sleep -Milliseconds 200   # VK_IME_OFF
        $k.japanese = [ordered]@{ excelNow = Get-ExcelLayout; ime = [Pointing.Ime]::State($script:Hwnd) }
    }
    Start-Sleep -Milliseconds 300
    return $k
}

function Run-Case($C) {
    $origId = $C.Id
    if ($Pass) { $C = $C.PSObject.Copy(); $C.Id = $C.Id + '-' + $Pass }
    Say "case $($C.Id): $($C.Keys)"
    $script:CaseId = $C.Id
    $script:NoUia = [bool]($NoUia -or ($C.PSObject.Properties['NoUia'] -and $C.NoUia))
    $keyboard = Set-Up $C
    Set-CaseDue 120
    $script:Geo = Get-Geometry
    Show-Excel $script:Xl
    Start-Sleep -Milliseconds 300
    $bef = Grab-Scene; $bef.bmp.Save((Join-Path $Raw "$($C.Id)-before.png"), [Drawing.Imaging.ImageFormat]::Png); $bef.bmp.Dispose()
    $states = @()
    foreach ($s in $C.States) {
        if ($s.ContainsKey('until')) {
            for ($n = 1; $n -le 6; $n++) {
                $st = Take-State $C $s ("{0}-{1}" -f $s.state, $n)
                $states += $st
                if ($st.list -and $st.list.selected -contains $s.until) { break }
                if (-not $st.list -or @($st.list.items).Count -eq 0) { $st.note = "the list was not read; no further Down is pressed"; break }
            }
            continue
        }
        $states += (Take-State $C $s $s.state)
    }
    $escapes = Until-Ready
    # After Escape: what the case left written, and where Excel is.
    $ws = $script:Books['Book1'].Worksheets.Item('Sheet1')
    $after = [ordered]@{ escapes = $escapes; activeWorkbook = [string]$script:Xl.ActiveWorkbook.Name; activeCell = [string]$script:Xl.ActiveCell.Address($false, $false)
        d10 = [string]$ws.Range('D10').Formula2; d8 = [string]$ws.Range('D8').Formula2; d11 = [string]$ws.Range('D11').Formula2; d12 = [string]$ws.Range('D12').Formula2
        selection = [string]$script:Xl.Selection.Address($false, $false); usedRange = [string]$ws.UsedRange.Address($false, $false) }
    Move-Mouse ([int]$script:Area.Right - 200) ([int]$script:Area.Bottom - 300)
    $row = [ordered]@{ case = $origId; pass = $(if ($Pass) { $Pass } else { 'a' }); group = $C.Group; book2 = $C.Book2; asked = $C.Asked; reading = $C.Reading; keys = $C.Keys
        time = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'); excelProcess = $script:OwnPid; keyboard = $keyboard
        geometry = $script:Geo; states = $states; afterEscape = $after }
    Clear-CaseDue
    if ($C.PSObject.Properties['AltTab'] -and $C.AltTab) { [Pointing.AltTarget]::Close() }
    [IO.File]::WriteAllText((Join-Path $Raw "$($C.Id).json"), ($row | ConvertTo-Json -Depth 16), $Utf8)
    Write-Line $row
    Say "  after Escape: active $($after.activeWorkbook) $($after.activeCell), selection $($after.selection), D8 [$($after.d8)] D10 [$($after.d10)] D11 [$($after.d11)] D12 [$($after.d12)], used $($after.usedRange)"
}

# File > Account, opened with Alt, F and a click on Account (its place read through UI Automation).
# Returns Excel's window as UI Automation sees it, or nothing when Account was not found. Nothing on
# this page is pictured: it shows the account's name and address.
function Open-Account {
    Send-Keys $script:Xl '%' 700; Send-Keys $script:Xl 'f' 1800
    $root = [Windows.Automation.AutomationElement]::FromHandle($script:Hwnd)
    $account = $root.FindFirst([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, 'Account')))
    if (-not $account) { return $null }
    $r = $account.Current.BoundingRectangle
    Show-Excel $script:Xl; Move-Mouse ([int]($r.Left + $r.Width / 2)) ([int]($r.Top + $r.Height / 2)); Press-Mouse; Release-Mouse
    Start-Sleep -Milliseconds 2500
    return $root
}
function Theme-Combo($Root) {
    $c = New-Object Windows.Automation.AndCondition @(
        (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, 'Office Theme')),
        (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::ComboBox)))
    return $Root.FindFirst([Windows.Automation.TreeScope]::Descendants, $c)
}
function Theme-Value($Root) {
    $combo = Theme-Combo $Root
    if (-not $combo) { return $null }
    try { return [string]$combo.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value } catch { return $null }
}
# The environment, and the keyboard checked end to end: literal text typed into a cell and read back.
function Run-Environment {
    $envRow = [ordered]@{ case = '0'; pass = $(if ($Pass) { $Pass } else { 'a' }); time = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'); screenshots = 'PrintWindow (PW_RENDERFULLCONTENT), each window of Excel''s laid at its place'; keys = 'SendInput (virtual-key and scan code per key)'; mouse = 'SetCursorPos and mouse_event (excel-driver.ps1)' }
    $keyboard = Set-Up ([pscustomobject]@{ Id = '0'; Book2 = $false })
    Set-CaseDue 120
    $c2r = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration' -ErrorAction SilentlyContinue
    $xl = $script:Xl
    $envRow.excel = [ordered]@{ version = [string]$xl.Version; build = [string]$xl.Build; versionToReport = [string]$c2r.VersionToReport; platform = [string]$c2r.Platform
        firstWorkbookName = [string]$script:Books['Book1'].Name
        editDirectlyInCell = [bool]$xl.EditDirectlyInCell; formulaAutoComplete = [bool]$xl.DisplayFormulaAutoComplete
        displayFunctionToolTips = [bool]$xl.DisplayFunctionToolTips; generateTableRefs = [int]$xl.GenerateTableRefs
        standardFont = [string]$xl.StandardFont; standardFontSize = [double]$xl.StandardFontSize }
    $pers = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize'
    $hc = New-Object Pointing.Native+HIGHCONTRAST; $hc.cbSize = [uint32][Runtime.InteropServices.Marshal]::SizeOf($hc)
    [void][Pointing.Native]::SystemParametersInfo(0x0042, $hc.cbSize, [ref]$hc, 0)   # SPI_GETHIGHCONTRAST
    $envRow.windows = [ordered]@{ appsUseLightTheme = [int]$pers.AppsUseLightTheme; systemUsesLightTheme = [int]$pers.SystemUsesLightTheme
        dpiOfExcelWindow = [int][Pointing.Native]::GetDpiForWindow($script:Hwnd); workArea = "$($script:Area.Width)x$($script:Area.Height)"
        highContrastOn = [bool]($hc.dwFlags -band 1); culture = (Get-Culture).Name }
    $theme = Get-ItemProperty 'HKCU:\Software\Microsoft\Office\16.0\Common' -ErrorAction SilentlyContinue
    $envRow.officeThemeRegistry = if ($theme -and $theme.PSObject.Properties['UI Theme']) { [string]$theme.'UI Theme' } else { 'no UI Theme value (Office default)' }
    $envRow.keyboard = $keyboard

    $envRow.displayScale = '{0}%' -f [int]([Pointing.Native]::GetDpiForWindow($script:Hwnd) * 100 / 96)

    # File > Account: the version line and the Office Theme, read through UI Automation (names
    # holding an @ are left out: the page shows the account's address).
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

    # The keyboard: every character the cases type, typed as text into D10 and read back.
    $probe = "'=SUM(Positions[PV])+XLOOKUP(1,A2:A4,B2:B4,,0,-1)*AVERAGE(10,""X 5"")"
    $ws = $script:Books['Book1'].Worksheets.Item('Sheet1'); [void]$ws.Range('D10').Select()
    $script:Geo = Get-Geometry
    Send-Keys $xl (Lit $probe) 600
    $seen = [Pointing.Ui]::Read($script:El['Book1'].fb, 3000)
    Send-Keys $xl '~' 800
    $back = [string]$ws.Range('D10').Formula2
    $envRow.keyboardCheck = [ordered]@{ typed = $probe; formulaBarBeforeEnter = (G $seen 'text'); readBack = $back; same = ($back -ceq $probe.Substring(1)) }   # case and all (-eq ignores case: the first run of case 0 read xLOOKUP as the same)
    $envRow.uiaElements = [ordered]@{ formulaBar = $seen; nameBox = [Pointing.Ui]::Read($script:El['Book1'].nb, 3000); statusBar = [Pointing.Ui]::Children($script:El['Book1'].sb, 6, 3000); geometry = $script:Geo.windows['Book1'] }
    Clear-CaseDue
    Write-Line $envRow
    Say ($envRow | ConvertTo-Json -Compress -Depth 6)
}

# ---- Main ---------------------------------------------------------------------------------------

. (Join-Path $PSScriptRoot '..\..\2026-09-27-windows-excel-2\case-guard.ps1')
Start-CaseGuard
Say "Excel processes before the run (left alone): $($script:ExcelsAtStart -join ', ')"
try {
    foreach ($id in $Case) {
        if ($id -ne '0' -and -not $Cases.Contains($id)) { throw "no case $id" }
        try {
            if ($id -eq '0') { Run-Environment; continue }
            Run-Case $Cases[$id]
        }
        catch {
            Clear-CaseDue
            $row = [ordered]@{ case = $id; step = 'failed'; error = $_.Exception.Message; line = $_.InvocationInfo.ScriptLineNumber }
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
        try { $k = Restore-Keyboard; Write-Line ([ordered]@{ case = 'keyboard'; step = 'restored'; time = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'); keyboard = $k }); Say "keyboard restored: $($k | ConvertTo-Json -Compress)" } catch { Say "keyboard not restored: $($_.Exception.Message)" }
    }
    Stop-OwnExcel
    $left = @(Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id } | Where-Object { $script:ExcelsAtStart -notcontains $_ })
    Say "Excel processes now that were not there at the start: $($left -join ', ') (this run's own were $($script:OwnPids -join ', '))"
}
