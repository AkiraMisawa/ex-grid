<#
.SYNOPSIS
    Asks a real Excel every case of the Excel case corpus, and records Excel's answer beside the
    one the engine's tests expect.

.DESCRIPTION
    First run on a real Excel on 2026-09-27; what that run needed is recorded in
    verification/2026-09-27-windows-excel/results.md. Keep it simple.

    ADR-0047 admits a function only when it gives Excel's answer, and its tests are to be written
    from Excel's observed behaviour. The corpus, tests/ExSheet.Engine.Tests/ExcelCases/*.json, is
    read by the engine's tests (ExcelCaseTests) and by this script, so both answer the same cases.

    For each case the script opens a fresh workbook in Excel through COM, enters the case's cells,
    applies its formats and actions, reads the check cell, and compares Excel's answer with the
    case:
      - a case with "engineDiffersByDecision" is compared with its "excelExpect" (the engine
        differs from Excel on purpose, by the ADR it names); with no "excelExpect", Excel's answer
        is only recorded ("recorded")
      - every other case is compared with its "expect"
    and writes ExcelOracle/results-<date>.json: per case, Excel's answer, what was expected, and
    agree / disagree / blocked / recorded.

    How the inputs go in:
      - A cell whose text begins with "=" is a Formula. It goes in through Range.Formula2 where
        Excel has it (Microsoft 365), otherwise Range.Formula. Both take the invariant (en-US)
        syntax the corpus is written in. Formula2 is preferred because Range.Formula applies
        Excel 2019's implicit intersection to a Formula written through it, which is exactly the
        behaviour ADR-0047 rejected; Formula2 gives the answer a user typing into Excel 365 sees.
      - Any other cell is typed text. It goes in through Range.FormulaLocal, which reads it as the
        user interface would under THIS MACHINE'S REGIONAL FORMAT. A case whose "culture" is not
        the machine's (Get-Culture) is therefore reported "blocked": change the Windows regional
        format (ask the user first) and run again with -Area/-Id to answer it.
      - Actions: insertRows/deleteRows/insertColumns/deleteColumns through EntireRow/EntireColumn
        Insert and Delete; fill through Range.AutoFill (xlFillDefault); copy through
        Range.Copy(destination); pasteText through the clipboard and Worksheet.Paste; rename by
        setting Worksheet.Name. "undo" cannot be asked: Excel's undo does not reach changes made
        through COM, so such a case is blocked.
      - "tables" become ListObjects of the same name, columns and rows, placed far to the right
        (column 15000 on). A table still waiting for its data (rows null) has no Excel counterpart.
      - "otherSheets" are added as empty worksheets, so that a Formula naming them is accepted.
      - "format" and "align" actions set Range.NumberFormat and Range.HorizontalAlignment on the
        action's range, which may be whole columns (B:B) or whole rows (2:2); "alignment" is
        read back from Range.HorizontalAlignment. A range with commas (B:B,3:3) is Excel's own
        range of several areas. A "style" action sets whichever of "format" and "align" it gives
        on its range.
      - "setColumnWidth" sets Range.ColumnWidth on the action's range (every column it spans), or,
        for a null width, Range.UseStandardWidth. "width" is compared with the check column's
        ColumnWidth as read before anything else changes it; null means the sheet's StandardWidth.
        With "sizeToFit" it is EntireColumn.AutoFit instead, and the width it gives is Excel's own.
        With "automatic" (a width an entry widened the column to) the case is blocked: Excel sets
        such a width only by widening on entry. "custom" is compared with the customWidth flag
        Excel writes on the check column, read from a copy of the workbook saved as .xlsx
        (absent is false), before the column's width is changed for reading its text.
      - A case with "oracleSkip" is blocked with that reason. When the reason contains "ask by keys",
        the case is one whose answer typed with real keys differs from COM's (a typed Error Value, a
        two-digit year, automatic percent entry, a Formula's result format, widening a column): it is
        blocked through COM and asked when -Keys types it, since the keyboard is Excel's answer for
        ExSheet (ADR-0047, "Observed in Excel, second run").
      - With -Keys, the case's own cells (and "enter" actions) are typed instead: the cell is
        selected, its text is sent as keyboard input (SendInput, one Unicode character at a time,
        a line break as Alt+Enter), and Enter commits it, into a visible Excel in front. What the
        cell editor does and COM does not (a leading + or - making a Formula, AutoComplete,
        AutoCorrect, widening a column) then shows. Under a regional format other than en-US the
        Formulas still go in through Formula2, since the corpus writes them in en-US syntax and a
        user there types the local one. A dialog Excel raises is answered by a watcher on a thread
        of its own: it reads the dialog (UI Automation), records it with the step it came in, and
        presses its default button; after "There's a problem with this formula" it presses Escape
        as well, which cancels the edit, and the entry counts as refused. Tables, fixtures, formats and
        actions other than "enter" still go in through COM. A case with more than -MaxKeyCells
        cells of its own is blocked. Nothing else may take the foreground while this runs.
      - Each step of a case has a time (20 seconds to type an entry, 30 to set up or apply the actions,
        60 to calculate and read the answer). An Excel that has not finished a step in its time,
        or whose dialog does not close, is ended and started again; the case is blocked, with what
        was seen. With -Keys the Excel is also ended when it is not back to Ready five seconds
        after a case. A case's dialogs and any such ending are in its "keyDialogs".
      - "refusedEntry": true expects Excel to refuse one of the case's entries (a COM error when
        the Formula is set); the case agrees when it does, and disagrees when Excel enters it.
      - "columnWidth" sets the check column's ColumnWidth (characters) before anything is entered,
        and its text is read at that width; every other case is read at width 100. The column's
        width after the case's entries is recorded ("columnWidth") and compared with "widens"
        (wider than the sheet's StandardWidth or not). "widthOnEntry" is the engine's answer and is not
        compared; Excel's width is beside it in the results.

    A disagreement is listed for the user. It is never fixed on the spot, and -Update never
    touches it: a human decides whether the engine changes, the case changes, or an ADR does.

.PARAMETER Update
    After the run, rewrite "source" to "observed" in the corpus for every case whose Excel answer
    agreed. Disagreeing, blocked and recorded cases are left exactly as they are.

.EXAMPLE
    pwsh tests/ExSheet.Engine.Tests/ExcelOracle/oracle.ps1
.EXAMPLE
    pwsh tests/ExSheet.Engine.Tests/ExcelOracle/oracle.ps1 -Area xlookup,round -Update
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Cases,
    [string]$Out,
    [string[]]$Area,
    [string[]]$Id,
    [switch]$Update,
    [switch]$Visible,
    [switch]$Keys,
    [int]$MaxKeyCells = 20
)

