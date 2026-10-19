# The Predictive Input combo box on the Settings app's Microsoft IME > General page: its value and items, or one chosen.
#     uia-combo.ps1 read | choose <item name>
param([string]$Cmd, [string]$Arg)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]
$root = $A::RootElement
$w = $null
foreach ($x in $root.FindAll('Children', (New-Object System.Windows.Automation.PropertyCondition($A::ClassNameProperty, 'ApplicationFrameWindow')))) { if ($x.Current.Name -eq 'Settings') { $w = $x } }
$combo = $null
foreach ($e in $w.FindAll('Descendants', (New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox)))) { if ($e.Current.Name -match 'word suggestions') { $combo = $e } }
if (-not $combo) { 'no combo'; exit }
$ec = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
$ec.Expand(); Start-Sleep -Milliseconds 800
$items = @($combo.FindAll('Descendants', (New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem))))
if ($Cmd -eq 'read') {
    foreach ($i in $items) { $s = $i.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern); "item: " + $i.Current.Name + ' selected=' + $s.Current.IsSelected }
    $ec.Collapse()
} elseif ($Cmd -eq 'choose') {
    $hit = $items | ? { $_.Current.Name -eq $Arg } | Select-Object -First 1
    if (-not $hit) { 'no item ' + $Arg; $ec.Collapse(); exit }
    $hit.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 1000
    try { $ec.Collapse() } catch {}
    'ok chose ' + $Arg
}
