<#
    Part E, item 27: Excel's display language set through Excel's own Options dialog (File > Options > Language >
    Office display language > Set as Preferred), driven with UI Automation and real keys, in an Excel of the
    script's own. -Mode dump opens the dialog's Language page, lists what UI Automation sees, and cancels.
    -Mode set -Target <ja-jp|en-gb> selects that display language, Set as Preferred, OK, and answers the restart
    notice. Every name seen and every step goes to the log.
#>
param([string]$Mode = 'dump', [string]$Target = '', [string]$Log)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms
Add-Type -Namespace U -Name W -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
'@
$A = [Windows.Automation.AutomationElement]; $T = [Windows.Automation.TreeScope]; $C = [Windows.Automation.Condition]
$out = New-Object Collections.Generic.List[string]
function Say($s) { $out.Add((Get-Date -Format 'HH:mm:ss.fff') + ' ' + $s) }
function Key([byte]$vk) { [U.W]::keybd_event($vk, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 40; [U.W]::keybd_event($vk, 0, 2, [UIntPtr]::Zero); Start-Sleep -Milliseconds 120 }
function Windows-Of([int]$procId) { @($A::RootElement.FindAll($T::Children, (New-Object Windows.Automation.PropertyCondition($A::ProcessIdProperty, $procId)))) }
function Wait-Window([int]$procId, [string]$pattern, [int]$sec = 15) {
    $t0 = Get-Date
    while (((Get-Date) - $t0).TotalSeconds -lt $sec) {
        $seen = @()
        foreach ($w in (Windows-Of $procId)) { $seen += "[$($w.Current.Name) | $($w.Current.ClassName)]"; if ($w.Current.Name -match $pattern) { return $w } }
        # Also any window of another process that came up meanwhile (a consent or sign-in prompt).
        $fg = $A::FocusedElement; if ($fg) { $seen += "focus: $($fg.Current.Name) | $($fg.Current.ClassName) | pid $($fg.Current.ProcessId)"
            # The dialog is owned by Excel's window and sits under it in UI Automation's tree: walk up from the focus.
            if ($fg.Current.ProcessId -eq $procId) { $wk = [Windows.Automation.TreeWalker]::ControlViewWalker; $e = $fg
                while ($e -and -not $A::RootElement.Equals($e)) { if ($e.Current.ControlType -eq [Windows.Automation.ControlType]::Window -and $e.Current.Name -match $pattern) { return $e }; $e = $wk.GetParent($e) } } }
        Say ("waiting; windows of Excel: " + ($seen -join ' '))
        Start-Sleep -Milliseconds 1000
    }
    return $null
}
function Find-Named($root, [string]$pattern, [string]$type = '') {
    foreach ($e in $root.FindAll($T::Descendants, $C::TrueCondition)) {
        if ($e.Current.Name -match $pattern -and ($type -eq '' -or $e.Current.ControlType.ProgrammaticName -match $type)) { return $e }
    }
    return $null
}
function Act($e, [string]$what) {
    $p = $null
    if ($e.TryGetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern, [ref]$p)) { $p.Select(); Say "selected $what"; return }
    if ($e.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern, [ref]$p)) { $p.Invoke(); Say "invoked $what"; return }
    throw "no pattern to act on $what"
}
function Dump($root, [string]$title) {
    Say "--- $title"
    foreach ($e in $root.FindAll($T::Descendants, $C::TrueCondition)) {
        $n = $e.Current.Name; if ($n) { Say ("  {0} | {1} | enabled {2}" -f $n, $e.Current.ControlType.ProgrammaticName, $e.Current.IsEnabled) }
    }
}
$xl = $null; $xpid = 0
try {
    $before = @(Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
    $xl = New-Object -ComObject Excel.Application
    [uint32]$q = 0; [void][U.W]::GetWindowThreadProcessId([IntPtr][long]$xl.Hwnd, [ref]$q); $xpid = [int]$q
    if ($before -contains $xpid) { throw "an Excel already running ($xpid)" }
    $xl.Visible = $true; [void]$xl.Workbooks.Add(); $xlHwnd = $xl.Hwnd
    Say ("Excel $xpid, display language before: " + $xl.LanguageSettings.LanguageID(2))
    Start-Sleep -Milliseconds 800
    [void][U.W]::SetForegroundWindow([IntPtr][long]$xl.Hwnd); Start-Sleep -Milliseconds 500
    # File > Options: Alt, F, T (the same KeyTips in the English and the Japanese editions).
    Key 0x12; Start-Sleep -Milliseconds 400; Key 0x46; Start-Sleep -Milliseconds 1500; Key 0x54
    $dlg = Wait-Window $xpid 'Options|オプション' 30
    if (-not $dlg) { throw 'the Options dialog did not open' }
    Say "dialog: $($dlg.Current.Name) | $($dlg.Current.ClassName)"
    Start-Sleep -Milliseconds 800
    $nav = Find-Named $dlg '^(Language|言語)$' 'ListItem'
    if (-not $nav) { Dump $dlg 'no Language page found'; throw 'no Language page' }
    Act $nav 'the Language page'; Start-Sleep -Milliseconds 1200
    if ($Mode -eq 'dump') { Dump $dlg 'the Language page' }
    else {
        # The display language list's items read "Display Language 3.     Japanese [日本語]" (and so on in the Japanese edition).
        $want = if ($Target -eq 'english-up') { '^(Display Language|表示言語)\s*\d+\.\s+(English$|英語 \[English\]$)' } elseif ($Target -eq 'ja-jp') { '^(Display Language|表示言語)\s*\d+\.\s+(Japanese|日本語)' } else { '^(Display Language|表示言語)\s*\d+\.\s+(Match Microsoft Windows|Microsoft Windows)' }
        $item = Find-Named $dlg $want 'ListItem|DataItem'
        if (-not $item) { Dump $dlg 'no item'; throw "no display language item for $Target" }
        Say "item: $($item.Current.Name)"
        Act $item "the $Target item"; Start-Sleep -Milliseconds 500
        $btn = if ($Target -eq 'english-up') { Find-Named $dlg '^(Move Up|上へ移動)' 'Button' } else { Find-Named $dlg '^(Set as Preferred|優先.*設定)' 'Button' }
        if (-not $btn) { Dump $dlg 'no button'; throw 'no Set as Preferred button' }
        Say "button: $($btn.Current.Name), enabled $($btn.Current.IsEnabled)"
        if ($btn.Current.IsEnabled) { Act $btn 'Set as Preferred'; Start-Sleep -Milliseconds 1200 } else { Say 'Set as Preferred is disabled: the item is already preferred' }
        $xlw = $A::FromHandle([IntPtr][long]$xlHwnd)
        foreach ($w in $xlw.FindAll($T::Children, (New-Object Windows.Automation.PropertyCondition($A::ControlTypeProperty, [Windows.Automation.ControlType]::Window)))) { if ($w.Current.Name -ne $dlg.Current.Name) { Say "another window under Excel: $($w.Current.Name)"; Dump $w 'it' } }
        # A message box raised by Set as Preferred (if any) inside the dialog's own tree.
        $okInner = $null
        Dump $dlg 'after Set as Preferred'
        $ok = Find-Named $dlg '^OK$' 'Button'
        Act $ok 'OK'; Start-Sleep -Milliseconds 1500
        $xlw = $A::FromHandle([IntPtr][long]$xlHwnd)
        foreach ($w in @(Windows-Of $xpid) + @($xlw.FindAll($T::Children, (New-Object Windows.Automation.PropertyCondition($A::ControlTypeProperty, [Windows.Automation.ControlType]::Window))))) {
            $n = $w.Current.Name
            if ($n -and $n -notmatch '^Book') { Say "after OK, a window: $n"; Dump $w 'it'; $b = Find-Named $w '^OK$' 'Button'; if ($b) { Act $b "OK on $n" } }
            elseif ($n) { Say "after OK, window: $n" }
        }
    }
    if ($Mode -eq 'dump') { $cancel = Find-Named $dlg '^(Cancel|キャンセル)$' 'Button'; Act $cancel 'Cancel' }
} catch { Say ("error: " + $_.Exception.Message + ' (line ' + $_.InvocationInfo.ScriptLineNumber + ')') }
finally {
    Start-Sleep -Milliseconds 800
    try { foreach ($b in @($xl.Workbooks)) { $b.Close($false) } } catch { Say "close: $($_.Exception.Message)" }
    try { $xl.Quit() } catch { }
    try { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl) } catch { }
    $xl = $null; [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    if ($xpid) { for ($k = 0; $k -lt 20; $k++) { if (-not (Get-Process -Id $xpid -ErrorAction SilentlyContinue)) { break }; Start-Sleep -Milliseconds 500 }
        if (Get-Process -Id $xpid -ErrorAction SilentlyContinue) { Stop-Process -Id $xpid -Force; Say 'ended its own Excel with Stop-Process' } }
    $lr = Get-ItemProperty 'HKCU:\Software\Microsoft\Office\16.0\Common\LanguageResources'
    Say "UILanguageTag=$($lr.UILanguageTag) FollowSystemUI=$($lr.FollowSystemUI) UISnapshotLanguages=$($lr.UISnapshotLanguages)"
    [IO.File]::WriteAllLines($Log, $out, (New-Object Text.UTF8Encoding $false))
}
