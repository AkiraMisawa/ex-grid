<#
.SYNOPSIS
    Part A of docs/specs/exsheet/verify-on-windows-8.md: what Excel's range finder draws while a
    Formula is open in an edit (ADR-0057, "Readings"), asked with real keys and the real mouse.

        powershell -File range-finder.ps1 -Case 0             the environment, and the keyboard checked
        powershell -File range-finder.ps1 -Case 1,2,10a,10b   cases of the procedure's table
        powershell -File range-finder.ps1 -Analyse 1,2        the pixel readings again, from saved shots

    Every Formula is typed through SendKeys, into Excel's window switched to the English (UK)
    keyboard; COM only sets a case up (a sheet, a Table, a cell's content before the edit). The
    script starts an Excel of its own and ends only that one: it never attaches to a running Excel,
    because one of the user's may be open.

    Each case: a fresh workbook with one sheet, Sheet1, at 100% zoom with A1 in view, and D10
    selected. The geometry is read through COM while Excel is Ready (Excel refuses COM mid-edit), and
    a screenshot is taken (the "before"). Then the keys; 600 ms; a screenshot; 300 ms; another. Then
    Escape until Excel is Ready. A case with several states (7, 20, 21) repeats the last part per
    state.

    Outputs, beside this script:
      range-finder.jsonl   one line per case: the geometry, and the readings of the screenshots
      shots\               the window at 600 ms (the account's initials in the title bar blanked),
                           and crops: the Formula Bar, the in-cell editor, the cells the case names
                           (at 600 ms, at 900 ms and before)
    The full screenshots the readings are made from (before, 600 ms, 900 ms) stay on this machine,
    in %LOCALAPPDATA%\exgrid-layer3\range-finder-8\, with the geometry of each case.
#>
#Requires -Version 5.1
param([string[]]$Case, [string[]]$Analyse)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Case) { $Case = @($Case | ForEach-Object { $_ -split ',' } | Where-Object { $_ }) }
if ($Analyse) { $Analyse = @($Analyse | ForEach-Object { $_ -split ',' } | Where-Object { $_ }) }

. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel\excel-driver.ps1')
$script:Shots = Join-Path $PSScriptRoot 'shots'
# The driver's Show-Excel and Send-Keys, with every key through SendInput (RangeFinder.Keys, below).
$script:Named = @{ DOWN = @(0x28, 1); UP = @(0x26, 1); LEFT = @(0x25, 1); RIGHT = @(0x27, 1); HOME = @(0x24, 1); END = @(0x23, 1)
    DEL = @(0x2E, 1); DELETE = @(0x2E, 1); F2 = @(0x71, 0); ESC = @(0x1B, 0); ENTER = @(0x0D, 0); TAB = @(0x09, 0); BS = @(0x08, 0) }
# SendKeys' notation, as the procedure writes keys: {NAME} or {NAME n} for a named key, {c} for a
# character SendKeys reserves, ~ for Enter, % alone for a tap of Alt; any other character as itself.
function Type-Keys([string]$Spec, [int]$GapMs = 30) {
    $i = 0
    while ($i -lt $Spec.Length) {
        $c = $Spec[$i]
        if ($c -eq '{') {
            $end = $Spec.IndexOf('}', $i + 2)
            $inner = $Spec.Substring($i + 1, $end - $i - 1)
            $i = $end + 1
            if ($inner.Length -eq 1) { [RangeFinder.Keys]::Char($inner[0]); Start-Sleep -Milliseconds $GapMs; continue }
            $parts = $inner -split ' '; $n = if ($parts.Count -gt 1) { [int]$parts[1] } else { 1 }
            $k = $script:Named[$parts[0].ToUpperInvariant()]
            if ($null -eq $k) { throw "no key named $($parts[0])" }
            for ($j = 0; $j -lt $n; $j++) { [RangeFinder.Keys]::Tap([uint16]$k[0], [bool]$k[1]); Start-Sleep -Milliseconds $GapMs }
            continue
        }
        if ($c -eq '~') { [RangeFinder.Keys]::Tap(0x0D, $false) }
        elseif ($c -eq '%') { [RangeFinder.Keys]::Tap(0x12, $false) }
        else { [RangeFinder.Keys]::Char($c) }
        Start-Sleep -Milliseconds $GapMs
        $i++
    }
}
function Show-Excel($xl) {
    $hwnd = $script:Hwnd
    [uint32]$excelPid = 0; [uint32]$frontPid = 0
    [void][ExcelDriver.Native]::GetWindowThreadProcessId($hwnd, [ref]$excelPid)
    [void][ExcelDriver.Native]::GetWindowThreadProcessId([ExcelDriver.Native]::GetForegroundWindow(), [ref]$frontPid)
    if ($excelPid -ne 0 -and $excelPid -eq $frontPid) { return }
    [RangeFinder.Keys]::Tap(0x12, $false)
    [void][ExcelDriver.Native]::ShowWindow($hwnd, 3)
    [void][ExcelDriver.Native]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 400
    if ([ExcelDriver.Native]::GetForegroundWindow() -ne $hwnd) { throw 'Excel is not in the foreground; keys would go elsewhere.' }
}
function Send-Keys($xl, [string]$Keys, [int]$Settle = 250) {
    Show-Excel $xl
    Type-Keys $Keys
    Start-Sleep -Milliseconds $Settle
}
$Log = Join-Path $PSScriptRoot 'range-finder.jsonl'
$Raw = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\range-finder-8'
if (-not (Test-Path $Raw)) { [void](New-Item -ItemType Directory $Raw) }
if (-not (Test-Path $script:Shots)) { [void](New-Item -ItemType Directory $script:Shots) }
$Utf8 = New-Object Text.UTF8Encoding $false
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

Add-Type -Namespace RangeFinder -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
[DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint thread);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr LoadKeyboardLayout(string id, uint flags);
[DllImport("user32.dll")] public static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint flags);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern short VkKeyScan(char c);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
[DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
[DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint action, uint param, ref HIGHCONTRAST value, uint winIni);
[StructLayout(LayoutKind.Sequential)] public struct HIGHCONTRAST { public uint cbSize; public uint dwFlags; public IntPtr lpszDefaultScheme; }
'@

# Keys through SendInput, a key down and a key up with its virtual-key code and its scan code, as a
# keyboard sends them. On 2026-09-29 from 23:36, SendKeys and keybd_event reached Excel garbled
# (`=A1+B1+...` arrived as `[-j+Akt~o/GlpB*.Enc_[q-fylv+As`, different on every try, under either
# keyboard: type-probe.ps1 beside this script), while SendInput reached it intact (9 of 9:
# type-probe-2.ps1). So every key of this run goes through SendInput.
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices; using System.Threading;
namespace RangeFinder {
public static class Keys {
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit, Size = 40)] struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public KEYBDINPUT ki; }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] i, int size);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern short VkKeyScan(char c);
    static void One(ushort vk, bool up, bool extended) {
        var i = new INPUT[1]; i[0].type = 1; i[0].ki.wVk = vk; i[0].ki.wScan = (ushort)MapVirtualKey(vk, 0);
        i[0].ki.dwFlags = (up ? 2u : 0u) | (extended ? 1u : 0u);
        if (SendInput(1, i, Marshal.SizeOf(typeof(INPUT))) != 1) throw new Exception("SendInput failed: " + Marshal.GetLastWin32Error());
    }
    public static void Tap(ushort vk, bool extended) { One(vk, false, extended); Thread.Sleep(5); One(vk, true, extended); }
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

