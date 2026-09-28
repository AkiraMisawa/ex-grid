<#
.SYNOPSIS
    Part A of docs/specs/exsheet/verify-on-windows-3.md: the machine's two-digit-year setting, and
    whether Excel's own Options have one.

        powershell -File two-digit-year.ps1

    Windows: HKCU\Control Panel\International\Calendars\TwoDigitYearMax (absent unless the user
    changed it in Region > Additional settings > Date) and the Gregorian calendar's TwoDigitYearMax
    as .NET reads it with the user's overrides. Excel: File > Options opened with real keys, every
    category selected in turn through UI Automation (all but Customize Ribbon and Quick Access
    Toolbar, which list commands, not settings), and every text on each page searched for "year",
    "two-digit", "2029", "2049" and "1930". Written to two-digit-year.json beside this script.
#>
#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel\excel-driver.ps1')
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [Windows.Automation.AutomationElement]
$Out = Join-Path $PSScriptRoot 'two-digit-year.json'

$key = 'HKCU:\Control Panel\International\Calendars\TwoDigitYearMax'
$registry = if (Test-Path $key) { (Get-ItemProperty $key | Select-Object * -ExcludeProperty PS* | ConvertTo-Json -Compress) } else { 'absent' }
$result = [ordered]@{
    ran = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    regionalFormat = (Get-Culture).Name
    registryTwoDigitYearMax = $registry
    dotnetTwoDigitYearMax = (Get-Culture).Calendar.TwoDigitYearMax
    excelOptionsSearched = @()
    excelOptionsMatches = @()
}

$xl = Connect-Excel
[void](Reset-Book $xl @{})
Show-Excel $xl
Send-Keys $xl '{ESC}' 200
# File > Options with real keys: Alt, F, T.
Send-Keys $xl '%' 600; Send-Keys $xl 'f' 900; Send-Keys $xl 't' 2500

$dialog = $null
for ($i = 0; $i -lt 20 -and $null -eq $dialog; $i++) {
    $cond = New-Object Windows.Automation.PropertyCondition ($A::NameProperty), 'Excel Options'
    $dialog = $A::RootElement.FindFirst([Windows.Automation.TreeScope]::Children, $cond)
    if ($null -eq $dialog) {
        $win = $A::FromHandle($script:Hwnd)
        $dialog = $win.FindFirst([Windows.Automation.TreeScope]::Descendants, $cond)
    }
    if ($null -eq $dialog) { Start-Sleep -Milliseconds 500 }
}
if ($null -eq $dialog) { throw 'the Excel Options dialog was not found' }

try {
    $items = $dialog.FindAll([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition ($A::ControlTypeProperty), ([Windows.Automation.ControlType]::ListItem)))
    $categories = @($items | Where-Object { $_.Current.Name -match '^(General|Formulas|Data|Proofing|Save|Language|Accessibility|Advanced|Add-ins|Trust Center)$' })
    foreach ($c in $categories) {
        $name = $c.Current.Name
        try { ($c.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern)).Select() } catch { continue }
        Start-Sleep -Milliseconds 1200
        $texts = New-Object System.Collections.Generic.List[string]
        foreach ($e in $dialog.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition)) {
            $t = [string]$e.Current.Name
            if ($t) { $texts.Add($t) }
        }
        $result.excelOptionsSearched += ('{0} ({1} texts)' -f $name, $texts.Count)
        foreach ($t in $texts) {
            if ($t -match '(?i)year|two-digit|2029|2049|1930') { $result.excelOptionsMatches += ('{0}: {1}' -f $name, $t) }
        }
        Write-Host ('{0,-22} {1} texts' -f $name, $texts.Count)
    }
}
finally {
    Send-KeysRaw '{ESC}' 800
}
[IO.File]::WriteAllText($Out, ($result | ConvertTo-Json -Depth 4), (New-Object Text.UTF8Encoding $false))
$result | ConvertTo-Json -Depth 4
