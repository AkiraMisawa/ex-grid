<#
.SYNOPSIS
Records Excel 365's function behaviour through invariant Formula2, without changing the engine.
.DESCRIPTION
Each case has its own workbook, explicit typed fixture Values, and no saved files. Numeric text
is written to a Text-formatted cell; formulas use Formula2 so arrays are not implicitly intersected.
Random samples are observations, not assertions of a seed, distribution, or deterministic result.
This script owns and quits its Excel instance; it never attaches to a user's open workbook.
#>
#Requires -Version 5.1
[CmdletBinding()]
param([string]$Cases, [string]$Out, [string[]]$Function)
if (-not $Cases) { $Cases=Join-Path $PSScriptRoot 'cases.json' }
if (-not $Out) { $Out=Join-Path $PSScriptRoot 'results.json' }
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$utf8=New-Object Text.UTF8Encoding $false
$inv=[Globalization.CultureInfo]::InvariantCulture
$inputData=[IO.File]::ReadAllText($Cases,$utf8) | ConvertFrom-Json
function Prop($o,$name,$default) {
    $p=$o.PSObject.Properties[$name]
    if ($null -eq $p) {return $default}; return $p.Value
}
# As in ExcelOracle/oracle.ps1, bypass PowerShell 5.1's cached COM assignment type.
$enUs=[Globalization.CultureInfo]::GetCultureInfo('en-US')
function Set-ComProperty($o,[string]$name,$value) {
    [void]$o.GetType().InvokeMember($name,[Reflection.BindingFlags]::SetProperty,$null,$o,@(,$value),$null,$enUs,$null)
}
function Read-Cell($sheet,$address) {
    $r=$sheet.Range($address)
    try {
        $v=$r.Value2
        $kind=if($null -eq $v){'blank'}elseif($v -is [bool]){'boolean'}elseif($v -is [string]){'text'}elseif($v -is [int]){'error'}else{'number'}
        $o=[ordered]@{address=$address;kind=$kind;value2=$v;text=[string]$r.Text;formula=[string]$r.Formula2;numberFormat=[string]$r.NumberFormat}
        if($kind -eq 'number') {
            $o.roundTrip=([double]$v).ToString('R',$inv)
            $o.bits=([BitConverter]::DoubleToInt64Bits([double]$v)).ToString('X16')
        }
        return [pscustomobject]$o
    } finally {[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($r)}
}
$excel=New-Object -ComObject Excel.Application
$records=New-Object Collections.Generic.List[object]
$failures=0
$templates=[Collections.Hashtable]::new([StringComparer]::Ordinal)
try {
    $excel.Visible=$false; $excel.DisplayAlerts=$false
    $excel.EnableEvents=$false
    $version=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $excel.Path 'EXCEL.EXE'))
    $metadata=[ordered]@{startedUtc=[DateTime]::UtcNow.ToString('o');baseline=$inputData.baseline;
        excelVersion=$excel.Version;excelBuild=$excel.Build;fileVersion=$version.FileVersion;
        operatingSystem=$excel.OperatingSystem;culture=(Get-Culture).Name;
        decimalSeparator=$excel.DecimalSeparator;thousandsSeparator=$excel.ThousandsSeparator;
        powerShell=$PSVersionTable.PSVersion.ToString();inputMode='COM Formula2; explicit Value2 fixtures';
        caseFileSha256=(Get-FileHash $Cases -Algorithm SHA256).Hash;
        scriptSha256=(Get-FileHash $PSCommandPath -Algorithm SHA256).Hash}
    foreach($c in $inputData.cases) {
        if($Function -and $Function -notcontains $c.function){continue}
        $wb=$null; $sheet=$null
        $record=[ordered]@{id=$c.id;function=$c.function;question=$c.question;formula=$c.formula;status='observed';states=@()}
        try {
            # A validated fixture is copied into a fresh workbook. The template has no check
            # Formula and is never changed; each case still has independent cells and Values.
            $fixtureKey=($c.cells | ConvertTo-Json -Depth 6 -Compress)
            $hasTemplate=$templates.ContainsKey($fixtureKey)
            if($hasTemplate) {
                $templates[$fixtureKey].Worksheets.Item(1).Copy()
                $wb=$excel.ActiveWorkbook
            } else {$wb=$excel.Workbooks.Add()}
            $sheet=$wb.Worksheets.Item(1)
            $wb.Date1904=$false; $excel.Iteration=$false; $excel.Calculation=-4135
            $sheet.Columns.Item('Z').ColumnWidth=80
            $properties=@($c.cells.PSObject.Properties)
            $fixture=New-Object Collections.Generic.List[object]
            if($properties.Count -gt 0) {
                $maxRow=1; $maxColumn=1
                foreach($p in $properties) {
                    if($p.Name -notmatch '^([A-Z]+)([0-9]+)$'){throw 'Fixture addresses must be A1 references'}
                    $row=[int]$Matches[2]; $col=0
                    foreach($ch in $Matches[1].ToCharArray()){$col=$col*26+[int]$ch-64}
                    $maxRow=[Math]::Max($maxRow,$row); $maxColumn=[Math]::Max($maxColumn,$col)
                }
                $rectangle=$sheet.Range($sheet.Cells.Item(1,1),$sheet.Cells.Item($maxRow,$maxColumn))
                try {
                    if(-not $hasTemplate) {
                    foreach($p in $properties) {
                        $r=$sheet.Range($p.Name)
                        try {
                            $v=$p.Value
                            if($null -eq $v){[void]$r.ClearContents()}
                            elseif($v -is [string] -and $v.StartsWith('=')){$r.Formula2=$v}
                            elseif($v -is [string]){Set-ComProperty $r 'NumberFormat' '@';Set-ComProperty $r 'Value2' $v}
                            elseif($v -is [bool]){Set-ComProperty $r 'Value2' $v}
                            else{Set-ComProperty $r 'Value2' ([double]$v)}
                        } finally {[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($r)}
                    }
                    }
                    $excel.Calculate()
                    $readback=$rectangle.Value2
                    foreach($p in $properties) {
                        [void]($p.Name -match '^([A-Z]+)([0-9]+)$');$row=[int]$Matches[2];$col=0
                        foreach($ch in $Matches[1].ToCharArray()){$col=$col*26+[int]$ch-64}
                        $v=if($readback -is [Array]){$readback.GetValue($row,$col)}else{$readback}
                        $kind=if($null -eq $v){'blank'}elseif($v -is [bool]){'boolean'}elseif($v -is [string]){'text'}elseif($v -is [int]){'error'}else{'number'}
                        $fixture.Add([pscustomobject]@{address=$p.Name;kind=$kind;value2=$v})
                    }
                } finally {[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($rectangle)}
            }
            $record.fixtureReadback=$fixture.ToArray()
            if(-not $hasTemplate) {
                $sheet.Copy()
                $templates[$fixtureKey]=$excel.ActiveWorkbook
            }
            $r=$sheet.Range('Z1')
            try {
                try {$r.Formula2=$c.formula}
                catch [Runtime.InteropServices.COMException] {
                    $record.status='refused-entry';$record.refusal=$_.Exception.Message
                    $record.hresult=$_.Exception.HResult;continue
                }
            } finally {[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($r)}
            $excel.Calculate()
            $samples=[int](Prop $c 'samples' 1)
            $states=New-Object Collections.Generic.List[object]
            for($i=0;$i -lt $samples;$i++) {
                if($i -gt 0){$excel.Calculate()}
                $states.Add([pscustomobject]@{step="calculation-$i";cells=@((Read-Cell $sheet 'Z1'),(Read-Cell $sheet 'Z2'),(Read-Cell $sheet 'Z3'))})
            }
            foreach($edit in @(Prop $c 'edits' @())) {
                Set-ComProperty ($sheet.Range($edit.address)) 'Value2' ([double]$edit.value)
                $excel.Calculate()
                $states.Add([pscustomobject]@{step=('edit-'+$edit.address);cells=@((Read-Cell $sheet 'Z1'))})
            }
            if([bool](Prop $c 'lifecycle' $false)) {
                $sheet.Range('AA1').Formula2='=Z1*2'
                $excel.Calculation=-4105; $excel.Calculate()
                $states.Add([pscustomobject]@{step='automatic-baseline';cells=@((Read-Cell $sheet 'Z1'),(Read-Cell $sheet 'AA1'))})
                $sheet.Range('B50').Value2=1.0
                $states.Add([pscustomobject]@{step='automatic-unrelated-edit';cells=@((Read-Cell $sheet 'Z1'),(Read-Cell $sheet 'AA1'))})
                $excel.Calculation=-4135
                $states.Add([pscustomobject]@{step='manual-baseline';cells=@((Read-Cell $sheet 'Z1'),(Read-Cell $sheet 'AA1'))})
                $sheet.Range('B50').Value2=2.0
                $states.Add([pscustomobject]@{step='manual-unrelated-edit';cells=@((Read-Cell $sheet 'Z1'),(Read-Cell $sheet 'AA1'))})
                $excel.Calculate()
                $states.Add([pscustomobject]@{step='manual-explicit-calculation';cells=@((Read-Cell $sheet 'Z1'),(Read-Cell $sheet 'AA1'))})
            }
            $record.states=@($states.ToArray())
        } catch {
            $record.status='failed';$record.failure=$_.Exception.ToString();$record.failureAt=$_.ScriptStackTrace;$failures++;break
        } finally {
            if($null -ne $sheet){[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($sheet)}
            if($null -ne $wb){$wb.Close($false);[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($wb)}
            $records.Add([pscustomobject]$record)
            if($records.Count % 25 -eq 0){Write-Host "$($records.Count) cases recorded"}
        }
    }
    $metadata.fixtureTemplates=$templates.Count
    $metadata.finishedUtc=[DateTime]::UtcNow.ToString('o')
    [IO.File]::WriteAllText($Out,([ordered]@{metadata=$metadata;cases=$records.ToArray()} | ConvertTo-Json -Depth 16),$utf8)
    Write-Host "$($records.Count) cases; $failures failures -> $Out"
} finally {foreach($template in $templates.Values){$template.Close($false);[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($template)};$excel.Quit();[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($excel)}
if($failures -gt 0){throw "$failures probe failures; inspect the recorded exceptions"}