# The pixel readings, in C#: PowerShell is too slow for a loop over a 3840-pixel-wide screenshot.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace RangeFinder {
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

    // Saturated colours along a strip of text, left to right. A pixel is saturated when
    // max(R,G,B) - min(R,G,B) > thr. For each column the most saturated pixel stands for it; columns
    // at most gap apart whose colours are within 30 degrees of hue make one run.
    public static List<object> TextRuns(Img img, int x0, int y0, int x1, int y1, int thr, int ox, int gap) {
        var runs = new List<object>();
        int runStart = -1, runEnd = -1, peak = -1, n = 0; var colCounts = new Dictionary<int, int>();
        Action close = () => {
            if (runStart < 0) return;
            int m; int mode = ArgMax(colCounts, out m);
            var r = new Dictionary<string, object>();
            r["x0"] = runStart + ox; r["x1"] = runEnd + ox; r["peak"] = Hex(peak); r["mode"] = Hex(mode);
            r["hue"] = Math.Round(Hue(peak)); r["pixels"] = n;
            runs.Add(r);
        };
        for (int x = x0; x < x1; x++) {
            int best = -1, bestSat = thr, cnt = 0;
            for (int y = y0; y < y1; y++) { int c = img.At(x, y); int s = Sat(c); if (s > thr) { cnt++; if (s > bestSat) { bestSat = s; best = c; } } }
            if (best < 0) continue;
            if (runStart >= 0 && x - runEnd <= gap && HueDiff(best, peak) < 30) {
                runEnd = x; n += cnt; if (Sat(best) > Sat(peak)) peak = best;
            } else {
                close();
                runStart = x; runEnd = x; peak = best; n = cnt; colCounts = new Dictionary<int, int>();
            }
            int k; colCounts.TryGetValue(best, out k); colCounts[best] = k + 1;
        }
        close();
        return runs;
    }

    // A box of (2k+1) x (2k+1) pixels round a point: how many are saturated, and the most frequent
    // saturated colour. A corner square fills much of it; two lines crossing fill a cross.
    public static string Corner(Img img, int x, int y, int k) {
        var c = new Dictionary<int, int>(); int n = 0;
        for (int yy = y - k; yy <= y + k; yy++) for (int xx = x - k; xx <= x + k; xx++) { int p = img.At(xx, yy); if (Sat(p) > 60) { n++; int m; c.TryGetValue(p, out m); c[p] = m + 1; } }
        int mm; int mode = ArgMax(c, out mm);
        return n + " of " + ((2 * k + 1) * (2 * k + 1)) + (mode >= 0 ? " #" + Hex(mode) + ":" + mm : "");
    }

    // Text, left to right: each column stands for the darkest pixel in it (the core of a stroke
    // wears the text's colour; ClearType's coloured fringes are lighter). A column is "colour"
    // when that pixel is saturated (max - min > thr), "dark" when it is not and is dark, else empty.
    // Neighbouring columns of one kind (and, for colour, within 30 degrees of hue) make a run; a gap
    // of up to `gap` empty columns is bridged. Runs of colour two columns wide or less are ClearType
    // fringes beside dark strokes, counted but not listed.
    public static Dictionary<string, object> TextByDarkest(Img img, int x0, int y0, int x1, int y1, int thr, int ox, int gap) {
        var runs = new List<object>(); int fringes = 0;
        string kind = null; int start = -1, last = -1, refColour = -1; var counts = new Dictionary<int, int>();
        Action close = () => {
            if (kind == null) return;
            int width = last - start + 1;
            if (kind == "colour" && width <= 2) { fringes++; }
            else {
                int m; int mode = ArgMax(counts, out m);
                var r = new Dictionary<string, object>();
                r["kind"] = kind; r["x0"] = start + ox; r["x1"] = last + ox; r["colour"] = Hex(mode); r["columns"] = m + "/" + counts.Count;
                runs.Add(r);
            }
            kind = null; counts = new Dictionary<int, int>();
        };
        for (int x = x0; x < x1; x++) {
            int dark = -1, lum = 999;
            for (int y = y0; y < y1; y++) { int c = img.At(x, y); if (c < 0) continue; int l = (R(c) * 30 + G(c) * 59 + B(c) * 11) / 100; if (l < lum) { lum = l; dark = c; } }
            string k = dark < 0 ? null : Sat(dark) > thr ? "colour" : lum < 140 ? "dark" : null;
            if (k == null) { if (kind != null && x - last > gap) close(); continue; }
            bool same = kind == k && (k == "dark" || HueDiff(dark, refColour) < 30);
            if (!same) { close(); kind = k; start = x; refColour = dark; }
            last = x;
            int n; counts.TryGetValue(dark, out n); counts[dark] = n + 1;
        }
        close();
        // Runs of one kind and one colour with nothing else between them are one run.
        var merged = new List<object>();
        foreach (Dictionary<string, object> r in runs) {
            if (merged.Count > 0) {
                var p = (Dictionary<string, object>)merged[merged.Count - 1];
                if ((string)p["kind"] == (string)r["kind"] && (string)p["colour"] == (string)r["colour"] || ((string)p["kind"] == "dark" && (string)r["kind"] == "dark")) {
                    p["x1"] = r["x1"]; p["merged"] = (p.ContainsKey("merged") ? (int)p["merged"] : 1) + 1; continue;
                }
            }
            merged.Add(r);
        }
        var o = new Dictionary<string, object>(); o["runs"] = merged; o["fringeRuns"] = fringes;
        return o;
    }

    // The ground behind text, left to right (cases 29-32: is the pointed text shown selected?). Each
    // column stands for its lightest pixel, which is the ground whatever strokes cross it. Columns
    // whose ground differs from `ground` by more than tol, next to each other, make a run: a
    // selection's highlight. Runs three columns wide or less (a caret, a gridline, a border) are
    // counted but not listed.
    public static Dictionary<string, object> Grounds(Img img, int x0, int y0, int x1, int y1, int ground, int tol, int ox) {
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
            int light = -1, lum = -1;
            for (int y = y0; y < y1; y++) { int c = img.At(x, y); if (c < 0) continue; int l = (R(c) * 30 + G(c) * 59 + B(c) * 11) / 100; if (l > lum) { lum = l; light = c; } }
            if (light < 0 || Dist(light, ground) <= tol) { close(); continue; }
            if (start < 0) start = x;
            last = x;
            int n; counts.TryGetValue(light, out n); counts[light] = n + 1;
        }
        close();
        var o = new Dictionary<string, object>(); o["ground"] = Hex(ground); o["runs"] = runs; o["narrowRuns"] = narrow;
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

$script:BookMark = 'ExSheetVerifyScratch'
$script:Xl = $null
$script:OwnPid = 0
$script:Book = $null
$script:Screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$script:ExcelsBefore = @()

function Write-Line($Row) { [IO.File]::AppendAllText($Log, (($Row | ConvertTo-Json -Compress -Depth 12) + "`n"), $Utf8) }
# A case read again replaces its line.
function Remove-Line([string]$Id) {
    if (-not (Test-Path $Log)) { return }
    $keep = @([IO.File]::ReadAllLines($Log, $Utf8) | Where-Object { $_ -notlike ('{"case":"' + $Id + '",*') })
    [IO.File]::WriteAllLines($Log, [string[]]$keep, $Utf8)
}
function Say([string]$Text) { Write-Host ('{0} {1}' -f (Get-Date -Format 'HH:mm:ss.fff'), $Text) }

# ---- Excel: one of this script's own ----------------------------------------------------------

function Start-OwnExcel {
    $script:ExcelsBefore = @(Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
    $xl = New-Object -ComObject Excel.Application
    $script:Xl = $null
    [uint32]$p = 0
    [void][RangeFinder.Native]::GetWindowThreadProcessId([IntPtr][long]$xl.Hwnd, [ref]$p)
    if ($script:ExcelsBefore -contains [int]$p) {
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl)
        throw "New-Object gave an Excel that was already running (process $p); nothing was changed in it"
    }
    $script:OwnPid = [int]$p
    $xl.Visible = $true
    $xl.DisplayAlerts = $false
    $xl.WindowState = -4137   # xlMaximized
    $script:Xl = $xl
    Update-Hwnd
    Say "own Excel: process $($script:OwnPid); other Excel processes left alone: $($script:ExcelsBefore -join ', ')"
    return $xl
}

function Stop-OwnExcel {
    if ($null -eq $script:Xl) { return }
    try { if ($script:Book) { $script:Book.Close($false) } } catch { }
    $script:Book = $null
    try { $script:Xl.Quit() } catch { }
    try { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($script:Xl) } catch { }
    $script:Xl = $null
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    for ($i = 0; $i -lt 20; $i++) { if (-not (Get-Process -Id $script:OwnPid -ErrorAction SilentlyContinue)) { break }; Start-Sleep -Milliseconds 500 }
    if (Get-Process -Id $script:OwnPid -ErrorAction SilentlyContinue) { Stop-Process -Id $script:OwnPid -Force; Say "own Excel $($script:OwnPid) ended by Stop-Process" }
    else { Say "own Excel $($script:OwnPid) quit" }
}