# Windows PowerShell 5.1 leaves $PSScriptRoot empty in param() defaults when the script is run
# with -File, so the defaults are resolved here instead.
if (-not $Cases) { $Cases = Join-Path $PSScriptRoot '..\ExcelCases' }
if (-not $Out) { $Out = Join-Path $PSScriptRoot ('results-{0}.json' -f (Get-Date -Format 'yyyy-MM-dd')) }
# Run with -File, "-Area xlookup,round" arrives as one string; split it as -Command would have.
if ($Area) { $Area = @($Area | ForEach-Object { $_ -split ',' }) }
if ($Id) { $Id = @($Id | ForEach-Object { $_ -split ',' }) }

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Invariant = [Globalization.CultureInfo]::InvariantCulture
$Utf8 = New-Object Text.UTF8Encoding $false
$Missing = [Type]::Missing

# Excel's error codes as Range.Value2 returns them (an Int32), by their CVErr number.
$ErrorTexts = @{
    2000 = '#NULL!'; 2007 = '#DIV/0!'; 2015 = '#VALUE!'; 2023 = '#REF!'; 2029 = '#NAME?'; 2036 = '#NUM!'
    2042 = '#N/A'; 2043 = '#GETTING_DATA'; 2045 = '#SPILL!'; 2046 = '#CONNECT!'; 2047 = '#BLOCKED!'
    2048 = '#UNKNOWN!'; 2049 = '#FIELD!'; 2050 = '#CALC!'
}
# Range.HorizontalAlignment's XlHAlign values, by the corpus's names.
$AlignmentCodes = @{ general = 1; left = -4131; center = -4108; right = -4152 }
$KnownErrors = @('#NULL!', '#DIV/0!', '#VALUE!', '#REF!', '#NAME?', '#NUM!', '#N/A', '#GETTING_DATA', '#CIRC!', '#SPILL!',
    '#CALC!', '#FIELD!', '#BLOCKED!', '#CONNECT!', '#BUSY!', '#UNKNOWN!')

function Read-Json([string]$Path) {
    [IO.File]::ReadAllText($Path, $Utf8) | ConvertFrom-Json
}

function Get-Prop($Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    $p = $Object.PSObject.Properties[$Name]
    if ($null -eq $p) { return $null }
    return $p.Value
}

function Has-Prop($Object, [string]$Name) {
    return ($null -ne $Object) -and ($null -ne $Object.PSObject.Properties[$Name])
}

function To-Double($Value) {
    # ConvertFrom-Json gives Int64, Double or (PowerShell 5.1) Decimal. A Decimal holds the corpus's
    # digits exactly; going through its text keeps the conversion to double correctly rounded.
    if ($Value -is [double]) { return $Value }
    if ($Value -is [decimal]) { return [double]::Parse($Value.ToString($Invariant), $Invariant) }
    return [double]$Value
}

function Set-Typed($Range, [string]$Typed, [bool]$UseFormula2) {
    try {
        if ($Typed -eq '') { [void]$Range.ClearContents() }
        elseif ($Typed.StartsWith('=')) {
            if ($UseFormula2) { $Range.Formula2 = $Typed } else { $Range.Formula = $Typed }
        }
        else { $Range.FormulaLocal = $Typed }
    }
    catch [Runtime.InteropServices.COMException] {
        # Excel would not take the entry at all: that is Excel's answer, not a failure of the oracle.
        throw [InvalidOperationException]::new("excel refused: Excel would not enter $Typed ($($_.Exception.Message))")
    }
}

