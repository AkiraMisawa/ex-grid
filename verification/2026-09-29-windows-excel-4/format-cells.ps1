<#
.SYNOPSIS
    Part A of docs/specs/exsheet/verify-on-windows-4.md, "Also ask FMT-076, 077 and 078 typed": each
    case's number format typed into Excel's Format Cells > Number > Custom > Type box, then OK, with
    real keys:

        powershell -File format-cells.ps1

    The oracle sets formats through COM only (Range.NumberFormat), and the refusal of these codes was
    observed only that way. For each case: the case's cell as the corpus enters it (A1), Ctrl+1, the
    Category list (Alt+C) to its last entry (End, "Custom"), the Type box (Alt+T), the code typed over
    what is there, Enter. What Excel then shows is recorded: a message box of its own (its text through
    UI Automation, and a screenshot), or the dialog closing and the format read back through COM. No
    COM call is made while a dialog of Excel's is up. Steps go to format-cells.jsonl beside this script
    and screenshots to shots\ (A-FMT-...).
#>
#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel\excel-driver.ps1')
$script:Shots = Join-Path $PSScriptRoot 'shots'
$Log = Join-Path $PSScriptRoot 'format-cells.jsonl'
$Utf8 = New-Object Text.UTF8Encoding $false
$xl = Connect-Excel
. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel-2\case-guard.ps1')
Start-CaseGuard
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

Add-Type -TypeDefinition @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Text;
namespace FormatCells {
    public static class Native {
        delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        // Excel's visible top-level windows other than its main one, as "handle|class|title".
        public static string[] Dialogs(IntPtr main) {
            uint pid; GetWindowThreadProcessId(main, out pid);
            var found = new List<string>();
            EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p);
                if (p == pid && h != main && IsWindowVisible(h)) {
                    var c = new StringBuilder(256); GetClassName(h, c, 256); var t = new StringBuilder(512); GetWindowText(h, t, 512);
                    string cls = c.ToString();
                    if (cls == "#32770" || cls == "NUIDialog" || cls == "bosa_sdm_XL9") found.Add(h.ToInt64() + "|" + cls + "|" + t);
                }
                return true; }, IntPtr.Zero);
            return found.ToArray();
        }
    }
}
'@

function Write-Row($Row) { [IO.File]::AppendAllText($Log, (($Row | ConvertTo-Json -Compress -Depth 5) + "`n"), $Utf8) }

# Excel's visible top-level windows other than its main one: its dialogs.
function Get-ExcelDialogs {
    return @([FormatCells.Native]::Dialogs($script:Hwnd) | ForEach-Object {
        $p = $_.Split('|', 3); [pscustomobject]@{ Hwnd = [IntPtr][long]$p[0]; Class = $p[1]; Title = $p[2] } })
}

# The texts UI Automation reads in a window, on a thread with a time limit (it hung once for 50 s).
function Read-WindowTexts([IntPtr]$Hwnd, [int]$LimitMs = 3000) {
    $shell = [PowerShell]::Create()
    [void]$shell.AddScript({
        param($h)
        Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
        $root = [Windows.Automation.AutomationElement]::FromHandle($h)
        $all = $root.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition)
        $out = @()
        foreach ($e in $all) { $n = $e.Current.Name; if ($n) { $out += ('{0}: {1}' -f $e.Current.ControlType.ProgrammaticName.Replace('ControlType.', ''), $n) } }
        return ($out -join ' | ')
    }).AddArgument($Hwnd)
    $async = $shell.BeginInvoke()
    if ($async.AsyncWaitHandle.WaitOne($LimitMs)) { $r = $shell.EndInvoke($async); $shell.Dispose(); return [string]($r -join '') }
    return "(UI Automation did not answer within $LimitMs ms)"
}
function Get-Focused {
    try { $f = [Windows.Automation.AutomationElement]::FocusedElement; return ('{0} "{1}"' -f $f.Current.ControlType.ProgrammaticName.Replace('ControlType.', ''), $f.Current.Name) } catch { return "(no focus: $($_.Exception.Message))" }
}
# A window as the screen shows it. Excel's own window is taken from 80 px below its top, so its title
# bar, which shows the signed-in account, is left out.
function Shot-Window([IntPtr]$Hwnd, [string]$Name) {
    $r = New-Object FormatCells.Native+RECT
    [void][FormatCells.Native]::GetWindowRect($Hwnd, [ref]$r)
    $top = if ($Hwnd -eq $script:Hwnd) { $r.Top + 80 } else { $r.Top }
    [void](Save-Shot $null $Name ([pscustomobject]@{ Left = [Math]::Max($r.Left, 0); Top = [Math]::Max($top, 0); Right = $r.Right; Bottom = $r.Bottom }))
    return "shots/$Name.png"
}
function Wait-Dialogs([int]$Count, [int]$Ms = 5000) {
    $deadline = (Get-Date).AddMilliseconds($Ms)
    do { $d = @(Get-ExcelDialogs); if ($d.Count -ge $Count) { return $d }; Start-Sleep -Milliseconds 200 } while ((Get-Date) -lt $deadline)
    return @(Get-ExcelDialogs)
}
function Raw([string]$K, [int]$Settle = 400) { [System.Windows.Forms.SendKeys]::SendWait($K); Start-Sleep -Milliseconds $Settle }
# SendKeys reads + ^ % ~ ( ) { } [ ] itself; each goes in braces.
function Escape-Keys([string]$S) { return ([regex]::Replace($S, '[+^%~(){}\[\]]', { param($m) '{' + $m.Value + '}' })) }