# A fresh workbook for each case: one sheet, Sheet1, the zoom asked, A1 in view, D10 selected. The
# previous case's workbook is closed unsaved first. The workbook is marked by a hidden Name, so it
# never shows in Formula AutoComplete.
function New-CaseBook([int]$Zoom = 100) {
    $xl = $script:Xl
    if ($script:Book) { try { $script:Book.Close($false) } catch { }; $script:Book = $null }
    $wb = $xl.Workbooks.Add()
    [void]$wb.Names.Add($script:BookMark, '=TRUE', $false)
    $wb.EnableAutoRecover = $false
    while ($wb.Worksheets.Count -gt 1) { $wb.Worksheets.Item($wb.Worksheets.Count).Delete() }
    $ws = $wb.Worksheets.Item(1)
    if ($ws.Name -ne 'Sheet1') { $ws.Name = 'Sheet1' }
    [void]$ws.Activate()
    $xl.ActiveWindow.WindowState = -4137
    $xl.ActiveWindow.Zoom = $Zoom
    [void]$xl.Goto($ws.Range('A1'), $true)
    $script:Book = $wb
    Update-Hwnd
    return $ws
}
function Update-Hwnd {
    # Each workbook has a window of its own; keys go to the active one's.
    $script:Hwnd = [IntPtr][long]$script:Xl.Hwnd
    Set-Content $script:HwndFile ([long]$script:Hwnd)
}
function Select-D10 { $ws = $script:Xl.ActiveSheet; [void]$script:Xl.Goto($ws.Range('A1'), $true); [void]$ws.Range('D10').Select() }

# Escape until Excel answers COM again (it refuses every call while an edit is open).
function Until-Ready([int]$Tries = 8) {
    for ($i = 0; $i -lt $Tries; $i++) {
        Send-KeysRaw '{ESC}' 350
        try { [void]$script:Xl.ActiveSheet.Name; if ($script:Xl.Ready) { return $i + 1 } } catch { }
    }
    throw 'Excel did not come back to Ready'
}

# ---- The keyboard -------------------------------------------------------------------------------

function Get-ExcelLayout { [uint32]$p = 0; $t = [RangeFinder.Native]::GetWindowThreadProcessId($script:Hwnd, [ref]$p); return ('0x{0:x8}' -f [long][RangeFinder.Native]::GetKeyboardLayout($t)) }
function Get-OwnLayout { return ('0x{0:x8}' -f [long][RangeFinder.Native]::GetKeyboardLayout(0)) }
# Excel's window to the English (UK) keyboard, as Win+Space would; and this thread too, because
# SendKeys turns each character into a key through the sending thread's layout (VkKeyScan), and
# Excel turns the key back into a character through its own.
function Set-EnglishKeyboard {
    $script:ExcelLayoutWas = Get-ExcelLayout
    $script:OwnLayoutWas = Get-OwnLayout
    Show-Excel $script:Xl
    [void][RangeFinder.Native]::PostMessage($script:Hwnd, 0x0050, [IntPtr]::Zero, [IntPtr]0x08090809)   # WM_INPUTLANGCHANGEREQUEST
    Start-Sleep -Milliseconds 600
    $uk = [RangeFinder.Native]::LoadKeyboardLayout('00000809', 1)   # KLF_ACTIVATE, this thread
    [void][RangeFinder.Native]::ActivateKeyboardLayout($uk, 0)
    $vk = [ordered]@{}
    foreach ($c in @('=', '+', '!', '$', ':', ',', '"', '&', '(', ')', '[', ']', "'")) { $vk[$c] = ('0x{0:x3}' -f [int][RangeFinder.Native]::VkKeyScan([char]$c)) }
    return [ordered]@{ excelWas = $script:ExcelLayoutWas; excelNow = Get-ExcelLayout; ownWas = $script:OwnLayoutWas; ownNow = Get-OwnLayout; vkKeyScan = $vk }
}
function Restore-Keyboard {
    Show-Excel $script:Xl
    [void][RangeFinder.Native]::PostMessage($script:Hwnd, 0x0050, [IntPtr]::Zero, [IntPtr][long]([Convert]::ToInt64($script:ExcelLayoutWas, 16)))
    Start-Sleep -Milliseconds 600
    [RangeFinder.Native]::keybd_event(0x1A, 0, 0, [UIntPtr]::Zero); [RangeFinder.Native]::keybd_event(0x1A, 0, 2, [UIntPtr]::Zero)   # VK_IME_OFF
    Start-Sleep -Milliseconds 200
    return [ordered]@{ excelNow = Get-ExcelLayout }
}

# Literal text for SendKeys: + ^ % ~ ( ) [ ] { } braced.
function Lit([string]$Text) { return ($Text -replace '([+^%~(){}\[\]])', '{$1}') }

# ---- Geometry, read through COM while Excel is Ready --------------------------------------------

function Get-FormulaBarRect {
    $root = [Windows.Automation.AutomationElement]::FromHandle($script:Hwnd)
    $cond = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, 'Formula Bar')
    $el = $root.FindFirst([Windows.Automation.TreeScope]::Descendants, $cond)
    $script:FbEl = $el
    if ($null -eq $el) { return $null }
    $r = $el.Current.BoundingRectangle
    return [ordered]@{ Left = [int]$r.Left; Top = [int]$r.Top; Right = [int]$r.Right; Bottom = [int]$r.Bottom; control = [string]$el.Current.ControlType.ProgrammaticName; class = [string]$el.Current.ClassName }
}
# A cell the case names may be a token: LASTROW:A (the last row of the view that is whole, in
# column A) or LASTCOL:1 (the last whole column, in row 1).
function Resolve-Cell([string]$A1) {
    $v = $script:Xl.ActiveWindow.VisibleRange
    if ($A1 -like 'LASTROW:*') { return ($A1.Substring(8) + [string]($v.Row + $v.Rows.Count - 2)) }
    if ($A1 -like 'LASTCOL:*') { $c = $script:Xl.ActiveSheet.Cells.Item([int]$A1.Substring(8), $v.Column + $v.Columns.Count - 2); return [string]$c.Address($false, $false) }
    return $A1
}
function Get-Geometry([string[]]$Cells, [string]$Reference) {
    $w = New-Object ExcelDriver.Native+RECT
    [void][ExcelDriver.Native]::GetWindowRect($script:Hwnd, [ref]$w)
    $cap = if ($script:Capture -eq 'window') { [ordered]@{ Left = $w.Left; Top = $w.Top; Right = $w.Right; Bottom = $w.Bottom; method = 'window (PrintWindow, PW_RENDERFULLCONTENT)' } }
           else { [ordered]@{ Left = [Math]::Max($w.Left, $script:Screen.Left); Top = [Math]::Max($w.Top, $script:Screen.Top); Right = [Math]::Min($w.Right, $script:Screen.Right); Bottom = [Math]::Min($w.Bottom, $script:Screen.Bottom); method = 'screen (CopyFromScreen)' } }
    $rects = [ordered]@{}
    foreach ($c in (@($Cells) + @('D10', $Reference) | Select-Object -Unique)) {
        $a = Resolve-Cell $c
        $r = Get-CellRect $script:Xl $a
        $rects[$a] = [ordered]@{ Left = [int]$r.Left; Top = [int]$r.Top; Right = [int]$r.Right; Bottom = [int]$r.Bottom }
    }
    $v = $script:Xl.ActiveWindow.VisibleRange
    return [ordered]@{
        window = [ordered]@{ Left = $w.Left; Top = $w.Top; Right = $w.Right; Bottom = $w.Bottom }
        capture = $cap
        zoom = [int]$script:Xl.ActiveWindow.Zoom
        visible = [string]$v.Address($false, $false)
        sheets = @($script:Xl.ActiveWorkbook.Worksheets | ForEach-Object { [string]$_.Name })
        activeSheet = [string]$script:Xl.ActiveSheet.Name
        selection = [string]$script:Xl.Selection.Address($false, $false)
        d10Formula = [string]$script:Xl.ActiveSheet.Range('D10').Formula2
        cells = $rects
        reference = (Resolve-Cell $Reference)
        formulaBar = (Get-FormulaBarRect)
        excelLayout = (Get-ExcelLayout)
        hwnd = [long]$script:Hwnd; activeWindowHwnd = [long]$script:Xl.ActiveWindow.Hwnd
        dpi = [int][RangeFinder.Native]::GetDpiForWindow($script:Hwnd)
    }
}

# What the Formula Bar holds, read through UI Automation (not COM, which Excel refuses mid-edit): the
# check that the keys arrived as typed.
$script:FbEl = $null
$script:ReadSelections = $true
function Read-FormulaBarText {
    if ($null -eq $script:FbEl) { return $null }
    try { return [string]$script:FbEl.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value } catch { }
    try { return [string]$script:FbEl.GetCurrentPattern([Windows.Automation.TextPattern]::Pattern).DocumentRange.GetText(-1) } catch { }
    return $null
}
# The selected text, read through UI Automation's TextPattern (cases 29-32): in the Formula Bar, and
# in whatever element of Excel's has the keyboard focus (the in-cell editor, when it is one).
function Read-Selection($El) {
    if ($null -eq $El) { return $null }
    try {
        $tp = $El.GetCurrentPattern([Windows.Automation.TextPattern]::Pattern)
        return [ordered]@{ selection = @($tp.GetSelection() | ForEach-Object { [string]$_.GetText(-1) }); document = [string]$tp.DocumentRange.GetText(-1) }
    } catch { return [ordered]@{ error = ([string]$_.Exception.Message).Split("`n")[0] } }
}
function Read-Selections {
    $o = [ordered]@{ formulaBar = (Read-Selection $script:FbEl) }
    try {
        $fe = [Windows.Automation.AutomationElement]::FocusedElement
        if ($fe -and $fe.Current.ProcessId -eq $script:OwnPid) {
            $o.focused = [ordered]@{ class = [string]$fe.Current.ClassName; control = [string]$fe.Current.ControlType.ProgrammaticName; text = (Read-Selection $fe) }
        } else { $o.focused = 'the focus is not in this run''s Excel' }
    } catch { $o.focused = [ordered]@{ error = ([string]$_.Exception.Message).Split("`n")[0] } }
    return $o
}