# ---- Excel's dialogs, and an Excel that stops answering ----------------------------------------
# OracleKeys.Native sends -Keys' keyboard input. OracleKeys.Dialogs is a thread of its own: it
# answers the dialogs Excel raises (with -Keys), and ends an Excel that has not finished a step
# within the step's time. It runs beside the script because a COM call Excel accepts while one of
# its dialogs is up can wait for as long as the dialog stays, and a script waiting inside that call
# cannot answer the dialog: on 2026-09-27 such a wait lasted 18 minutes, and another did not end.
if (-not ('OracleKeys.Dialogs' -as [type])) {
    Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, WindowsBase
    $uiAutomation = @([AppDomain]::CurrentDomain.GetAssemblies() |
        Where-Object { @('UIAutomationClient', 'UIAutomationTypes', 'WindowsBase') -contains $_.GetName().Name } | ForEach-Object { $_.Location })
    Add-Type -ReferencedAssemblies $uiAutomation -TypeDefinition @'
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace OracleKeys {
    public static class Native {
        [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public InputUnion U; }
        [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        public static void Key(ushort vk, bool up) {
            var i = new INPUT[1]; i[0].type = 1; i[0].U.ki.wVk = vk; i[0].U.ki.dwFlags = up ? 2u : 0u;
            SendInput(1, i, Marshal.SizeOf(typeof(INPUT)));
        }
        public static void Tap(ushort vk) { Key(vk, false); Key(vk, true); }
        public static void Char(char c) {
            var i = new INPUT[2];
            i[0].type = 1; i[0].U.ki.wScan = c; i[0].U.ki.dwFlags = 4u;
            i[1].type = 1; i[1].U.ki.wScan = c; i[1].U.ki.dwFlags = 6u;
            SendInput(2, i, Marshal.SizeOf(typeof(INPUT)));
        }
    }

    public static class Dialogs {
        delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, StringBuilder name, int size);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hWnd, uint message, IntPtr w, IntPtr l);
        const uint WM_CLOSE = 0x0010;

        static readonly object Gate = new object();
        static int excelProcess;
        static IntPtr excelWindow;
        static bool answer;
        static string doing = "";
        static long deadline;
        static Thread watcher;
        // The script is typing an entry: a dialog waits until its keys are all sent.
        public static volatile bool Typing;
        public static volatile bool Answering;
        public static volatile bool Ended;
        public static readonly ConcurrentQueue<string> Events = new ConcurrentQueue<string>();

        public static void Watch(int process, IntPtr window, bool answerDialogs) {
            lock (Gate) { excelProcess = process; excelWindow = window; answer = answerDialogs; doing = ""; deadline = 0; }
            Ended = false;
            if (watcher == null) { watcher = new Thread(Loop); watcher.IsBackground = true; watcher.Start(); }
        }

        // What the script is doing, and the milliseconds it has before Excel is taken to have stopped
        // answering and is ended (0: no limit).
        public static void Step(string what, int budget) {
            lock (Gate) { doing = what; deadline = budget > 0 ? DateTime.UtcNow.AddMilliseconds(budget).Ticks : 0; }
        }

        public static string[] Drain() {
            var all = new List<string>(); string e;
            while (Events.TryDequeue(out e)) all.Add(e);
            return all.ToArray();
        }

        public static bool Present() { return Find() != IntPtr.Zero; }

        public static void End(string why) { int p; lock (Gate) { p = excelProcess; } Kill(p, why); }

        static void Record(string what) { Events.Enqueue(DateTime.Now.ToString("HH:mm:ss.fff") + " " + what); }

        // A dialog of Excel's: a visible top-level window of its process, of a dialog's class.
        static IntPtr Find() {
            int p; IntPtr main; lock (Gate) { p = excelProcess; main = excelWindow; }
            if (p == 0) return IntPtr.Zero;
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate (IntPtr h, IntPtr l) {
                uint owner; Native.GetWindowThreadProcessId(h, out owner);
                if (owner != (uint)p || h == main || !IsWindowVisible(h)) return true;
                var name = new StringBuilder(64); GetClassName(h, name, 64);
                string c = name.ToString();
                if (c == "NUIDialog" || c == "#32770" || c.StartsWith("bosa_sdm")) { found = h; return false; }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        static void Loop() {
            IntPtr seen = IntPtr.Zero; DateTime since = DateTime.MinValue;
            while (true) {
                Thread.Sleep(100);
                try {
                    int p; long due; string what; bool answering;
                    lock (Gate) { p = excelProcess; due = deadline; what = doing; answering = answer; }
                    if (p == 0 || Ended) continue;
                    if (due != 0 && DateTime.UtcNow.Ticks > due) { Kill(p, "it had not finished in time: " + what); continue; }
                    if (!answering) continue;
                    IntPtr dialog = Find();
                    if (dialog == IntPtr.Zero) { seen = IntPtr.Zero; continue; }
                    // Answered once it has been up for 300 ms (its text comes a moment after its window).
                    if (dialog != seen) { seen = dialog; since = DateTime.UtcNow; continue; }
                    if (Typing || (DateTime.UtcNow - since).TotalMilliseconds < 300) continue;
                    AnswerDialog(dialog, what, p);
                    seen = IntPtr.Zero;
                }
                catch (Exception e) { Record("the dialog watcher failed: " + e.Message); }
            }
        }

        // Its default button (Enter); failing that Escape, then WM_CLOSE; failing all three, Excel is
        // ended. "There's a problem with this formula" goes back to the edit it was raised on, which
        // Escape then cancels.
        static void AnswerDialog(IntPtr dialog, string what, int p) {
            Answering = true;
            try {
                string text = ReadText(dialog, 1500).Replace("\r", " ").Replace("\n", " ");
                string how;
                if (BringToFront(dialog, p)) { Native.Tap(0x0D); how = "Enter (its default button)"; }
                else { PostMessage(dialog, WM_CLOSE, IntPtr.Zero, IntPtr.Zero); how = "WM_CLOSE (it could not be brought in front)"; }
                if (!WaitGone(dialog, 1500)) {
                    if (BringToFront(dialog, p)) { Native.Tap(0x1B); how += ", then Escape"; }
                    if (!WaitGone(dialog, 1500)) { PostMessage(dialog, WM_CLOSE, IntPtr.Zero, IntPtr.Zero); how += ", then WM_CLOSE"; }
                    if (!WaitGone(dialog, 1500)) {
                        Record(what + ": " + text + " -> " + how + "; it did not close");
                        Kill(p, "a dialog of Excel's did not close: " + what);
                        return;
                    }
                }
                if (text.Contains("problem with this formula")) {
                    Thread.Sleep(300);
                    if (Find() == IntPtr.Zero && ForegroundProcess() == p) { Native.Tap(0x1B); how += "; Escape then cancelled the edit"; }
                }
                Record(what + ": " + text + " -> " + how);
            }
            finally { Answering = false; }
        }

        // UI Automation, on a thread of its own and for at most the budget: a dialog whose process
        // is stuck makes FindAll wait for nearly a minute.
        static string ReadText(IntPtr dialog, int budget) {
            string best = null;
            var reader = new Thread(delegate () {
                DateTime end = DateTime.UtcNow.AddMilliseconds(budget);
                while (true) {
                    try {
                        AutomationElement element = AutomationElement.FromHandle(dialog);
                        var names = new List<string>();
                        foreach (AutomationElement item in element.FindAll(TreeScope.Descendants, Condition.TrueCondition)) {
                            string n = item.Current.Name;
                            if (!string.IsNullOrEmpty(n) && !names.Contains(n)) names.Add(n);
                        }
                        best = "[" + element.Current.Name + "] " + string.Join(" | ", names.ToArray());
                        if (names.Count > 1) return;
                    }
                    catch (Exception ex) { if (best == null) best = "[unreadable: " + ex.GetType().Name + "]"; }
                    if (DateTime.UtcNow > end) return;
                    Thread.Sleep(150);
                }
            });
            reader.IsBackground = true;
            reader.Start();
            if (!reader.Join(budget + 500)) return "[its text was not read within " + (budget + 500) + " ms]" + (best == null ? "" : " " + best);
            return best ?? "[no text]";
        }

        static int ForegroundProcess() { uint p; Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out p); return (int)p; }

        static bool BringToFront(IntPtr dialog, int p) {
            if (Native.GetForegroundWindow() == dialog) return true;
            // An Alt tap lets SetForegroundWindow through from a process in the background; inside
            // Excel it would open the ribbon's KeyTips, so it is sent only when Excel is not in front.
            if (ForegroundProcess() != p) { Native.keybd_event(0x12, 0, 0, UIntPtr.Zero); Native.keybd_event(0x12, 0, 2, UIntPtr.Zero); }
            Native.SetForegroundWindow(dialog);
            Thread.Sleep(200);
            return Native.GetForegroundWindow() == dialog;
        }

        static bool WaitGone(IntPtr dialog, int budget) {
            DateTime end = DateTime.UtcNow.AddMilliseconds(budget);
            while (DateTime.UtcNow < end) { if (!IsWindowVisible(dialog)) return true; Thread.Sleep(50); }
            return !IsWindowVisible(dialog);
        }

        static void Kill(int p, string why) {
            lock (Gate) { deadline = 0; }
            if (p == 0 || Ended) return;
            Ended = true;
            try { Process.GetProcessById(p).Kill(); Record("Excel (process " + p + ") was ended: " + why); }
            catch (Exception e) { Record("Excel (process " + p + ") could not be ended (" + why + "): " + e.Message); }
        }
    }
}
'@
}

function Get-ProcessOf([IntPtr]$Window) { [uint32]$id = 0; [void][OracleKeys.Native]::GetWindowThreadProcessId($Window, [ref]$id); return $id }

# How long a step may take before Excel is taken to have stopped answering, in milliseconds: long
# enough for a whole-sheet recalculation, short enough that a stuck Excel costs seconds, not minutes.
$StepBudget = @{ setup = 30000; typing = 20000; actions = 30000; answer = 60000; close = 20000 }

function Set-Step([string]$What, [int]$Budget) { [OracleKeys.Dialogs]::Step($What, $Budget) }

# What the dialog watcher did since last asked, into the case's record.
function Save-Dialogs { foreach ($e in [OracleKeys.Dialogs]::Drain()) { $script:CaseDialogs.Add($e) } }

# Starts the Excel the run asks (visible and maximised with -Keys) and has the watcher watch it.
function Start-OracleExcel {
    $x = New-Object -ComObject Excel.Application
    $x.Visible = [bool]$Visible -or [bool]$Keys
    if ($Keys) { $x.WindowState = -4137 }   # xlMaximized
    $script:ExcelWindow = [IntPtr]$x.Hwnd
    $script:ExcelProcess = Get-ProcessOf $script:ExcelWindow
    $x.DisplayAlerts = $false
    $x.AskToUpdateLinks = $false
    [OracleKeys.Dialogs]::Watch([int]$script:ExcelProcess, $script:ExcelWindow, [bool]$Keys)
    return $x
}

