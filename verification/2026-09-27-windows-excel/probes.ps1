<#
.SYNOPSIS
    Asks Excel, through COM, what docs/specs/exsheet/verify-on-windows.md Part A items 2, 3, 4, 7,
    8 and 9 ask, and writes what it shows to probes.json beside this script. Nothing is compared
    here: results.md sets each answer beside the engine's.

    Each probe opens a fresh workbook, does one thing, reads the cells it names, and closes the
    workbook without saving. Values go in through Value2 and Formulas through Formula2 (the
    invariant syntax), so the answers do not depend on the machine's regional format; only Text
    does. NumberFormat is read and written with the en-US locale id, as oracle.ps1 explains.
#>
#Requires -Version 5.1
param([string]$Out)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $Out) { $Out = Join-Path $PSScriptRoot 'probes.json' }
$Invariant = [Globalization.CultureInfo]::InvariantCulture
$EnUs = [Globalization.CultureInfo]::GetCultureInfo('en-US')
$Utf8 = New-Object Text.UTF8Encoding $false
$ErrorTexts = @{
    2000 = '#NULL!'; 2007 = '#DIV/0!'; 2015 = '#VALUE!'; 2023 = '#REF!'; 2029 = '#NAME?'; 2036 = '#NUM!'
    2042 = '#N/A'; 2043 = '#GETTING_DATA'; 2045 = '#SPILL!'; 2047 = '#BLOCKED!'; 2050 = '#CALC!'
}

function Set-ComProperty($Object, [string]$Name, $Value) {
    [void]$Object.GetType().InvokeMember($Name, [Reflection.BindingFlags]::SetProperty, $null, $Object, @(, $Value), $null, $EnUs, $null)
}
function Get-ComProperty($Object, [string]$Name) {
    return $Object.GetType().InvokeMember($Name, [Reflection.BindingFlags]::GetProperty, $null, $Object, @(), $null, $EnUs, $null)
}

function Read-Cell($Sheet, [string]$Address) {
    $c = $Sheet.Range($Address)
    $v = $c.Value2
    $value = if ($null -eq $v) { $null }
        elseif ($v -is [double]) { $v.ToString('R', $Invariant) }
        elseif ($v -is [int]) { $code = $v + 2146826288 + 2000; if ($ErrorTexts.ContainsKey($code)) { $ErrorTexts[$code] } else { "#ERROR($v)" } }
        else { $v }
    $fill = [int]$c.Interior.ColorIndex
    return [ordered]@{
        address = $Address; value2 = $value; text = [string]$c.Text; formula2 = [string]$c.Formula2
        numberFormat = [string](Get-ComProperty $c 'NumberFormat')
        fill = if ($fill -eq -4142) { 'none' } else { 'color ' + [int]$c.Interior.Color }
    }
}

function Set-Values($Sheet, [hashtable]$Cells) {
    foreach ($a in $Cells.Keys) {
        $v = $Cells[$a]
        if ($v -is [string] -and $v.StartsWith('=')) { $Sheet.Range($a).Formula2 = $v }
        elseif ($v -is [string]) { Set-ComProperty $Sheet.Range($a) 'Value2' ("'" + $v) }
        else { Set-ComProperty $Sheet.Range($a) 'Value2' ([double]$v) }
    }
}

$probes = New-Object Collections.Generic.List[object]
function Probe([string]$Item, [string]$Name, [scriptblock]$Setup, [scriptblock]$Act, [string[]]$Read) {
    $workbook = $excel.Workbooks.Add()
    $sheet = $workbook.Worksheets.Item(1)
    $result = [ordered]@{ item = $Item; probe = $Name; before = $null; refused = $false; refusal = $null; after = $null }
    try {
        & $Setup $sheet
        $excel.Calculate()
        $result.before = @($Read | ForEach-Object { Read-Cell $sheet $_ })
        try { & $Act $sheet }
        catch { $result.refused = $true; $result.refusal = $_.Exception.Message }
        $excel.Calculate()
        $result.after = @($Read | ForEach-Object { Read-Cell $sheet $_ })
    }
    catch { $result.refusal = "the probe failed: $($_.Exception.Message)" }
    finally { $workbook.Close($false) }
    $probes.Add([pscustomobject]$result)
    Write-Host ("{0,-4} {1}" -f $Item, $Name)
}

