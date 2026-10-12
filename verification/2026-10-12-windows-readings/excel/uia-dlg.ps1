param([string]$Title = 'Format Cells', [string]$Pattern = '.')
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [Windows.Automation.AutomationElement]
$x = Get-Process excel | Select-Object -First 1
$wins = $A::RootElement.FindAll([Windows.Automation.TreeScope]::Children, (New-Object Windows.Automation.PropertyCondition($A::ProcessIdProperty, $x.Id)))
foreach ($w in $wins) {
  foreach ($d in @($w) + @($w.FindAll([Windows.Automation.TreeScope]::Children, (New-Object Windows.Automation.PropertyCondition($A::ControlTypeProperty, [Windows.Automation.ControlType]::Window))))) {
    if ($d.Current.Name -ne $Title) { continue }
    foreach ($e in $d.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition)) {
      if ($e.Current.Name -match $Pattern) { $r = $e.Current.BoundingRectangle; $v = $null; $val = ''; $s = $null; $sel = ''
        if ($e.TryGetCurrentPattern([Windows.Automation.ValuePattern]::Pattern, [ref]$v)) { $val = ' value=' + $v.Current.Value }
        if ($e.TryGetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern, [ref]$s)) { $sel = ' selected=' + $s.Current.IsSelected }
        '{0} | {1} | {2},{3},{4},{5}{6}{7}' -f $e.Current.Name, $e.Current.ControlType.ProgrammaticName, [int]$r.Left, [int]$r.Top, [int]$r.Right, [int]$r.Bottom, $val, $sel }
    }
  }
}