# After a case: an Excel still editing is brought back to Ready with Escape; one that does not come
# back within five seconds is ended, and an ended Excel is replaced by a new one.
function Restore-OracleExcel($Excel) {
    if ($Keys -and -not [OracleKeys.Dialogs]::Ended) {
        $until = [DateTime]::UtcNow.AddSeconds(5)
        while ([DateTime]::UtcNow -lt $until) {
            if (Test-DialogUp) { Start-Sleep -Milliseconds 200; continue }
            if (Test-ExcelReady $Excel) { return $Excel }
            try { Assert-ExcelInFront; Send-Escape } catch { }
            Start-Sleep -Milliseconds 500
        }
        [OracleKeys.Dialogs]::End('it was not back to Ready five seconds after the case')
    }
    if (-not [OracleKeys.Dialogs]::Ended) { return $Excel }
    try { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($Excel) } catch { }
    Start-Sleep -Seconds 1
    return (Start-OracleExcel)
}

# ---- -Keys: typing into a visible Excel -------------------------------------------------------

# Excel, or a dialog or menu of its own, is in front; otherwise it is brought there once, with an
# Alt tap only when it is not (an Alt tap inside Excel would open the ribbon's KeyTips).
function Assert-ExcelInFront {
    if ((Get-ProcessOf ([OracleKeys.Native]::GetForegroundWindow())) -eq $script:ExcelProcess) { return }
    [OracleKeys.Native]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
    [OracleKeys.Native]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
    [void][OracleKeys.Native]::ShowWindow($script:ExcelWindow, 3)
    [void][OracleKeys.Native]::SetForegroundWindow($script:ExcelWindow)
    Start-Sleep -Milliseconds 400
    if ((Get-ProcessOf ([OracleKeys.Native]::GetForegroundWindow())) -ne $script:ExcelProcess) {
        throw [InvalidOperationException]::new('blocked: input could not be sent: another window is in front of Excel')
    }
}

function Send-Enter { [OracleKeys.Native]::Tap(0x0D) }
function Send-Escape { [OracleKeys.Native]::Tap(0x1B) }

function Send-Text([string]$Text) {
    foreach ($c in $Text.ToCharArray()) {
        if ($c -eq "`r") { continue }
        if ($c -eq "`n") {
            # A line break inside a cell is Alt+Enter.
            [OracleKeys.Native]::Key(0x12, $false); Send-Enter; [OracleKeys.Native]::Key(0x12, $true)
        }
        else { [OracleKeys.Native]::Char($c) }
    }
}

# Whether Excel answers a COM call (it refuses every call while it edits). Workbooks answers with no
# workbook open, as between two cases; ActiveSheet is then null.
function Test-ExcelReady($Excel) { try { [void]$Excel.Workbooks.Count; return $true } catch { return $false } }

# A dialog of Excel's is up, or the watcher is answering one.
function Test-DialogUp { return ([OracleKeys.Dialogs]::Answering -or [OracleKeys.Dialogs]::Present()) }

# A visible Excel refuses COM calls (RPC_E_CALL_REJECTED) while it edits or repaints. The call is
# made only while no dialog is up (a call accepted under one can wait for as long as it stays), and
# tried again for up to ten seconds.
function Invoke-WhenReady([scriptblock]$Call) {
    $readyBy = [DateTime]::UtcNow.AddSeconds(10)
    while ($true) {
        if ([OracleKeys.Dialogs]::Ended) { throw [InvalidOperationException]::new('blocked: Excel was ended') }
        if (-not (Test-DialogUp)) {
            try { return (& $Call) }
            catch { if ($_.Exception.Message -notmatch '0x80010001' -or [DateTime]::UtcNow -gt $readyBy) { throw } }
        }
        elseif ([DateTime]::UtcNow -gt $readyBy) { throw [InvalidOperationException]::new("blocked: a dialog of Excel's stayed up for ten seconds") }
        Start-Sleep -Milliseconds 100
    }
}

# Ready three times in a row, 100 ms apart, with no dialog up: COM alone may answer while a dialog
# is on its way or an edit is being closed.
function Test-ExcelSettled($Excel) {
    for ($i = 0; $i -lt 3; $i++) {
        if ((Test-DialogUp) -or -not (Test-ExcelReady $Excel)) { return $false }
        Start-Sleep -Milliseconds 100
    }
    return (-not (Test-DialogUp))
}

function Wait-ExcelSettled($Excel) {
    $settledBy = [DateTime]::UtcNow.AddSeconds(10)
    while (-not (Test-ExcelSettled $Excel)) {
        if ([OracleKeys.Dialogs]::Ended) { throw [InvalidOperationException]::new('blocked: Excel was ended') }
        if ([DateTime]::UtcNow -gt $settledBy) { throw [InvalidOperationException]::new('blocked: Excel was not back to Ready within ten seconds') }
        Start-Sleep -Milliseconds 100
    }
}

function Set-TypedByKeys($Excel, $Range, [string]$Typed) {
    if ($Typed -eq '') { Invoke-WhenReady { [void]$Range.ClearContents() }; return }
    if ($Typed.Contains("`t")) { throw [InvalidOperationException]::new('blocked: a tab cannot be typed into a cell; the Tab key moves to the next cell') }
    Set-Step "$($script:CaseId): typing $Typed" $StepBudget.typing
    # Excel's alerts are on while a user types, as they are for the user (the circular reference
    # warning, "There's a problem with this formula"), and off again for the COM calls.
    Invoke-WhenReady { $Excel.DisplayAlerts = $true }
    try { Send-TypedEntry $Excel $Range $Typed }
    finally { Invoke-WhenReady { $Excel.DisplayAlerts = $false } }
}

