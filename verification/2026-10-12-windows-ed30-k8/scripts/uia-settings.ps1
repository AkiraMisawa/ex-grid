# The Settings app's Microsoft IME pages, read and pressed through UI Automation.
#     uia-settings.ps1 open <ms-settings uri> | list [filter] | toggle <name regex> | invoke <name regex> | close
param([string]$Cmd, [string]$Arg)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$root = [System.Windows.Automation.AutomationElement]::RootElement
function SettingsWindow {
    $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ClassNameProperty, 'ApplicationFrameWindow')
    foreach ($w in $root.FindAll([System.Windows.Automation.TreeScope]::Children, $c)) { if ($w.Current.Name -match '^Settings$') { return $w } }
    return $null
}
function All($w) { $w.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) }
switch ($Cmd) {
    'open' { Start-Process $Arg; Start-Sleep -Seconds 4; $w = SettingsWindow; if ($w) { "ok open: " + $w.Current.Name } else { 'no Settings window' } }
    'list' {
        $w = SettingsWindow; if (-not $w) { 'no Settings window'; break }
        foreach ($e in (All $w)) {
            $n = $e.Current.Name; $t = $e.Current.ControlType.ProgrammaticName -replace 'ControlType\.', ''
            if (-not $n) { continue }
            if ($Arg -and $n -notmatch $Arg) { continue }
            $state = ''
            try { $tp = $e.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern); $state = ' toggle=' + $tp.Current.ToggleState } catch {}
            "$t | $n$state"
        }
    }
    'toggle' {
        $w = SettingsWindow; $hit = $null
        foreach ($e in (All $w)) { if ($e.Current.Name -match $Arg) { try { $tp = $e.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern); $hit = $e; break } catch {} } }
        if (-not $hit) { 'no toggle matches'; break }
        $tp = $hit.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
        $before = $tp.Current.ToggleState; $tp.Toggle(); Start-Sleep -Milliseconds 800
        "ok toggle '" + $hit.Current.Name + "': " + $before + ' -> ' + $tp.Current.ToggleState
    }
    'invoke' {
        $w = SettingsWindow; $hit = $null
        foreach ($e in (All $w)) { if ($e.Current.Name -match $Arg) { try { $ip = $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern); $hit = $e; break } catch {} } }
        if (-not $hit) { 'no invokable matches'; break }
        $hit.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Seconds 2
        "ok invoke '" + $hit.Current.Name + "'"
    }
    'close' { $w = SettingsWindow; if ($w) { $w.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close(); 'ok closed' } else { 'no Settings window' } }
}