function Dialog-List { return ((@(Get-ExcelDialogs) | ForEach-Object { '{0} "{1}"' -f $_.Class, $_.Title }) -join '; ') }
# A message box of Excel's over Format Cells: its text and a screenshot, then its default button.
function Answer-Or-Cancel([string]$Id, [string]$After) {
    $msg = @(Get-ExcelDialogs) | Where-Object { $_.Class -ne 'bosa_sdm_XL9' } | Select-Object -First 1
    if ($null -eq $msg) { return }
    $row = [ordered]@{ case = $Id; step = "message after $After"; title = $msg.Title; message = (Read-WindowTexts $msg.Hwnd); shot = (Shot-Window $msg.Hwnd ('A-{0}-message-after-{1}' -f $Id, ($After -replace ' ', '-'))) }
    Write-Row $row
    Write-Host ('{0}  Excel says: {1}' -f $Id, $row.message)
    Raw '~' 800
    Write-Row ([ordered]@{ case = $Id; step = "Enter on the message after $After"; dialogs = (Dialog-List); focused = (Get-Focused) })
}
# A button's screen rectangle in a window, by its name, through UI Automation.
function Find-Button([IntPtr]$Hwnd, [string]$Name) {
    $root = [Windows.Automation.AutomationElement]::FromHandle($Hwnd)
    $cond = New-Object Windows.Automation.AndCondition @(
        (New-Object Windows.Automation.PropertyCondition ([Windows.Automation.AutomationElement]::ControlTypeProperty), ([Windows.Automation.ControlType]::Button)),
        (New-Object Windows.Automation.PropertyCondition ([Windows.Automation.AutomationElement]::NameProperty), $Name))
    $b = $root.FindFirst([Windows.Automation.TreeScope]::Descendants, $cond)
    if ($null -eq $b) { return $null }
    return $b.Current.BoundingRectangle
}

$Cases = @(
    @{ Id = 'control'; Value = '3'; Code = '[Red]0' },
    @{ Id = 'FMT-076'; Value = '-3'; Code = '0;[Color10]-0' },
    @{ Id = 'FMT-077'; Value = '3'; Code = '[color3]0' },
    @{ Id = 'FMT-078'; Value = '3'; Code = '[Color3]0' }
)