# The watcher answers any dialog the entry raises. What stands is read once Excel has settled; a
# "There's a problem with this formula" among the entry's dialogs means Excel refused it.
function Send-TypedEntry($Excel, $Range, [string]$Typed) {
    Assert-ExcelInFront
    Invoke-WhenReady { [void]$Range.Select() }
    Save-Dialogs
    $before = $script:CaseDialogs.Count
    [OracleKeys.Dialogs]::Typing = $true
    try { Send-Text $Typed; Send-Enter }
    finally { [OracleKeys.Dialogs]::Typing = $false }
    $kept = $false; $refusedSince = $null
    $backBy = [DateTime]::UtcNow.AddSeconds(15)
    while ([DateTime]::UtcNow -lt $backBy) {
        Start-Sleep -Milliseconds 100
        if ([OracleKeys.Dialogs]::Ended) { throw [InvalidOperationException]::new('blocked: Excel was ended') }
        if (Test-DialogUp) { $refusedSince = $null; continue }
        if (-not (Test-ExcelSettled $Excel)) {
            # Refusing COM with no dialog up: Excel kept the edit open without taking the entry (it
            # selects the part it cannot read), or it is still calculating. Escape cancels an edit:
            # after one second when a dialog of this entry has just been answered (Excel goes back
            # to the edit after "There's a problem with this formula", whose text the watcher may
            # not have read), after three otherwise. The entry is then refused.
            Save-Dialogs
            $wait = if ($script:CaseDialogs.Count -gt $before) { 1 } else { 3 }
            if ($null -eq $refusedSince) { $refusedSince = [DateTime]::UtcNow }
            elseif (([DateTime]::UtcNow - $refusedSince).TotalSeconds -ge $wait) { Send-Escape; $kept = $true; $refusedSince = $null }
            continue
        }
        Save-Dialogs
        # Excel warns of a circular reference once it has recalculated, a moment after it took the
        # entry: if the sheet has one and no dialog has come in this case yet, the warning is
        # waited for.
        $circular = $null
        try { $circular = $Excel.ActiveSheet.CircularReference } catch { $circular = $null }
        if ($null -ne $circular -and $script:CaseDialogs.Count -eq 0) {
            $warnedBy = [DateTime]::UtcNow.AddSeconds(2)
            while ([DateTime]::UtcNow -lt $warnedBy -and -not (Test-DialogUp)) { Start-Sleep -Milliseconds 100 }
            if (Test-DialogUp) { continue }
        }
        $problem = @($script:CaseDialogs | Select-Object -Skip $before | Where-Object { $_ -match 'problem with this formula' } | ForEach-Object { $_ -replace '^\S+ ', '' })
        if ($problem.Count -gt 0) { throw [InvalidOperationException]::new("excel refused: the keyboard entry of $Typed was refused: $($problem[0])") }
        if ($kept) {
            $how = if ($script:CaseDialogs.Count -gt $before) { 'after the dialog it raised (keyDialogs)' } else { 'with no dialog' }
            throw [InvalidOperationException]::new("excel refused: Excel did not commit the keyboard entry of $Typed; it stayed in Edit mode $how")
        }
        return
    }
    throw [InvalidOperationException]::new("blocked: Excel was not back to Ready within 15 seconds after $Typed was typed")
}

function Set-Cells($Sheet, $Cells, [bool]$UseFormula2, [bool]$ByKeys = $false) {
    if ($null -eq $Cells) { return }
    foreach ($p in $Cells.PSObject.Properties) {
        $typed = [string]$p.Value
        if ($ByKeys -and ($script:KeysForFormulas -or -not $typed.StartsWith('='))) { Set-TypedByKeys $Sheet.Application $Sheet.Range($p.Name) $typed }
        else { Set-Typed $Sheet.Range($p.Name) $typed $UseFormula2 }
    }
}

# Two properties are set and read through IDispatch directly, with the en-US locale id, rather
# than through PowerShell's COM binder:
#   - Range.NumberFormat. The binder passes the user's locale id, and Excel reads and writes
#     NumberFormat in that locale's codes, so under de-DE "General" is refused and "mmmm" becomes
#     minutes. VBA always passes en-US, and the corpus's codes are the invariant ones.
#   - Range.Value2. Windows PowerShell 5.1's binder keeps the type of the first value assigned to
#     it and then refuses another (a Double after a String throws InvalidCastException).
# FormulaLocal is not affected: Excel reads it under the machine's regional format whatever the
# locale id, which is what a typed entry needs.
$EnUs = [Globalization.CultureInfo]::GetCultureInfo('en-US')
function Set-ComProperty($Object, [string]$Name, $Value) {
    [void]$Object.GetType().InvokeMember($Name, [Reflection.BindingFlags]::SetProperty, $null, $Object, @(, $Value), $null, $EnUs, $null)
}
function Get-ComProperty($Object, [string]$Name) {
    return $Object.GetType().InvokeMember($Name, [Reflection.BindingFlags]::GetProperty, $null, $Object, @(), $null, $EnUs, $null)
}

function Add-Tables($Sheet, $Tables, [bool]$NeedsValues) {
    if ($null -eq $Tables) { return }
    $column = 15000
    foreach ($t in $Tables.PSObject.Properties) {
        $columns = @($t.Value.columns)
        $rows = $t.Value.rows
        if ($null -eq $rows) {
            if ($NeedsValues) { throw [InvalidOperationException]::new("blocked: table $($t.Name) is waiting for its data, which Excel has no counterpart for") }
            $rows = @(, @($columns | ForEach-Object { $null }))
        }
        for ($c = 0; $c -lt $columns.Count; $c++) { Set-ComProperty $Sheet.Cells.Item(1, $column + $c) 'Value2' ([string]$columns[$c]) }
        $r = 2
        foreach ($row in @($rows)) {
            $values = @($row)
            for ($c = 0; $c -lt $values.Count; $c++) {
                $v = $values[$c]
                $cell = $Sheet.Cells.Item($r, $column + $c)
                if ($null -eq $v) { continue }
                elseif ($v -is [bool]) { Set-ComProperty $cell 'Value2' $v }
                elseif ($v -is [string]) {
                    if ($KnownErrors -contains $v) { $cell.Formula = $v } else { Set-ComProperty $cell 'Value2' ("'" + $v) }
                }
                else { Set-ComProperty $cell 'Value2' (To-Double $v) }
            }
            $r++
        }
        $range = $Sheet.Range($Sheet.Cells.Item(1, $column), $Sheet.Cells.Item($r - 1, $column + $columns.Count - 1))
        $list = $Sheet.ListObjects.Add(1, $range, $Missing, 1)   # xlSrcRange, xlYes (has headers)
        $list.Name = $t.Name
        $column += $columns.Count + 1
    }
}

function Column-Index($Sheet, [string]$Letters) { return $Sheet.Range($Letters + '1').Column }