# ---- Screenshots --------------------------------------------------------------------------------

# Two ways to take a screenshot. "screen" copies the screen (CopyFromScreen). "window" asks the
# window to draw itself (PrintWindow with PW_RENDERFULLCONTENT, which includes what DirectX draws):
# on 2026-09-29 from 23:36 the screen copy came back empty (every pixel 0, alpha too) while the
# monitor reported itself off line, and only the window's own rendering could be read. A window
# capture holds the window alone: a popup of Excel's (an AutoComplete list, a ScreenTip) is a window
# of its own and is not in it.
$script:Capture = 'screen'
function Test-ScreenCapture {
    $b = New-Object Drawing.Bitmap 64, 64
    $g = [Drawing.Graphics]::FromImage($b); $g.CopyFromScreen(200, 200, 0, 0, $b.Size); $g.Dispose()
    $blank = $true
    for ($y = 0; $y -lt 64 -and $blank; $y += 7) { for ($x = 0; $x -lt 64; $x += 7) { if ($b.GetPixel($x, $y).A -ne 0) { $blank = $false; break } } }
    $b.Dispose()
    return (-not $blank)
}
function Grab($Cap) {
    $bmp = New-Object Drawing.Bitmap ($Cap.Right - $Cap.Left), ($Cap.Bottom - $Cap.Top)
    $g = [Drawing.Graphics]::FromImage($bmp)
    if ($script:Capture -eq 'window') {
        $hdc = $g.GetHdc()
        $ok = [RangeFinder.Native]::PrintWindow($script:Hwnd, $hdc, 2)   # PW_RENDERFULLCONTENT
        $g.ReleaseHdc($hdc)
        if (-not $ok) { throw 'PrintWindow failed' }
    } else {
        $g.CopyFromScreen($Cap.Left, $Cap.Top, 0, 0, $bmp.Size)
    }
    $g.Dispose()
    return $bmp
}
# The account's initials sit at the right of the title bar, left of the window's buttons.
function Blank-Account($Bmp) {
    $g = [Drawing.Graphics]::FromImage($Bmp)
    $x0 = $Bmp.Width - 640; $ground = $Bmp.GetPixel($x0 - 10, 40)
    $brush = New-Object Drawing.SolidBrush $ground
    $g.FillRectangle($brush, $x0, 0, 415, 88)
    $brush.Dispose(); $g.Dispose()
}
function Save-Crop([string]$FromFile, $Cap, $Rect, [string]$Name, [int]$Pad = 0) {
    $src = [Drawing.Image]::FromFile($FromFile)
    try {
        $l = [Math]::Max([int]$Rect.Left - $Pad - $Cap.Left, 0); $t = [Math]::Max([int]$Rect.Top - $Pad - $Cap.Top, 0)
        $r = [Math]::Min([int]$Rect.Right + $Pad - $Cap.Left, $src.Width); $b = [Math]::Min([int]$Rect.Bottom + $Pad - $Cap.Top, $src.Height)
        $bmp = New-Object Drawing.Bitmap ($r - $l), ($b - $t)
        $g = [Drawing.Graphics]::FromImage($bmp)
        $g.DrawImage($src, (New-Object Drawing.Rectangle 0, 0, ($r - $l), ($b - $t)), (New-Object Drawing.Rectangle $l, $t, ($r - $l), ($b - $t)), [Drawing.GraphicsUnit]::Pixel)
        $g.Dispose()
        $path = Join-Path $script:Shots ($Name + '.png')
        $bmp.Save($path, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
        return "shots/$Name.png"
    } finally { $src.Dispose() }
}

# One state of a case: its action (keys, a click), 600 ms, a screenshot, 300 ms, another.
function Take-State([string]$Id, [string]$State, [scriptblock]$Action) {
    if ($Action) { & $Action }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    Start-Sleep -Milliseconds 600
    $a = Grab $script:Geo.capture
    $atA = $sw.ElapsedMilliseconds
    while ($sw.ElapsedMilliseconds -lt $atA + 300) { Start-Sleep -Milliseconds 5 }
    $b = Grab $script:Geo.capture
    $atB = $sw.ElapsedMilliseconds
    $base = Join-Path $Raw ("{0}-{1}" -f $Id, $State)
    $a.Save("$base-600.png", [Drawing.Imaging.ImageFormat]::Png)
    $b.Save("$base-900.png", [Drawing.Imaging.ImageFormat]::Png)
    $a.Dispose(); $b.Dispose()
    $seen = Read-FormulaBarText
    Say "  $Id $State shots at ${atA} ms and ${atB} ms after the action; the Formula Bar holds: $seen"
    $row = [ordered]@{ state = $State; shotAtMs = $atA; secondShotAtMs = $atB; formulaBarHolds = $seen }
    if ($script:ReadSelections) { $row.selections = Read-Selections; Say ("  selections: " + ($row.selections | ConvertTo-Json -Compress -Depth 6)) }
    return $row
}

# ---- The readings, from the saved screenshots ---------------------------------------------------

$script:Tol = 12
function Local-Rect($R, $Cap) { return @{ L = [int]$R.Left - $Cap.Left; T = [int]$R.Top - $Cap.Top; R = [int]$R.Right - $Cap.Left; B = [int]$R.Bottom - $Cap.Top } }

function Read-State($Geo, [string]$Id, $StateRow) {
    $cap = $Geo.capture
    $base = Join-Path $Raw ("{0}-{1}" -f $Id, $StateRow.state)
    $open = [RangeFinder.Img]::Load("$base-600.png")
    $open2 = [RangeFinder.Img]::Load("$base-900.png")
    $before = [RangeFinder.Img]::Load((Join-Path $Raw "$Id-before.png"))
    # The ground and the gridline, from the reference cell in the "before" shot: its middle, and the
    # darkest pixel within 2 px of its right edge, halfway down.
    $ref = Local-Rect $Geo.cells.($Geo.reference) $cap
    $ground = [RangeFinder.Px]::Mode($before, $ref.L + 6, $ref.T + 4, $ref.R - 6, $ref.B - 4)
    $midY = [int](($ref.T + $ref.B) / 2); $grid = -1; $dark = 999
    for ($x = $ref.R - 2; $x -le $ref.R + 2; $x++) { $c = $before.At($x, $midY); $s = (($c -shr 16) -band 255) + (($c -shr 8) -band 255) + ($c -band 255); if ($s -lt $dark) { $dark = $s; $grid = $c } }
    $cells = [ordered]@{}
    foreach ($name in $Geo.cells.PSObject.Properties.Name) {
        if ($name -eq $Geo.reference) { continue }
        $r = Local-Rect $Geo.cells.$name $cap
        if ($r.R -le 0 -or $r.B -le 0 -or $r.L -ge $open.W -or $r.T -ge $open.H) { $cells[$name] = 'not in view'; continue }
        # The cell's own ground, from the "before" shot, inside its edges.
        $cellGround = [RangeFinder.Px]::Mode($before, $r.L + 5, $r.T + 4, $r.R - 5, $r.B - 4)
        $edges = [ordered]@{}
        foreach ($side in 'top', 'right', 'bottom', 'left') {
            $edges[$side] = [RangeFinder.Px]::EdgeReport($open, $before, $side, $r.L, $r.T, $r.R, $r.B, [int[]](1, 2, 3), $cellGround, $grid, $script:Tol, 4)
        }
        $w = $r.R - $r.L; $h = $r.B - $r.T
        $cells[$name] = [ordered]@{
            rect = @($Geo.cells.$name.Left, $Geo.cells.$name.Top, $Geo.cells.$name.Right, $Geo.cells.$name.Bottom)
            ground = [RangeFinder.Px]::Hex($cellGround)
            centre = [RangeFinder.Px]::Hex($open.At([int](($r.L + $r.R) / 2), [int](($r.T + $r.B) / 2)))
            centreBefore = [RangeFinder.Px]::Hex($before.At([int](($r.L + $r.R) / 2), [int](($r.T + $r.B) / 2)))
            # Above the text: the middle of the cell's width, its top quarter.
            fill = [RangeFinder.Px]::Hex([RangeFinder.Px]::Mode($open, $r.L + [int]($w * 0.3), $r.T + [int]($h * 0.12), $r.R - [int]($w * 0.3), $r.T + [int]($h * 0.3)))
            fillBefore = [RangeFinder.Px]::Hex([RangeFinder.Px]::Mode($before, $r.L + [int]($w * 0.3), $r.T + [int]($h * 0.12), $r.R - [int]($w * 0.3), $r.T + [int]($h * 0.3)))
            edges = $edges
            corners = [ordered]@{
                topLeft = [RangeFinder.Px]::Corner($open, $r.L - 1, $r.T, 4); topRight = [RangeFinder.Px]::Corner($open, $r.R - 1, $r.T, 4)
                bottomRight = [RangeFinder.Px]::Corner($open, $r.R - 1, $r.B, 4); bottomLeft = [RangeFinder.Px]::Corner($open, $r.L - 1, $r.B, 4) }
            movedIn300ms = [RangeFinder.Px]::Diff($open, $open2, $r.L - 6, $r.T - 6, $r.R + 6, $r.B + 6, $script:Tol, $cap.Left, $cap.Top)
        }
    }
    # The text: the Formula Bar's box, and row 10 from D10 rightwards (the in-cell editor).
    $text = [ordered]@{}
    if ($Geo.formulaBar) {
        $f = Local-Rect $Geo.formulaBar $cap
        # The line of text only: the box's own border (black while it has focus) runs 12 px inside
        # the element's top, 11 px inside its bottom and 5 px inside its left, at 150%.
        $text['formulaBar'] = [RangeFinder.Px]::TextByDarkest($open, $f.L + 8, $f.T + 16, $f.R - 30, $f.B - 15, 60, $cap.Left, 6)
        $text['formulaBarMovedIn300ms'] = [RangeFinder.Px]::Diff($open, $open2, $f.L, $f.T, $f.R, $f.B, $script:Tol, $cap.Left, $cap.Top)
        # The ground behind the Formula Bar's text: a run is a highlight (text shown selected).
        $fg = [RangeFinder.Px]::Mode($open, $f.L + 8, $f.T + 16, $f.R - 30, $f.B - 15)
        $text['formulaBarGround'] = [RangeFinder.Px]::Grounds($open, $f.L + 8, $f.T + 16, $f.R - 30, $f.B - 15, $fg, $script:Tol, $cap.Left)
    }
    $d = Local-Rect $Geo.cells.D10 $cap
    $editor = @{ L = $d.L + 3; T = $d.T + 3; R = [Math]::Min($d.L + 1500, $open.W); B = $d.B - 3 }
    $text['cell'] = [RangeFinder.Px]::TextByDarkest($open, $editor.L, $editor.T, $editor.R, $editor.B, 60, $cap.Left, 6)
    # The ground behind the in-cell editor's text, within D10 (the editor's ground is D10's).
    $eg = [RangeFinder.Px]::Mode($open, $editor.L, $editor.T, $d.R - 3, $editor.B)
    $text['cellGround'] = [RangeFinder.Px]::Grounds($open, $editor.L, $editor.T, $editor.R, $editor.B, $eg, $script:Tol, $cap.Left)
    return [ordered]@{
        state = $StateRow.state; shotAtMs = $StateRow.shotAtMs; secondShotAtMs = $StateRow.secondShotAtMs
        formulaBarHolds = $StateRow.formulaBarHolds
        selections = $(if ($StateRow.PSObject.Properties['selections']) { $StateRow.selections } else { $null })
        action = $(if ($StateRow.PSObject.Properties['action']) { $StateRow.action } else { $null })
        keysSent = $(if ($StateRow.PSObject.Properties['keys']) { $StateRow.keys } else { $null })
        groundAtReference = [RangeFinder.Px]::Hex($ground); gridline = [RangeFinder.Px]::Hex($grid)
        cells = $cells
        text = $text
        windowMovedIn300ms = [RangeFinder.Px]::Diff($open, $open2, 0, 0, $open.W, $open.H, $script:Tol, $cap.Left, $cap.Top)
    }
}

# The outlines. A cell inside an outline wears its pale fill, so the cells that share a fill they did
# not have before make one outline; its line colours are read on their edges. A line seen on a cell
# with no fill of its own belongs to an outline next to it (or to one with no fill).
function Split-Address([string]$A1) {
    $m = [regex]::Match($A1, '^([A-Z]+)([0-9]+)$')
    $col = 0; foreach ($ch in $m.Groups[1].Value.ToCharArray()) { $col = $col * 26 + ([int]$ch - 64) }
    return @($col, [int]$m.Groups[2].Value)
}
function Column-Letters([int]$N) { $s = ''; while ($N -gt 0) { $r = ($N - 1) % 26; $s = [char](65 + $r) + $s; $N = [int][Math]::Floor(($N - 1) / 26) }; return $s }
function Get-Outlines($Read) {
    $groups = [ordered]@{}; $unfilled = @()
    foreach ($name in $Read.cells.Keys) {
        $c = $Read.cells[$name]; if ($c -is [string]) { continue }
        if ($c.fill -ne $c.fillBefore) {
            if (-not $groups.Contains($c.fill)) { $groups[$c.fill] = New-Object System.Collections.ArrayList }
            [void]$groups[$c.fill].Add($name)
        } else { $unfilled += $name }
    }
    $out = @()
    foreach ($f in $groups.Keys) {
        # Cells next to each other (sharing an edge) make one part; one outline is one part.
        $left = New-Object System.Collections.ArrayList; foreach ($n in $groups[$f]) { [void]$left.Add($n) }
        while ($left.Count) {
            $part = New-Object System.Collections.ArrayList; [void]$part.Add($left[0]); $left.RemoveAt(0)
            $grew = $true
            while ($grew) {
                $grew = $false
                foreach ($n in @($left)) {
                    $a = Split-Address $n
                    foreach ($m in @($part)) { $b = Split-Address $m; if ([Math]::Abs($a[0] - $b[0]) + [Math]::Abs($a[1] - $b[1]) -eq 1) { [void]$part.Add($n); $left.Remove($n); $grew = $true; break } }
                }
            }
            $names = @($part); $cols = @(); $rows = @()
            foreach ($n in $names) { $a = Split-Address $n; $cols += $a[0]; $rows += $a[1] }
            $range = '{0}{1}:{2}{3}' -f (Column-Letters ($cols | Measure-Object -Minimum).Minimum), ($rows | Measure-Object -Minimum).Minimum, (Column-Letters ($cols | Measure-Object -Maximum).Maximum), ($rows | Measure-Object -Maximum).Maximum
            $lines = @()
            foreach ($n in $names) { foreach ($side in 'top', 'right', 'bottom', 'left') { $e = $Read.cells[$n].edges[$side]; if ($e['line']) { $lines += "$n $side #$($e['line'])" } } }
            $out += [ordered]@{ fill = $f; cells = ($names -join ' '); range = $range; lines = ($lines -join '; ') }
        }
    }
    $loose = @()
    foreach ($n in $unfilled) { foreach ($side in 'top', 'right', 'bottom', 'left') { $e = $Read.cells[$n].edges[$side]; if ($e['line']) { $loose += "$n $side #$($e['line'])" } } }
    if ($loose.Count) { $out += [ordered]@{ fill = $null; cells = ($unfilled -join ' '); range = $null; lines = ($loose -join '; ') } }
    return $out
}

function Save-CaseShots($Geo, [string]$Id, $StateRows, [string[]]$Cells) {
    $cap = $Geo.capture
    $shots = [ordered]@{}
    # The cells the case names, together, with a margin.
    $l = 1e9; $t = 1e9; $r = -1e9; $b = -1e9
    foreach ($n in $Geo.cells.PSObject.Properties.Name) {
        if ($n -eq $Geo.reference) { continue }
        $c = $Geo.cells.$n
        if ($c.Left -lt $l) { $l = $c.Left }; if ($c.Top -lt $t) { $t = $c.Top }; if ($c.Right -gt $r) { $r = $c.Right }; if ($c.Bottom -gt $b) { $b = $c.Bottom }
    }
    $cellsRect = @{ Left = [Math]::Max($l, $cap.Left); Top = [Math]::Max($t, $cap.Top); Right = [Math]::Min($r, $cap.Right - 40); Bottom = [Math]::Min($b, $cap.Bottom - 60) }
    $d = $Geo.cells.D10
    $editorRect = @{ Left = $d.Left; Top = $d.Top; Right = [Math]::Min($d.Left + 1500, $cap.Right); Bottom = $d.Bottom }
    $shots.before = Save-Crop (Join-Path $Raw "$Id-before.png") $cap $cellsRect ("A{0}-before-cells" -f $Id) 16
    foreach ($s in $StateRows) {
        $st = $s.state
        $src = Join-Path $Raw "$Id-$st-600.png"
        $win = Join-Path $script:Shots ("A{0}-{1}-window.png" -f $Id, $st)
        $bmp = New-Object Drawing.Bitmap $src
        $copy = New-Object Drawing.Bitmap $bmp.Width, $bmp.Height
        $g = [Drawing.Graphics]::FromImage($copy); $g.DrawImage($bmp, 0, 0, $bmp.Width, $bmp.Height); $g.Dispose(); $bmp.Dispose()
        Blank-Account $copy
        $copy.Save($win, [Drawing.Imaging.ImageFormat]::Png); $copy.Dispose()
        $one = [ordered]@{ window = "shots/A$Id-$st-window.png" }
        if ($Geo.formulaBar) { $one.formulaBar = Save-Crop $src $cap $Geo.formulaBar ("A{0}-{1}-formula-bar" -f $Id, $st) 4 }
        $one.cell = Save-Crop $src $cap $editorRect ("A{0}-{1}-cell" -f $Id, $st) 8
        $one.cells = Save-Crop $src $cap $cellsRect ("A{0}-{1}-cells" -f $Id, $st) 16
        $one.cells900ms = Save-Crop (Join-Path $Raw "$Id-$st-900.png") $cap $cellsRect ("A{0}-{1}-cells-900ms" -f $Id, $st) 16
        $shots[$st] = $one
    }
    return $shots
}

function Analyse-Case([string]$Id) {
    $saved = Get-Content (Join-Path $Raw "$Id.json") -Raw | ConvertFrom-Json
    $reads = @()
    foreach ($s in $saved.states) {
        $read = Read-State $saved.geometry $Id $s
        $read['outlines'] = @(Get-Outlines $read)
        $reads += $read
    }
    $shots = Save-CaseShots $saved.geometry $Id $saved.states $saved.cells
    $row = [ordered]@{ case = $Id; asked = $saved.asked; reading = $saved.reading; setUp = $saved.setUp; keys = $saved.keys
        time = $saved.time; geometry = $saved.geometry; states = $reads; shots = $shots; notes = $saved.notes }
    Remove-Line $Id
    Write-Line $row
    foreach ($r in $reads) {
        Say ("  {0} {1}: the Formula Bar holds {2}" -f $Id, $r.state, $r.formulaBarHolds)
        foreach ($o in $r.outlines) { Say ("    fill #{0} over {1} ({2}): {3}" -f $o.fill, $o.range, $o.cells, $o.lines) }
        $fmt = { param($t) ($t['runs'] | ForEach-Object { if ($_['kind'] -eq 'dark') { 'dark' } else { '#' + $_['colour'] } }) -join ' ' }
        if ($r.text.Contains('formulaBar')) { Say ("    Formula Bar text: {0}" -f (& $fmt $r.text['formulaBar'])) }
        Say ("    cell text: {0}" -f (& $fmt $r.text['cell']))
        $gfmt = { param($g) ('ground #' + $g['ground'] + '; ' + (($g['runs'] | ForEach-Object { '#{0} at {1}-{2}' -f $_['colour'], $_['x0'], $_['x1'] }) -join ', ')) }
        if ($r.text.Contains('formulaBarGround')) { Say ("    Formula Bar ground: {0}" -f (& $gfmt $r.text['formulaBarGround'])) }
        Say ("    cell ground: {0}" -f (& $gfmt $r.text['cellGround']))
    }
}

# ---- The cases ----------------------------------------------------------------------------------

$Cases = [ordered]@{}
function Def([string]$Id, [string]$Asked, [string]$Reading, [string]$Typed, [string[]]$Cells, [scriptblock]$SetUp = $null, [string]$SetUpText = 'none', [int]$Zoom = 100, $States = $null) {
    if ($null -eq $States) { $States = @(@{ state = 'typed'; keys = (Lit $Typed) }) }
    $Cases[$Id] = [pscustomobject]@{ Id = $Id; Asked = $Asked; Reading = $Reading; Typed = $Typed; Cells = $Cells; SetUp = $SetUp; SetUpText = $SetUpText; Zoom = $Zoom; States = $States }
}

$row1 = @('A1', 'B1', 'C1', 'D1', 'E1', 'F1', 'G1', 'H1', 'I1', 'J1')
Def '1' 'The colour of each Reference, in order. After how many the colours repeat' 'eight colours, then round again' '=A1+B1+C1+D1+E1+F1+G1+H1+I1+J1' $row1
Def '2' 'One colour or two; one outline or two' 'one colour, one outline' '=A1+A1' @('A1')
Def '3' 'One colour or two; one outline or two' 'one colour, one outline' '=A1+$A$1' @('A1')
Def '4' 'The colour of each of the three' 'A1 first colour, B1 second, the second A1 first' '=A1+B1+A1' @('A1', 'B1')
Def '5' "The outline's cells" 'A1:B2' '=B2:A1' @('A1', 'B1', 'A2', 'B2', 'C1', 'C2', 'A3', 'B3')
Def '6' 'Colours and outlines' 'two colours, two outlines' '=A1:B2+B2' @('A1', 'B1', 'A2', 'B2')
Def '7' "B1's colour before the deletion and after" 'first colour after: colours follow first appearance' '=A1+B1, then F2, {HOME}{RIGHT}{DEL 3}' @('A1', 'B1') -States @(
    @{ state = 'typed'; keys = (Lit '=A1+B1') },
    @{ state = 'after-deletion'; keys = '{F2}{HOME}{RIGHT}{DEL 3}' })
Def '8' 'Coloured? Outlined?' 'both' '=Sheet1!A1' @('A1')
Def '9' 'Is Sheet2!A1 coloured? B1''s colour' 'Sheet2!A1 uncoloured, B1 first colour' '=Sheet2!A1+B1' @('A1', 'B1') -SetUpText 'Add a sheet Sheet2, then activate Sheet1' -SetUp {
    $wb = $script:Xl.ActiveWorkbook
    $s2 = $wb.Worksheets.Add([Type]::Missing, $wb.Worksheets.Item(1)); $s2.Name = 'Sheet2'
    [void]$wb.Worksheets.Item('Sheet1').Activate()
}
Def '10a' "The outline's extent (row 1, row 30 and the last visible row of column A)" 'the whole column' '=SUM(A:A)' @('A1', 'A30', 'LASTROW:A', 'B1', 'B30')
Def '10b' "The outline's extent (column A and the last visible column of row 1)" 'the whole row' '=SUM(1:1)' @('A1', 'H1', 'LASTCOL:1', 'A2', 'H2')
$table = {
    $ws = $script:Xl.ActiveSheet
    $ws.Range('A1').Value2 = 'Id'; $ws.Range('B1').Value2 = 'PV'
    $ws.Range('A2').Value2 = 1; $ws.Range('A3').Value2 = 2; $ws.Range('A4').Value2 = 3
    $ws.Range('B2').Value2 = 100; $ws.Range('B3').Value2 = 250; $ws.Range('B4').Value2 = 75
    $lo = $ws.ListObjects.Add(1, $ws.Range('A1:B4'), [Type]::Missing, 1)   # xlSrcRange, xlYes
    $lo.Name = 'Positions'
}
$tableCells = @('A1', 'B1', 'A2', 'B2', 'A3', 'B3', 'A4', 'B4')
Def '11' 'Coloured? Which cells are outlined: B1 (header), B2:B4 (data), or A1:B4' 'coloured; B2:B4' '=SUM(Positions[PV])' $tableCells -SetUp $table -SetUpText 'A1:B4 as a Table named Positions, headers Id, PV, numbers below (1, 2, 3; 100, 250, 75)'
Def '12' 'Two colours?' 'two' '=SUM(Positions[PV])+SUM(Positions[Id])' $tableCells -SetUp $table -SetUpText 'As 11'
Def '13' 'Is A1 coloured and outlined?' 'yes' '=SUM(A1,' @('A1')
Def '14' 'Is A1 coloured and outlined?' 'yes' '=A1+' @('A1')
Def '15' 'Is the A1 inside the string coloured?' 'only B1' '="A1"&B1' @('A1', 'B1')
Def '16' 'Is LOG10 coloured, or cell LOG10 outlined?' 'only A1' '=LOG10(A1)' @('A1')
Def '17' 'Coloured?' 'yes' '=a1' @('A1')
Def '18' 'Anything coloured or outlined?' 'nothing' 'A1' @('A1')
Def '19' 'The pointed outline: colour, dashed or solid, moving or still' 'dashed, first colour' '=, then {DOWN} (pointing at D11)' @('D11') -States @(
    @{ state = 'pointing'; keys = '={DOWN}' })
Def '20' 'The first outline once pointing has moved on, and the new one' 'the first solid; the new one dashed, second colour' '=, {DOWN}, {+}, {DOWN}' @('D11', 'D12') -States @(
    @{ state = 'first-pointing'; keys = '={DOWN}' },
    @{ state = 'second-pointing'; keys = '{+}{DOWN}' })
# Not in the procedure: case 20's keys point at D11 twice (Point starts again from D10 after the
# +), so the second Reference names the same cell. This one moves on to D12.
Def '20x' 'Not in the procedure: as 20, with the second pointing moved on to D12' '(none: an addition)' '=, {DOWN}, {+}, {DOWN}{DOWN}' @('D11', 'D12', 'D13') -States @(
    @{ state = 'first-pointing'; keys = '={DOWN}' },
    @{ state = 'second-pointing'; keys = '{+}{DOWN}{DOWN}' })
Def '21' 'Outlines shown in each state' 'none when only selected; shown for F2, the double-click and the Formula Bar' '(no keys typed: =A1+B1 written through COM)' @('A1', 'B1') -SetUpText '=A1+B1 written into D10 through COM' -SetUp {
    $script:Xl.ActiveSheet.Range('D10').Formula2 = '=A1+B1'
} -States @(
    @{ state = 'selected'; keys = '' },
    @{ state = 'F2'; keys = '{F2}' },
    @{ state = 'double-click'; action = 'double-click' },
    @{ state = 'formula-bar-click'; action = 'formula-bar-click' })
# Not in the procedure: case 1's keys typed into the Formula Bar (clicked first) instead of D10, for
# the Formula Bar's own colours, which show only while the edit is in the Formula Bar.
Def '1fb' 'Not in the procedure: the colours in the Formula Bar when case 1 is typed there' '(none: an addition)' '=A1+B1+C1+D1+E1+F1+G1+H1+I1+J1, typed into the Formula Bar' $row1 -States @(
    @{ state = 'typed-in-formula-bar'; action = 'formula-bar-type'; keys = (Lit '=A1+B1+C1+D1+E1+F1+G1+H1+I1+J1') })
Def '22' 'Is D10, the cell being edited, outlined?' 'yes' '=D10' @('D10', 'C10', 'E10')
Def '23' 'One outline close up: its width in screen pixels, the fill, the corner squares' 'solid, a pale fill, corner squares' '=A1+B1+C1+D1+E1+F1+G1+H1+I1+J1' @('A1', 'B1', 'C1') -Zoom 400 -SetUpText 'Zoom 400%'
# Cases 24-32, added to the procedure at dd50310 and run on 2026-09-30.
Def '24' 'Coloured? Outlined? Excel reads the entry as =+A1 once entered' 'nothing: only text beginning with = is a Formula while it is typed, as for F4 and Point' '+A1' @('A1', 'B1', 'A2')
Def '25' 'Coloured? Outlined?' 'nothing' '-B2' @('B2', 'A1', 'C3')
Def '26' 'Is A1 coloured and outlined before the second corner is typed?' 'not until the second corner is typed' '=SUM(A1:' @('A1', 'B1', 'A2')
Def '27' 'Is Nope[PV] coloured?' 'not coloured: it names no Table' '=SUM(Nope[PV]' $tableCells -SetUpText 'No Table named Nope (the workbook has no Table at all; checked through COM)' -SetUp {
    if ($script:Xl.ActiveSheet.ListObjects.Count -ne 0) { throw 'the fresh workbook has a Table' }
}
Def '28' 'Is Positions[Nope], a column the Table lacks, coloured?' 'not coloured: it names no column' '=SUM(Positions[Nope]' $tableCells -SetUp $table -SetUpText 'As 11'
Def '29' 'Is the pointed D11''s text shown selected (a grey ground, as D12 in case 20x)?' 'open' '=SUM(, then {DOWN} (=SUM(D11)' @('D11', 'D12') -States @(
    @{ state = 'pointing'; keys = ((Lit '=SUM(') + '{DOWN}') })
Def '30' 'Is the pointed D11''s text shown selected?' 'open' '=1+, then {DOWN} (=1+D11)' @('D11', 'D12') -States @(
    @{ state = 'pointing'; keys = ((Lit '=1+') + '{DOWN}') })
Def '31' 'Is the pointed D12''s text shown selected (one Reference, pointed twice)?' 'open' '=, {DOWN}{DOWN} (=D12)' @('D11', 'D12', 'D13') -States @(
    @{ state = 'pointing'; keys = '={DOWN}{DOWN}' })
Def '32' 'The Formula Bar''s text after 5 (UI Automation): does 5 replace the grey D12 (=D11+5) or follow it (=D11+D125)? Is D12 still outlined, and are the dashes still there?' 'open' '=D11+, {DOWN}{DOWN} (=D11+D12), then 5' @('D11', 'D12', 'D13') -States @(
    @{ state = 'pointing'; keys = ((Lit '=D11+') + '{DOWN}{DOWN}') },
    @{ state = 'after-5'; keys = '5' })

function Run-Case($C) {
    Say "case $($C.Id): $($C.Typed)"
    $ws = New-CaseBook $C.Zoom
    if ($C.SetUp) { & $C.SetUp }
    Select-D10
    Update-Hwnd
    Show-Excel $script:Xl
    $reference = if ($C.Zoom -gt 100) { 'B7' } else { 'H30' }
    $script:Geo = Get-Geometry $C.Cells $reference
    Start-Sleep -Milliseconds 300
    $bmp = Grab $script:Geo.capture; $bmp.Save((Join-Path $Raw "$($C.Id)-before.png"), [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    $states = @(); $notes = @()
    foreach ($s in $C.States) {
        if ($s.ContainsKey('action') -and $s.action -eq 'double-click') {
            [void](Until-Ready)
            $d = $script:Geo.cells.D10; $x = [int](($d.Left + $d.Right) / 2); $y = [int](($d.Top + $d.Bottom) / 2)
            $row = Take-State $C.Id $s.state { Show-Excel $script:Xl; Move-Mouse $x $y; Press-Mouse; Release-Mouse; Press-Mouse; Release-Mouse }
            $row.action = "Escape until Ready, then a double-click at ($x, $y), D10's middle"
        }
        elseif ($s.ContainsKey('action') -and $s.action -eq 'formula-bar-click') {
            [void](Until-Ready)
            $f = $script:Geo.formulaBar
            # Into the text, just right of its last character: the text =A1+B1 is short, so 120 px in.
            $x = [int]$f.Left + 120; $y = [int](($f.Top + $f.Bottom) / 2)
            $row = Take-State $C.Id $s.state { Show-Excel $script:Xl; Move-Mouse $x $y; Press-Mouse; Release-Mouse }
            $row.action = "Escape until Ready, then a click at ($x, $y), in the Formula Bar's text (the Formula Bar found by UI Automation: $($f.Left),$($f.Top)-$($f.Right),$($f.Bottom))"
        }
        elseif ($s.ContainsKey('action') -and $s.action -eq 'formula-bar-type') {
            $f = $script:Geo.formulaBar
            $x = [int]$f.Left + 120; $y = [int](($f.Top + $f.Bottom) / 2)
            $k = $s.keys
            $row = Take-State $C.Id $s.state ([scriptblock]::Create("Show-Excel `$script:Xl; Move-Mouse $x $y; Press-Mouse; Release-Mouse; Start-Sleep -Milliseconds 300; Send-Keys `$script:Xl '$($k -replace "'", "''")' 0"))
            $row.action = "a click at ($x, $y), in the Formula Bar, then the keys"
            $row.keys = $k
        }
        else {
            $k = $s.keys
            $row = Take-State $C.Id $s.state $(if ($k) { [scriptblock]::Create("Send-Keys `$script:Xl '$($k -replace "'", "''")' 0") } else { $null })
            $row.keys = $k
        }
        $states += $row
    }
    $escapes = Until-Ready
    $notes += "Escape pressed $escapes time(s) until Ready"
    # Park the pointer away from the cells, for the next case.
    Move-Mouse ([int]$script:Geo.capture.Right - 300) ([int]$script:Geo.capture.Top + 700)
    $saved = [ordered]@{ case = $C.Id; asked = $C.Asked; reading = $C.Reading; setUp = $C.SetUpText; keys = $C.Typed
        time = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'); geometry = $script:Geo; cells = $C.Cells; states = $states; notes = $notes }
    [IO.File]::WriteAllText((Join-Path $Raw "$($C.Id).json"), ($saved | ConvertTo-Json -Depth 12), $Utf8)
}

# The environment, and the keyboard checked end to end: literal text typed into a cell and read back.
function Run-Environment {
    $envRow = [ordered]@{ case = '0'; time = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'); screenshots = $script:Capture; keys = 'SendInput (virtual-key and scan code per key)' }
    $ws = New-CaseBook 100
    $c2r = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration' -ErrorAction SilentlyContinue
    $envRow.excel = [ordered]@{ version = [string]$script:Xl.Version; build = [string]$script:Xl.Build; versionToReport = [string]$c2r.VersionToReport; platform = [string]$c2r.Platform; channelUrl = [string]$c2r.CDNBaseUrl
        editDirectlyInCell = [bool]$script:Xl.EditDirectlyInCell; formulaAutoComplete = [bool]$script:Xl.DisplayFormulaAutoComplete
        standardFont = [string]$script:Xl.StandardFont; standardFontSize = [double]$script:Xl.StandardFontSize
        gridlineColor = ('{0:x6}' -f [int]$script:Xl.ActiveWindow.GridlineColor); zoom = [int]$script:Xl.ActiveWindow.Zoom }
    $pers = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize'
    $hc = New-Object RangeFinder.Native+HIGHCONTRAST; $hc.cbSize = [uint32][Runtime.InteropServices.Marshal]::SizeOf($hc)
    [void][RangeFinder.Native]::SystemParametersInfo(0x0042, $hc.cbSize, [ref]$hc, 0)   # SPI_GETHIGHCONTRAST
    $envRow.windows = [ordered]@{ appsUseLightTheme = [int]$pers.AppsUseLightTheme; systemUsesLightTheme = [int]$pers.SystemUsesLightTheme
        dpiOfExcelWindow = [int][RangeFinder.Native]::GetDpiForWindow($script:Hwnd); screen = "$($script:Screen.Width)x$($script:Screen.Height)"
        highContrastOn = [bool]($hc.dwFlags -band 1) }
    $theme = Get-ItemProperty 'HKCU:\Software\Microsoft\Office\16.0\Common' -ErrorAction SilentlyContinue
    $envRow.officeThemeRegistry = if ($theme -and $theme.PSObject.Properties['UI Theme']) { [string]$theme.'UI Theme' } else { 'no UI Theme value (Office default)' }

    # File > Account: the version line and the Office Theme, read through UI Automation; a crop of
    # the theme's box only (the page also shows the account's name and address).
    Show-Excel $script:Xl
    Send-Keys $script:Xl '%' 700; Send-Keys $script:Xl 'f' 1800
    $root = [Windows.Automation.AutomationElement]::FromHandle($script:Hwnd)
    $account = $root.FindFirst([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, 'Account')))
    if ($account) {
        $r = $account.Current.BoundingRectangle
        Click-At $script:Xl ([int]($r.Left + $r.Width / 2)) ([int]($r.Top + $r.Height / 2))
        Start-Sleep -Milliseconds 2500
        $names = @()
        foreach ($el in $root.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition)) {
            $n = [string]$el.Current.Name
            if ($n -match 'Version|Build|Channel|Office Theme|Click-to-Run|Microsoft 365|About Excel' -and $n -notmatch '@') {
                $v = ''
                try { $p = $el.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern); $v = [string]$p.Current.Value } catch { }
                $names += [ordered]@{ name = $n; control = [string]$el.Current.ControlType.ProgrammaticName; value = $v }
                if ($n -eq 'Office Theme') {
                    $b = $el.Current.BoundingRectangle
                    $w = New-Object ExcelDriver.Native+RECT; [void][ExcelDriver.Native]::GetWindowRect($script:Hwnd, [ref]$w)
                    $cap = if ($script:Capture -eq 'window') { [ordered]@{ Left = $w.Left; Top = $w.Top; Right = $w.Right; Bottom = $w.Bottom } } else { [ordered]@{ Left = 0; Top = 0; Right = $script:Screen.Right; Bottom = $script:Screen.Bottom } }
                    $whole = Grab $cap
                    $crop = $whole.Clone((New-Object Drawing.Rectangle ([int]$b.Left - 12 - $cap.Left), ([int]$b.Top - 50 - $cap.Top), ([int]$b.Width + 24), ([int]$b.Height + 62)), $whole.PixelFormat)
                    $crop.Save((Join-Path $script:Shots 'A0-office-theme.png'), [Drawing.Imaging.ImageFormat]::Png); $crop.Dispose(); $whole.Dispose()
                }
            }
        }
        $envRow.fileAccount = $names
    } else { $envRow.fileAccount = 'the Account entry was not found by UI Automation' }
    Send-KeysRaw '{ESC}' 900
    try { [void]$script:Xl.ActiveSheet.Name } catch { Send-KeysRaw '{ESC}' 900 }

    # The keyboard: Excel's window and this thread to English (UK); then a check that every
    # character the cases type arrives as itself.
    $envRow.keyboard = Set-EnglishKeyboard
    $probe = "'=Sheet1!A1+`$B`$2:C3,`"x`"&(D4)[E]a"
    Select-D10
    Show-Excel $script:Xl
    $geo = Get-Geometry @() 'H30'
    Send-Keys $script:Xl (Lit $probe) 600
    $seen = Read-FormulaBarText
    $bmp = Grab $geo.capture; $bmp.Save((Join-Path $Raw '0-keyboard-check.png'), [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    $f = $geo.formulaBar
    if ($f) { [void](Save-Crop (Join-Path $Raw '0-keyboard-check.png') $geo.capture $f 'A0-keyboard-check-formula-bar' 4) }
    Send-Keys $script:Xl '~' 800
    $back = [string]$script:Xl.ActiveSheet.Range('D10').Formula2
    $envRow.keyboardCheck = [ordered]@{ typed = $probe; formulaBarBeforeEnter = $seen; readBack = $back; same = ($back -eq $probe.Substring(1)); method = 'SendInput, virtual-key and scan codes, both keyboards English (UK)' }
    Write-Line $envRow
    Say ($envRow | ConvertTo-Json -Compress -Depth 6)
}

# ---- Main ---------------------------------------------------------------------------------------

if ($Analyse) {
    foreach ($id in $Analyse) { Say "reading $id"; Analyse-Case $id }
    return
}

$script:Capture = if (Test-ScreenCapture) { 'screen' } else { 'window' }
Say "screenshots: $($script:Capture)"
[void](Start-OwnExcel)
. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel-2\case-guard.ps1')
Start-CaseGuard
function Reconnect-Excel { Stop-OwnExcel; [void](Start-OwnExcel) }
$keyboardSet = $false
try {
    foreach ($id in $Case) {
        if ($id -eq '0') { Set-CaseDue 120; Run-Environment; $keyboardSet = $true; Clear-CaseDue; continue }
        if (-not $Cases.Contains($id)) { throw "no case $id" }
        if (-not $keyboardSet) { [void](New-CaseBook 100); $k = Set-EnglishKeyboard; $c2r = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration' -ErrorAction SilentlyContinue; Write-Line ([ordered]@{ case = "keyboard"; time = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'); keyboard = $k; excel = [ordered]@{ version = [string]$script:Xl.Version; build = [string]$script:Xl.Build; versionToReport = [string]$c2r.VersionToReport }; screenshots = $script:Capture }); $keyboardSet = $true }
        Set-CaseDue 90
        try { Run-Case $Cases[$id]; Clear-CaseDue; Analyse-Case $id }
        catch {
            Clear-CaseDue
            $row = [ordered]@{ case = $id; step = 'failed'; error = $_.Exception.Message; line = $_.InvocationInfo.ScriptLineNumber }
            Write-Line $row
            Say "case $id failed: $($_.Exception.Message) (line $($_.InvocationInfo.ScriptLineNumber))"
            try { [void](Until-Ready) } catch { }
        }
        if ($script:Guard.Ended) {
            Write-Line ([ordered]@{ case = $id; step = 'Excel ended'; error = $script:Guard.Ended })
            Say "case ${id}: $($script:Guard.Ended)"
            Reconnect-Excel; $keyboardSet = $false
        }
    }
}
finally {
    if ($keyboardSet -and $script:Xl) {
        try { [void](Until-Ready 3) } catch { }
        try { $k = Restore-Keyboard; Write-Line ([ordered]@{ case = 'keyboard'; step = 'restored'; time = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'); keyboard = $k }) } catch { Say "keyboard not restored: $($_.Exception.Message)" }
    }
    Stop-OwnExcel
}
