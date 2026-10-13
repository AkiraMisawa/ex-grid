param([string]$Pattern)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [Windows.Automation.AutomationElement]
$x = Get-Process excel | Select-Object -First 1
$root = $A::FromHandle($x.MainWindowHandle)
foreach ($e in $root.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition)) {
  if ($e.Current.Name -match $Pattern) { $r = $e.Current.BoundingRectangle; '{0} | {1} | {2} | {3},{4},{5},{6}' -f $e.Current.Name, $e.Current.ControlType.ProgrammaticName, $e.Current.ClassName, [int]$r.Left, [int]$r.Top, [int]$r.Right, [int]$r.Bottom }
}