function Invoke-Action($Excel, $Sheet, $Action, [bool]$UseFormula2) {
    switch ([string]$Action.do) {
        'enter' { Set-Cells $Sheet $Action.cells $UseFormula2 ([bool]$Keys) }
        'insertRows' { [void]$Sheet.Range(('{0}:{1}' -f $Action.row, ($Action.row + (Get-Count $Action) - 1))).EntireRow.Insert() }
        'deleteRows' { [void]$Sheet.Range(('{0}:{1}' -f $Action.row, ($Action.row + (Get-Count $Action) - 1))).EntireRow.Delete() }
        'insertColumns' {
            $first = Column-Index $Sheet $Action.column
            [void]$Sheet.Range($Sheet.Cells.Item(1, $first), $Sheet.Cells.Item(1, $first + (Get-Count $Action) - 1)).EntireColumn.Insert()
        }
        'deleteColumns' {
            $first = Column-Index $Sheet $Action.column
            [void]$Sheet.Range($Sheet.Cells.Item(1, $first), $Sheet.Cells.Item(1, $first + (Get-Count $Action) - 1)).EntireColumn.Delete()
        }
        'fill' {
            $source = $Sheet.Range($Action.source)
            $destination = $Sheet.Range($source, $Sheet.Range($Action.target))   # the rectangle spanning both
            [void]$source.AutoFill($destination, 0)                               # xlFillDefault
        }
        'copy' { [void]$Sheet.Range($Action.source).Copy($Sheet.Range($Action.destination)) }
        'pasteText' {
            $lines = foreach ($row in @($Action.rows)) { (@($row) | ForEach-Object { [string]$_ }) -join "`t" }
            Set-Clipboard -Value (($lines -join "`r`n") + "`r`n")
            [void]$Sheet.Activate()
            [void]$Sheet.Range($Action.at).Select()
            [void]$Sheet.Paste()
        }
        'rename' { $Sheet.Name = [string]$Action.name }
        'setColumnWidth' {
            if ((Has-Prop $Action 'automatic') -and $Action.automatic) { throw [InvalidOperationException]::new("blocked: Excel records an automatic width only when an entry widens the column; nothing sets one") }
            if ((Has-Prop $Action 'sizeToFit') -and $Action.sizeToFit) { [void]$Sheet.Range([string]$Action.range).EntireColumn.AutoFit() }
            elseif ($null -eq $Action.width) { $Sheet.Range([string]$Action.range).EntireColumn.UseStandardWidth = $true }
            else { $Sheet.Range([string]$Action.range).EntireColumn.ColumnWidth = (To-Double $Action.width) }
        }
        # A range such as B:B or 2:2 is Excel's own whole column or row, so a format set on it is
        # set the way the Format Cells dialog sets it on a selected column or row.
        'format' { Set-ComProperty $Sheet.Range([string]$Action.range) 'NumberFormat' ([string]$Action.format) }
        'align' { $Sheet.Range([string]$Action.range).HorizontalAlignment = $AlignmentCodes[[string]$Action.align] }
        'style' {
            $range = $Sheet.Range([string]$Action.range)
            if (Has-Prop $Action 'format') { $range.NumberFormat = [string]$Action.format }
            if (Has-Prop $Action 'align') { $range.HorizontalAlignment = $AlignmentCodes[[string]$Action.align] }
        }
        'undo' { throw [InvalidOperationException]::new("blocked: Excel's undo does not reach changes made through COM") }
        default { throw [InvalidOperationException]::new("blocked: unknown action $($Action.do)") }
    }
}

function Get-Count($Action) { if (Has-Prop $Action 'count') { return [int]$Action.count } else { return 1 } }