foreach ($c in $Cases) {
    $id = $c.Id
    Set-CaseDue 120
    try {
        $ws = Reset-Book $xl @{ A1 = $c.Value }
        Show-Excel $xl; Send-Keys $xl '{ESC}' 200
        Send-Keys $xl '^1' 1200
        $d = @(Wait-Dialogs 1)
        $fc = $d | Where-Object { $_.Class -eq 'bosa_sdm_XL9' } | Select-Object -First 1
        Write-Row ([ordered]@{ case = $id; step = 'Ctrl+1'; dialogs = (($d | ForEach-Object { '{0} "{1}"' -f $_.Class, $_.Title }) -join '; '); focused = (Get-Focused) })
        if ($null -eq $fc) { throw 'Format Cells did not open' }
        Raw '%c' 400
        Write-Row ([ordered]@{ case = $id; step = 'Alt+C (Category)'; focused = (Get-Focused) })
        Raw '{END}' 400
        Write-Row ([ordered]@{ case = $id; step = 'End (the last category)'; focused = (Get-Focused) })
        Raw '%t' 400
        Write-Row ([ordered]@{ case = $id; step = 'Alt+T (Type)'; focused = (Get-Focused) })
        Raw '{HOME}+{END}' 300
        Raw (Escape-Keys $c.Code) 500
        $shot = Shot-Window $fc.Hwnd ('A-{0}-typed' -f $id)
        Write-Row ([ordered]@{ case = $id; step = "typed $($c.Code) into Type"; focused = (Get-Focused); texts = (Read-WindowTexts $fc.Hwnd); shot = $shot })
        # OK by Enter; then what the screen shows over Excel's window 0.3 s and 2 s after it.
        Raw '~' 300
        $row = [ordered]@{ case = $id; step = 'Enter (OK)'; dialogs03 = (Dialog-List); shot03 = (Shot-Window $script:Hwnd ('A-{0}-Enter-0.3s' -f $id)) }
        Start-Sleep -Milliseconds 1700
        $row['dialogs2'] = Dialog-List
        $row['shot2'] = Shot-Window $script:Hwnd ('A-{0}-Enter-2s' -f $id)
        $row['focused'] = Get-Focused
        Write-Row $row
        Write-Host ('{0}  after Enter: 0.3 s [{1}]  2 s [{2}]  focus {3}' -f $id, $row.dialogs03, $row.dialogs2, $row.focused)
        Answer-Or-Cancel $id 'Enter'
        # Still in Format Cells: the OK button clicked with the real mouse, as a hand would.
        $fc2 = @(Get-ExcelDialogs) | Where-Object { $_.Class -eq 'bosa_sdm_XL9' } | Select-Object -First 1
        if ($null -ne $fc2) {
            $ok = Find-Button $fc2.Hwnd 'OK'
            if ($null -ne $ok) {
                Click-At $null ([int]($ok.X + $ok.Width / 2)) ([int]($ok.Y + $ok.Height / 2))
                Start-Sleep -Milliseconds 300
                $row2 = [ordered]@{ case = $id; step = 'OK clicked'; button = ('{0},{1} {2}x{3}' -f $ok.X, $ok.Y, $ok.Width, $ok.Height); dialogs03 = (Dialog-List); shot03 = (Shot-Window $script:Hwnd ('A-{0}-OK-0.3s' -f $id)) }
                Start-Sleep -Milliseconds 1700
                $row2['dialogs2'] = Dialog-List
                $row2['shot2'] = Shot-Window $script:Hwnd ('A-{0}-OK-2s' -f $id)
                $row2['focused'] = Get-Focused
                Write-Row $row2
                Write-Host ('{0}  after OK clicked: 0.3 s [{1}]  2 s [{2}]  focus {3}' -f $id, $row2.dialogs03, $row2.dialogs2, $row2.focused)
                Answer-Or-Cancel $id 'OK clicked'
            }
            else { Write-Row ([ordered]@{ case = $id; step = 'no OK button found by UI Automation' }) }
        }
        # Whatever is still open is cancelled.
        for ($i = 0; $i -lt 3 -and @(Get-ExcelDialogs).Count -gt 0; $i++) {
            Write-Row ([ordered]@{ case = $id; step = 'Escape (Cancel)'; dialogs = (Dialog-List) })
            Raw '{ESC}' 800
        }
        if (@(Get-ExcelDialogs).Count -gt 0) { throw ('a dialog is still up: ' + ((Get-ExcelDialogs | ForEach-Object { $_.Class }) -join ', ')) }
        $cell = $ws.Range('A1')
        $state = [ordered]@{ case = $id; step = 'A1 afterwards'; method = 'COM'; numberFormat = [string](Get-ComProperty $cell 'NumberFormat'); text = [string]$cell.Text; value2 = [string]$cell.Value2; fontColor = [int]$cell.DisplayFormat.Font.Color }
        Write-Row $state
        Save-View $xl ('A-{0}-after' -f $id) 'F6'
        Write-Host ('{0}  A1 format "{1}" text "{2}"' -f $id, $state.numberFormat, $state.text)
    }
    catch {
        Write-Row ([ordered]@{ case = $id; step = 'failed'; error = $_.Exception.Message; line = $_.InvocationInfo.ScriptLineNumber })
        Write-Host "$id failed: $($_.Exception.Message) (line $($_.InvocationInfo.ScriptLineNumber))"
        for ($i = 0; $i -lt 3 -and @(Get-ExcelDialogs).Count -gt 0; $i++) { Raw '{ESC}' 500 }
    }
    Clear-CaseDue
    if ($script:Guard.Ended) {
        Write-Row ([ordered]@{ case = $id; step = 'Excel ended'; error = $script:Guard.Ended })
        $xl = Reconnect-Excel
    }
}