$excel = New-Object -ComObject Excel.Application
try {
    $excel.Visible = $false
    $excel.DisplayAlerts = $false

    # ---- Item 2: formatting of inserted rows and columns (Insert with the default CopyOrigin) ----
    Probe '2' 'row 2 formatted 0.00 and filled yellow as a whole row; Rows(3).Insert()' {
        param($s)
        Set-ComProperty $s.Rows.Item(2) 'NumberFormat' '0.00'; $s.Rows.Item(2).Interior.Color = 65535
        Set-Values $s @{ A2 = 1.5 }
    } { param($s) [void]$s.Rows.Item(3).Insert() } @('A2', 'A3', 'A4', 'Z3')
    Probe '2' 'A2 alone formatted 0.00 and filled yellow; Rows(3).Insert()' {
        param($s)
        Set-ComProperty $s.Range('A2') 'NumberFormat' '0.00'; $s.Range('A2').Interior.Color = 65535
        Set-Values $s @{ A2 = 1.5 }
    } { param($s) [void]$s.Rows.Item(3).Insert() } @('A2', 'A3', 'B3')
    Probe '2' 'row 2 formatted and filled; Rows(2).Insert() (at the formatted row itself)' {
        param($s)
        Set-ComProperty $s.Rows.Item(2) 'NumberFormat' '0.00'; $s.Rows.Item(2).Interior.Color = 65535
        Set-Values $s @{ A2 = 1.5 }
    } { param($s) [void]$s.Rows.Item(2).Insert() } @('A1', 'A2', 'A3')
    Probe '2' 'column B formatted 0.00 and filled yellow as a whole column; Columns(3).Insert()' {
        param($s)
        Set-ComProperty $s.Columns.Item(2) 'NumberFormat' '0.00'; $s.Columns.Item(2).Interior.Color = 65535
        Set-Values $s @{ B1 = 1.5 }
    } { param($s) [void]$s.Columns.Item(3).Insert() } @('B1', 'C1', 'D1', 'C100')

    # ---- Item 3: inserting where a Reference would be pushed off the Sheet ----
    Probe '3' 'a value in A1048576, =A1048576 in B1; Rows(1).Insert()' {
        param($s) Set-Values $s @{ A1048576 = 7; B1 = '=A1048576' }
    } { param($s) [void]$s.Rows.Item(1).Insert() } @('B1', 'B2', 'A1048576')
    Probe '3' 'A1048576 blank, =A1048576 in B1; Rows(1).Insert()' {
        param($s) Set-Values $s @{ B1 = '=A1048576' }
    } { param($s) [void]$s.Rows.Item(1).Insert() } @('B1', 'B2')
    Probe '3' 'A1048576 holding only a number format, nothing else; Rows(1).Insert()' {
        param($s) Set-ComProperty $s.Range('A1048576') 'NumberFormat' '0.00'
    } { param($s) [void]$s.Rows.Item(1).Insert() } @('A1048576', 'A1')
    Probe '3' '=SUM(A1:A1048576) in B1; Rows(5).Insert()' {
        param($s) Set-Values $s @{ B1 = '=SUM(A1:A1048576)'; A1 = 1; A1000 = 2 }
    } { param($s) [void]$s.Rows.Item(5).Insert() } @('B1')
    Probe '3' '=SUM(A2:A1048576) in B1; Rows(1).Insert() (STRUCT-028, read where B1 went)' {
        param($s) Set-Values $s @{ B1 = '=SUM(A2:A1048576)'; A2 = 1 }
    } { param($s) [void]$s.Rows.Item(1).Insert() } @('B1', 'B2')
    Probe '3' '=SUM(A5:A1048575) in B1; Rows(1).Insert() (the range reaches the last row)' {
        param($s) Set-Values $s @{ B1 = '=SUM(A5:A1048575)'; A5 = 1 }
    } { param($s) [void]$s.Rows.Item(1).Insert() } @('B1', 'B2')

    # ---- Item 4: fill with AutoFill, xlFillDefault ----
    $fill = { param($s, $src, $dst) [void]$s.Range($src).AutoFill($s.Range($dst), 0) }
    Probe '4' 'a single number (5) filled down A1 -> A1:A4' { param($s) Set-Values $s @{ A1 = 5 } } { param($s) & $fill $s 'A1' 'A1:A4' } @('A1', 'A2', 'A3', 'A4')
    Probe '4' 'a single date (2026-09-26, m/d/yyyy) filled down A1 -> A1:A4' {
        param($s) Set-Values $s @{ A1 = 46291 }; Set-ComProperty $s.Range('A1') 'NumberFormat' 'm/d/yyyy'
    } { param($s) & $fill $s 'A1' 'A1:A4' } @('A1', 'A2', 'A3', 'A4')
    Probe '4' 'a date with a time (2026-09-26 10:00, m/d/yyyy h:mm) filled down A1 -> A1:A4' {
        param($s) Set-Values $s @{ A1 = (46291 + 10 / 24) }; Set-ComProperty $s.Range('A1') 'NumberFormat' 'm/d/yyyy h:mm'
    } { param($s) & $fill $s 'A1' 'A1:A4' } @('A1', 'A2', 'A3', 'A4')
    Probe '4' 'Item 1 filled down A1 -> A1:A4' { param($s) Set-Values $s @{ A1 = 'Item 1' } } { param($s) & $fill $s 'A1' 'A1:A4' } @('A1', 'A2', 'A3', 'A4')
    Probe '4' '1, 3 filled down A1:A2 -> A1:A6' { param($s) Set-Values $s @{ A1 = 1; A2 = 3 } } { param($s) & $fill $s 'A1:A2' 'A1:A6' } @('A1', 'A2', 'A3', 'A4', 'A5', 'A6')
    Probe '4' '1, 2, 4 filled down A1:A3 -> A1:A8' { param($s) Set-Values $s @{ A1 = 1; A2 = 2; A3 = 4 } } { param($s) & $fill $s 'A1:A3' 'A1:A8' } @('A1', 'A2', 'A3', 'A4', 'A5', 'A6', 'A7', 'A8')
    Probe '4' '0.1, 0.2, 0.4 filled down A1:A3 -> A1:A8 (the last digit of a trend)' { param($s) Set-Values $s @{ A1 = 0.1; A2 = 0.2; A3 = 0.4 } } { param($s) & $fill $s 'A1:A3' 'A1:A8' } @('A1', 'A2', 'A3', 'A4', 'A5', 'A6', 'A7', 'A8')
    Probe '4' 'numbers 1, 3 in A5:A6 filled up -> A1:A6' { param($s) Set-Values $s @{ A5 = 1; A6 = 3 } } { param($s) & $fill $s 'A5:A6' 'A1:A6' } @('A1', 'A2', 'A3', 'A4', 'A5', 'A6')
    Probe '4' 'text a, b in A5:A6 filled up -> A1:A6' { param($s) Set-Values $s @{ A5 = 'a'; A6 = 'b' } } { param($s) & $fill $s 'A5:A6' 'A1:A6' } @('A1', 'A2', 'A3', 'A4', 'A5', 'A6')
    Probe '4' 'text a, b, c in A4:A6 filled up -> A1:A6' { param($s) Set-Values $s @{ A4 = 'a'; A5 = 'b'; A6 = 'c' } } { param($s) & $fill $s 'A4:A6' 'A1:A6' } @('A1', 'A2', 'A3', 'A4', 'A5', 'A6')
    Probe '4' 'numbers 1, 3 in E1:F1 filled left -> A1:F1' { param($s) Set-Values $s @{ E1 = 1; F1 = 3 } } { param($s) & $fill $s 'E1:F1' 'A1:F1' } @('A1', 'B1', 'C1', 'D1', 'E1', 'F1')
    Probe '4' 'text a, b in E1:F1 filled left -> A1:F1' { param($s) Set-Values $s @{ E1 = 'a'; F1 = 'b' } } { param($s) & $fill $s 'E1:F1' 'A1:F1' } @('A1', 'B1', 'C1', 'D1', 'E1', 'F1')
    Probe '4' 'a source of =B1*2, x and a blank (A1:A3) filled down -> A1:A9, B1:B9 = 1..9' {
        param($s)
        Set-Values $s @{ A1 = '=B1*2'; A2 = 'x' }
        for ($i = 1; $i -le 9; $i++) { Set-ComProperty $s.Range("B$i") 'Value2' ([double]$i) }
    } { param($s) & $fill $s 'A1:A3' 'A1:A9' } @('A1', 'A2', 'A3', 'A4', 'A5', 'A6', 'A7', 'A8', 'A9')

    # ---- Item 7: spilled arrays ----
    Probe '7' '=A1:A3 in C1 through Formula2, with 1, 2, 3 in A1:A3' {
        param($s) Set-Values $s @{ A1 = 1; A2 = 2; A3 = 3 }
    } { param($s) $s.Range('C1').Formula2 = '=A1:A3' } @('C1', 'C2', 'C3', 'C4')
    Probe '7' '=A1:A3 in C2 through Range.Formula (Excel 2019 implicit intersection), with 1, 2, 3 in A1:A3' {
        param($s) Set-Values $s @{ A1 = 1; A2 = 2; A3 = 3 }
    } { param($s) $s.Range('C2').Formula = '=A1:A3' } @('C1', 'C2', 'C3')
    Probe '7' '=A1:A3 in C1 through Formula2, with x already in C3' {
        param($s) Set-Values $s @{ A1 = 1; A2 = 2; A3 = 3; C3 = 'x' }
    } { param($s) $s.Range('C1').Formula2 = '=A1:A3' } @('C1', 'C2', 'C3')

    # ---- Item 8: whitespace ----
    Probe '8' 'Range.Formula = "= A1 + B1" with 1 and 2 in A1:B1' {
        param($s) Set-Values $s @{ A1 = 1; B1 = 2 }
    } { param($s) $s.Range('C1').Formula = '= A1 + B1' } @('C1')
    Probe '8' 'Range.Formula2 = "= A1 + B1" with 1 and 2 in A1:B1' {
        param($s) Set-Values $s @{ A1 = 1; B1 = 2 }
    } { param($s) $s.Range('C1').Formula2 = '= A1 + B1' } @('C1')

    # ---- Item 9: XLOOKUP's binary search over duplicate keys, and over unsorted data ----
    $keysAsc = @{ B1 = 1; B2 = 2; B3 = 2; B4 = 2; B5 = 3; B6 = 4; C1 = 'a'; C2 = 'b'; C3 = 'c'; C4 = 'd'; C5 = 'e'; C6 = 'f' }
    foreach ($f in '=XLOOKUP(2,B1:B6,C1:C6,,0,2)', '=XLOOKUP(2,B1:B6,C1:C6,,0,1)', '=XLOOKUP(2,B1:B6,C1:C6,,0,-1)',
                   '=XLOOKUP(2.5,B1:B6,C1:C6,,-1,2)', '=XLOOKUP(1.5,B1:B6,C1:C6,,1,2)') {
        Probe '9' "B1:B6 = 1,2,2,2,3,4 (C = a..f): $f" { param($s) Set-Values $s $keysAsc } { param($s) $s.Range('E1').Formula2 = $f } @('E1')
    }
    foreach ($keys in @(@(1, 2, 2), @(2, 2, 3), @(1, 2, 2, 2, 2, 2, 3), @(2, 2, 2, 2, 2, 2, 2, 2), @(1, 1, 2, 2, 3, 3, 4, 4, 5, 5))) {
        $cells = @{}
        for ($i = 0; $i -lt $keys.Count; $i++) { $cells["B$($i + 1)"] = $keys[$i]; $cells["C$($i + 1)"] = "row$($i + 1)" }
        $n = $keys.Count
        $f = "=XLOOKUP(2,B1:B$n,C1:C$n,,0,2)"
        Probe '9' ("B1:B{0} = {1} (C = row1..): {2}" -f $n, ($keys -join ','), $f) { param($s) Set-Values $s $cells } { param($s) $s.Range('E1').Formula2 = $f } @('E1')
    }
    $keysDesc = @{ B1 = 4; B2 = 3; B3 = 2; B4 = 2; B5 = 2; B6 = 1; C1 = 'a'; C2 = 'b'; C3 = 'c'; C4 = 'd'; C5 = 'e'; C6 = 'f' }
    foreach ($f in '=XLOOKUP(2,B1:B6,C1:C6,,0,-2)', '=XLOOKUP(2.5,B1:B6,C1:C6,,-1,-2)', '=XLOOKUP(1.5,B1:B6,C1:C6,,1,-2)') {
        Probe '9' "B1:B6 = 4,3,2,2,2,1 (C = a..f): $f" { param($s) Set-Values $s $keysDesc } { param($s) $s.Range('E1').Formula2 = $f } @('E1')
    }
    $unsorted = @{ B1 = 3; B2 = 1; B3 = 4; B4 = 1; B5 = 5; B6 = 9; B7 = 2; B8 = 6; C1 = 'a'; C2 = 'b'; C3 = 'c'; C4 = 'd'; C5 = 'e'; C6 = 'f'; C7 = 'g'; C8 = 'h' }
    foreach ($f in '=XLOOKUP(4,B1:B8,C1:C8,,0,2)', '=XLOOKUP(1,B1:B8,C1:C8,,0,2)', '=XLOOKUP(9,B1:B8,C1:C8,,0,2)', '=XLOOKUP(2,B1:B8,C1:C8,,0,2)',
                   '=XLOOKUP(4,B1:B8,C1:C8,,0,-2)', '=XLOOKUP(1,B1:B8,C1:C8,,0,-2)', '=XLOOKUP(6,B1:B8,C1:C8,,0,-2)') {
        Probe '9' "B1:B8 = 3,1,4,1,5,9,2,6 unsorted (C = a..h): $f" { param($s) Set-Values $s $unsorted } { param($s) $s.Range('E1').Formula2 = $f } @('E1')
    }
}
finally {
    $version = '{0} (build {1}); EXCEL.EXE {2}' -f $excel.Version, $excel.Build, (Get-Item (Join-Path $excel.Path 'EXCEL.EXE')).VersionInfo.ProductVersion
    $excel.Quit()
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}

$report = [ordered]@{
    ran = (Get-Date).ToString('s', $Invariant); excel = $version; windowsRegionalFormat = (Get-Culture).Name
    probes = $probes
}
[IO.File]::WriteAllText($Out, ($report | ConvertTo-Json -Depth 8), $Utf8)
Write-Host "-> $Out"