function Read-CustomWidth($Sheet, [int]$Column) {
    # Whether Excel marks the column's width as the user's (customWidth), which no COM property
    # says: read from a copy of the workbook saved as .xlsx. The check sheet is the first sheet.
    $path = Join-Path ([IO.Path]::GetTempPath()) ('oracle-{0}.xlsx' -f [Guid]::NewGuid())
    try {
        $Sheet.Parent.SaveCopyAs($path)
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [IO.Compression.ZipFile]::OpenRead($path)
        try {
            $reader = New-Object IO.StreamReader($zip.GetEntry('xl/worksheets/sheet1.xml').Open())
            try { [xml]$xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
        }
        finally { $zip.Dispose() }
        $ns = New-Object Xml.XmlNamespaceManager($xml.NameTable)
        $ns.AddNamespace('s', 'http://schemas.openxmlformats.org/spreadsheetml/2006/main')
        foreach ($col in $xml.SelectNodes('//s:cols/s:col', $ns)) {
            if ([int]$col.min -le $Column -and $Column -le [int]$col.max) {
                return ($col.GetAttribute('customWidth') -eq '1' -or $col.GetAttribute('customWidth') -eq 'true')
            }
        }
        return $false
    }
    finally { Remove-Item -LiteralPath $path -ErrorAction SilentlyContinue }
}

function Read-Answer($Sheet, [string]$Check, [bool]$UseFormula2, [bool]$KeepWidth, [bool]$ReadCustom) {
    $cell = $Sheet.Range($Check)
    # The width Excel left the column at, before anything here changes it: a column still at its
    # default width that an entry widened says so here ("widens").
    $width = [double]$cell.EntireColumn.ColumnWidth
    # Whether that width is the user's, read before the width is changed below.
    $custom = if ($ReadCustom) { Read-CustomWidth $Sheet ([int]$cell.Column) } else { $null }
    # Range.Text shows #### when the column is too narrow; a case that sets "columnWidth" asks
    # what that width shows, and every other case is read in a column 100 characters wide.
    if (-not $KeepWidth) { $cell.EntireColumn.ColumnWidth = 100 }
    $v = $cell.Value2
    $answer = [ordered]@{}
    if ($null -eq $v) { $answer.value2 = $null; $answer.kind = 'blank' }
    elseif ($v -is [int]) {
        $code = $v + 2146826288 + 2000
        $answer.value2 = if ($ErrorTexts.ContainsKey($code)) { $ErrorTexts[$code] } else { "#ERROR($v)" }
        $answer.kind = 'error'
    }
    elseif ($v -is [double]) { $answer.value2 = $v; $answer.kind = 'number'; $answer.number = $v.ToString('R', $Invariant) }
    elseif ($v -is [bool]) { $answer.value2 = $v; $answer.kind = 'boolean' }
    else { $answer.value2 = [string]$v; $answer.kind = 'text' }
    $text = [string]$cell.Text
    if ($answer.kind -eq 'number' -and $text -match '^#+$') { $text = '####' }
    $answer.text = $text
    $answer.formula = if ($UseFormula2) { [string]$cell.Formula2 } else { [string]$cell.Formula }
    $answer.numberFormat = [string](Get-ComProperty $cell 'NumberFormat')
    $alignment = [int]$cell.HorizontalAlignment
    $answer.alignment = ($AlignmentCodes.GetEnumerator() | Where-Object { $_.Value -eq $alignment } | Select-Object -First 1).Key
    if ($null -eq $answer.alignment) { $answer.alignment = "xlHAlign($alignment)" }
    $answer.columnWidth = $width
    # The default width follows the workbook's default font (8.43 with Calibri, 8.09 with Aptos
    # Narrow), so it is read from the sheet rather than assumed.
    $answer.standardWidth = [double]$Sheet.StandardWidth
    $answer.widens = ($width -gt $answer.standardWidth + 0.001)
    if ($ReadCustom) { $answer.custom = $custom }
    return $answer
}

function Compare-Answer($Target, $Answer) {
    $differences = New-Object Collections.Generic.List[string]
    $wantRefused = Has-Prop $Target 'refused'
    # An expectation that the entry itself is refused is met only in the catch below, where Excel refused it.
    if (Has-Prop $Target 'refusedEntry') { $differences.Add('expected the entry refused; Excel entered it') }
    if ($wantRefused -and -not $Answer.refused) { $differences.Add("expected a refusal ($($Target.refused)); Excel did it") }
    if (-not $wantRefused -and $Answer.refused) { $differences.Add("Excel refused the action: $($Answer.refusal)") }
    if (Has-Prop $Target 'value2') {
        $e = $Target.value2
        $kind = Get-Prop $Target 'kind'
        $same = $false
        if ($null -eq $e) { $same = ($Answer.kind -eq 'blank') }
        elseif ($e -is [bool]) { $same = ($Answer.kind -eq 'boolean' -and $Answer.value2 -eq $e) }
        elseif ($e -is [string]) {
            if ($kind -ne 'text' -and $KnownErrors -contains $e) { $same = ($Answer.kind -eq 'error' -and $Answer.value2 -eq $e) }
            else { $same = ($Answer.kind -eq 'text' -and [string]::Equals($Answer.value2, $e, [StringComparison]::Ordinal)) }
        }
        elseif ($Answer.kind -eq 'number') {
            $expected = To-Double $e
            if (Has-Prop $Target 'digits') {
                $d = [int]$Target.digits
                $same = ([Math]::Round($expected, $d) -eq [Math]::Round([double]$Answer.value2, $d))
            }
            else { $same = ($expected -eq [double]$Answer.value2) }
        }
        if (-not $same) { $differences.Add(('value2: expected {0}, Excel {1} ({2})' -f ($e | ConvertTo-Json -Compress), ($Answer.value2 | ConvertTo-Json -Compress), $Answer.kind)) }
    }
    if (Has-Prop $Target 'widens') {
        # "widthOnEntry" is the engine's answer in characters; Excel's width is recorded beside it, not compared.
        if ([bool]$Target.widens -ne [bool]$Answer.widens) { $differences.Add("widens: expected $($Target.widens), Excel's column is $($Answer.columnWidth) wide") }
    }
    if (Has-Prop $Target 'width') {
        # null is a column nobody set: Excel reports the sheet's standard width for it.
        $expected = if ($null -eq $Target.width) { $Answer.standardWidth } else { To-Double $Target.width }
        if ([Math]::Abs($expected - $Answer.columnWidth) -gt 0.005) { $differences.Add("width: expected $($Target.width), Excel's column is $($Answer.columnWidth) wide") }
    }
    if (Has-Prop $Target 'custom') {
        if ([bool]$Target.custom -ne [bool]$Answer.custom) { $differences.Add("custom: expected $($Target.custom), Excel's customWidth says $($Answer.custom)") }
    }
    foreach ($name in 'text', 'formula', 'numberFormat', 'alignment') {
        if (Has-Prop $Target $name) {
            $e = [string](Get-Prop $Target $name)
            $a = [string]$Answer[$name]
            if (-not [string]::Equals($e, $a, [StringComparison]::Ordinal)) { $differences.Add("${name}: expected ""$e"", Excel ""$a""") }
        }
    }
    return , $differences
}

# ------------------------------------------------------------------------------------------------

$fixtures = (Read-Json (Join-Path $Cases 'fixtures.json')).fixtures
$files = Get-ChildItem -Path $Cases -Filter '*.json' | Where-Object { $_.BaseName -ne 'fixtures' } | Sort-Object Name
if ($Area) { $files = $files | Where-Object { $Area -contains $_.BaseName } }
$machineCulture = (Get-Culture).Name
$results = New-Object Collections.Generic.List[object]

$excel = Start-OracleExcel
# Formulas are typed only where the corpus's en-US syntax is what a user types.
$script:KeysForFormulas = [bool]$Keys -and ($machineCulture -eq 'en-US')
try {
    $probe = $excel.Workbooks.Add()
    $useFormula2 = $true
    try { $null = $probe.Worksheets.Item(1).Range('A1').Formula2 } catch { $useFormula2 = $false }
    $probe.Close($false)

    foreach ($file in $files) {
        $corpus = Read-Json $file.FullName
        foreach ($case in @($corpus.cases)) {
            if ($Id -and -not ($Id -contains $case.id)) { continue }
            $culture = if (Has-Prop $case 'culture') { [string]$case.culture } else { 'en-US' }
            $differs = Has-Prop $case 'engineDiffersByDecision'
            $target = if ($differs) { Get-Prop $case 'excelExpect' } else { $case.expect }
            $result = [ordered]@{
                id = $case.id; area = $corpus.area; source = $case.source; description = $case.description
                status = $null; reason = $null
                comparedWith = if ($differs) { if ($null -ne $target) { 'excelExpect' } else { $null } } else { 'expect' }
                expected = $target; engineExpect = $case.expect; excel = $null; differences = @()
            }
            if ($differs) { $result.engineDiffersByDecision = $case.engineDiffersByDecision }
            $script:CaseDialogs = New-Object Collections.Generic.List[string]
            $script:CaseId = $case.id
            $caseStarted = [DateTime]::UtcNow
            $ownCells = if ($null -eq $case.cells) { 0 } else { @($case.cells.PSObject.Properties).Count }

            # A case the corpus says to ask by keys is asked when -Keys types it.
            $askByKeys = $Keys -and (Has-Prop $case 'oracleSkip') -and ([string]$case.oracleSkip -match 'ask by keys')
            if ((Has-Prop $case 'oracleSkip') -and -not $askByKeys) { $result.status = 'blocked'; $result.reason = [string]$case.oracleSkip }
            elseif ($culture -ne $machineCulture) {
                $result.status = 'blocked'; $result.reason = "needs $culture; this machine's regional format is $machineCulture"
            }
            elseif ($Keys -and $ownCells -gt $MaxKeyCells) {
                $result.status = 'blocked'; $result.reason = "not typed: $ownCells cells, more than -MaxKeyCells $MaxKeyCells"
            }
            else {
                $workbook = $null
                try {
                    Set-Step "$($case.id): setting up" $StepBudget.setup
                    $workbook = $excel.Workbooks.Add()
                    # Iteration is refused while no workbook is open, so it is set once one is.
                    $excel.Iteration = $false
                    $sheet = $workbook.Worksheets.Item(1)
                    if (Has-Prop $case 'sheetName') { $sheet.Name = [string]$case.sheetName }
                    foreach ($other in @(Get-Prop $case 'otherSheets')) {
                        if ($null -eq $other) { continue }
                        $added = $workbook.Worksheets.Add($Missing, $workbook.Worksheets.Item($workbook.Worksheets.Count))
                        $added.Name = [string]$other
                    }
                    # A width set by the case is set before anything is entered, so it is the width the
                    # entry meets; a column given a width is no longer at its default, and never widens.
                    $keepWidth = Has-Prop $case 'columnWidth'
                    if ($keepWidth) { $sheet.Range([string]$case.check).EntireColumn.ColumnWidth = (To-Double $case.columnWidth) }
                    $checksValues = (Has-Prop $case.expect 'value2') -or (Has-Prop $case.expect 'text')
                    Add-Tables $sheet (Get-Prop $case 'tables') $checksValues
                    if (Has-Prop $case 'fixture') { Set-Cells $sheet (Get-Prop $fixtures ([string]$case.fixture)).cells $useFormula2 }
                    if ($Keys) { Assert-ExcelInFront; [void]$sheet.Activate() }
                    Set-Cells $sheet $case.cells $useFormula2 ([bool]$Keys)
                    Set-Step "$($case.id): formats and actions" $StepBudget.actions
                    if ($Keys) { Wait-ExcelSettled $excel }
                    $formats = Get-Prop $case 'formats'
                    if ($null -ne $formats) {
                        foreach ($p in $formats.PSObject.Properties) {
                            # A format code Excel will not take is Excel's answer, as a refused entry is.
                            try { Set-ComProperty $sheet.Range($p.Name) 'NumberFormat' ([string]$p.Value) }
                            catch { throw [InvalidOperationException]::new("excel refused: Excel would not take the format $($p.Value) ($($_.Exception.InnerException.Message))") }
                        }
                    }

                    $refused = $false; $refusal = $null
                    foreach ($action in @(Get-Prop $case 'action')) {
                        if ($null -eq $action) { continue }
                        # A COM call Excel refuses while it is still busy is not Excel refusing the action.
                        if ($Keys -and [string]$action.do -ne 'enter') { Wait-ExcelSettled $excel }
                        try { Invoke-Action $excel $sheet $action $useFormula2 }
                        catch [Runtime.InteropServices.COMException] { $refused = $true; $refusal = $_.Exception.Message; break }
                    }
                    Set-Step "$($case.id): calculating and reading the answer" $StepBudget.answer
                    if ($Keys) { Invoke-WhenReady { $excel.Calculate() } } else { $excel.Calculate() }
                    $answer = Read-Answer $sheet ([string]$case.check) $useFormula2 $keepWidth ((Has-Prop $target 'custom'))
                    $answer.refused = $refused
                    if ($refused) { $answer.refusal = $refusal }
                    $result.excel = $answer

                    if ($null -eq $target) { $result.status = 'recorded' }
                    else {
                        $differences = Compare-Answer $target $answer
                        $result.differences = @($differences)
                        $result.status = if ($differences.Count -eq 0) { 'agree' } else { 'disagree' }
                    }
                }
                catch {
                    $message = $_.Exception.Message
                    Save-Dialogs
                    $ended = @($script:CaseDialogs | Where-Object { $_ -match '^\S+ Excel \(process \d+\) (was|could not be) ended' })
                    if ([OracleKeys.Dialogs]::Ended -or $ended.Count -gt 0) {
                        $result.status = 'blocked'
                        $result.reason = "Excel stopped answering and was ended, then started again for the next case: $(($ended | Select-Object -Last 1) -replace '^\S+ ', '') (the step failed with: $message)"
                    }
                    elseif ($message.StartsWith('excel refused: ')) {
                        $result.excel = [ordered]@{ refusedEntry = $message.Substring(15) }
                        if ($null -eq $target) { $result.status = 'recorded' }
                        elseif (Has-Prop $target 'refusedEntry') { $result.status = 'agree' }
                        else { $result.status = 'disagree'; $result.differences = @($message.Substring(15)) }
                    }
                    elseif ($message.StartsWith('blocked: ')) { $result.status = 'blocked'; $result.reason = $message.Substring(9) }
                    else { $result.status = 'blocked'; $result.reason = "the oracle failed: $message (oracle.ps1 line $($_.InvocationInfo.ScriptLineNumber))" }
                }
                finally {
                    Set-Step "$($case.id): closing its workbook" $StepBudget.close
                    if ($null -ne $workbook -and -not [OracleKeys.Dialogs]::Ended) {
                        try { if ($Keys) { Invoke-WhenReady { $workbook.Close($false) } } else { $workbook.Close($false) } } catch { }
                    }
                    Set-Step '' 0
                    if ($Keys -or [OracleKeys.Dialogs]::Ended) { $excel = Restore-OracleExcel $excel }
                    Save-Dialogs
                }
            }
            if ($script:CaseDialogs.Count -gt 0) { $result.keyDialogs = @($script:CaseDialogs) }
            $results.Add([pscustomobject]$result)
            $seconds = ([DateTime]::UtcNow - $caseStarted).TotalSeconds
            Write-Host ('{0} {1,-12} {2,-9} {3,5:n1} s{4}' -f (Get-Date -Format 'HH:mm:ss'), $case.id, $result.status, $seconds, $(if ($script:CaseDialogs.Count -gt 0) { ', dialogs: ' + $script:CaseDialogs.Count } else { '' }))
        }
    }
}
finally {
    # Only the workbooks this script made, which have no file: one Excel opened by itself (an
    # AutoRecovered workbook) or the user's is never closed, since closing a recovered one deletes it.
    try { foreach ($w in @($excel.Workbooks)) { if ([string]$w.Path -eq '') { $w.Close($false) } } } catch { }
    $excelVersion = 'unknown'
    try { $excelVersion = '{0} (build {1})' -f $excel.Version, $excel.Build } catch { }
    # Application.Build gives only the third part of the version; the file names the whole of it.
    try { $excelVersion += '; EXCEL.EXE ' + (Get-Item (Join-Path $excel.Path 'EXCEL.EXE')).VersionInfo.ProductVersion } catch { }
    Set-Step '' 0
    try { $excel.Quit() } catch { }
    [OracleKeys.Dialogs]::Watch(0, [IntPtr]::Zero, $false)
    try { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($excel) } catch { }
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}

$summary = [ordered]@{}
foreach ($s in 'agree', 'disagree', 'blocked', 'recorded') { $summary[$s] = @($results | Where-Object { $_.status -eq $s }).Count }
$report = [ordered]@{
    ran = (Get-Date).ToString('s', $Invariant)
    excel = $excelVersion
    windowsRegionalFormat = $machineCulture
    formulasEnteredThrough = if ($Keys -and $script:KeysForFormulas) { 'typed (keyboard input), as every other cell of a case' } elseif ($useFormula2) { 'Range.Formula2' } else { 'Range.Formula' }
    constantsEnteredThrough = if ($Keys) { 'typed (keyboard input: SendInput, then Enter)' } else { 'Range.FormulaLocal' }
    summary = $summary
    cases = $results
}
[IO.File]::WriteAllText($Out, ($report | ConvertTo-Json -Depth 12), $Utf8)
Write-Host ''
Write-Host ('agree {0}, disagree {1}, blocked {2}, recorded {3} -> {4}' -f $summary.agree, $summary.disagree, $summary.blocked, $summary.recorded, $Out)
foreach ($r in $results | Where-Object { $_.status -eq 'disagree' }) {
    Write-Host ("DISAGREE {0}: {1}" -f $r.id, ($r.differences -join '; '))
}

if ($Update) {
    $agreed = @($results | Where-Object { $_.status -eq 'agree' })
    foreach ($group in $agreed | Group-Object area) {
        $path = Join-Path $Cases ($group.Name + '.json')
        $text = [IO.File]::ReadAllText($path, $Utf8)
        foreach ($r in $group.Group) {
            # "source" sits on the line after "id" in every case; only that value is rewritten.
            $pattern = '("id": "' + [regex]::Escape($r.id) + '",\s*"source": )"(documented|uncertain)"'
            $text = [regex]::Replace($text, $pattern, '$1"observed"')
        }
        [IO.File]::WriteAllText($path, $text, $Utf8)
    }
    Write-Host ("-Update: {0} agreeing cases now say source ""observed""; disagreeing ones are untouched." -f $agreed.Count)
}
