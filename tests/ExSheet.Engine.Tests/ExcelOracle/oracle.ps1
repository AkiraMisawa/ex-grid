<#
.SYNOPSIS
    Asks a real Excel every case of the Excel case corpus, and records Excel's answer beside the
    one the engine's tests expect.

.DESCRIPTION
    UNTESTED UNTIL THE FIRST WINDOWS RUN. This script was written in a Linux container with no
    Excel, and has never been executed. Expect to fix it on its first run; keep it simple.

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
      - A case with "oracleSkip" is blocked with that reason.
      - "columnWidth" sets the check column's ColumnWidth (characters) before anything is entered,
        and its text is read at that width; every other case is read at width 100. The column's
        width after the case's entries is recorded ("columnWidth") and compared with "widens"
        (wider than the default 8.43 or not). "widthOnEntry" is the engine's answer and is not
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
    [string]$Cases = (Join-Path $PSScriptRoot '..\ExcelCases'),
    [string]$Out = (Join-Path $PSScriptRoot ('results-{0}.json' -f (Get-Date -Format 'yyyy-MM-dd'))),
    [string[]]$Area,
    [string[]]$Id,
    [switch]$Update,
    [switch]$Visible
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Invariant = [Globalization.CultureInfo]::InvariantCulture
$Utf8 = New-Object Text.UTF8Encoding $false
$Missing = [Type]::Missing
$DefaultColumnWidth = 8.43

# Excel's error codes as Range.Value2 returns them (an Int32), by their CVErr number.
$ErrorTexts = @{
    2000 = '#NULL!'; 2007 = '#DIV/0!'; 2015 = '#VALUE!'; 2023 = '#REF!'; 2029 = '#NAME?'; 2036 = '#NUM!'
    2042 = '#N/A'; 2043 = '#GETTING_DATA'; 2045 = '#SPILL!'; 2046 = '#CONNECT!'; 2047 = '#BLOCKED!'
    2048 = '#UNKNOWN!'; 2049 = '#FIELD!'; 2050 = '#CALC!'
}
$KnownErrors = @('#NULL!', '#DIV/0!', '#VALUE!', '#REF!', '#NAME?', '#NUM!', '#N/A', '#GETTING_DATA', '#CIRC!', '#SPILL!')

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

function Set-Cells($Sheet, $Cells, [bool]$UseFormula2) {
    if ($null -eq $Cells) { return }
    foreach ($p in $Cells.PSObject.Properties) { Set-Typed $Sheet.Range($p.Name) ([string]$p.Value) $UseFormula2 }
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
        for ($c = 0; $c -lt $columns.Count; $c++) { $Sheet.Cells.Item(1, $column + $c).Value2 = [string]$columns[$c] }
        $r = 2
        foreach ($row in @($rows)) {
            $values = @($row)
            for ($c = 0; $c -lt $values.Count; $c++) {
                $v = $values[$c]
                $cell = $Sheet.Cells.Item($r, $column + $c)
                if ($null -eq $v) { continue }
                elseif ($v -is [bool]) { $cell.Value2 = $v }
                elseif ($v -is [string]) {
                    if ($KnownErrors -contains $v) { $cell.Formula = $v } else { $cell.Value2 = "'" + $v }
                }
                else { $cell.Value2 = (To-Double $v) }
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
        'enter' { Set-Cells $Sheet $Action.cells $UseFormula2 }
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
        'undo' { throw [InvalidOperationException]::new("blocked: Excel's undo does not reach changes made through COM") }
        default { throw [InvalidOperationException]::new("blocked: unknown action $($Action.do)") }
    }
}

function Get-Count($Action) { if (Has-Prop $Action 'count') { return [int]$Action.count } else { return 1 } }

function Read-Answer($Sheet, [string]$Check, [bool]$UseFormula2, [bool]$KeepWidth) {
    $cell = $Sheet.Range($Check)
    # The width Excel left the column at, before anything here changes it: a column still at its
    # default width that an entry widened says so here ("widens").
    $width = [double]$cell.EntireColumn.ColumnWidth
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
    $answer.numberFormat = [string]$cell.NumberFormat
    $answer.columnWidth = $width
    $answer.widens = ($width -gt $DefaultColumnWidth + 0.001)
    return $answer
}

function Compare-Answer($Target, $Answer) {
    $differences = New-Object Collections.Generic.List[string]
    $wantRefused = Has-Prop $Target 'refused'
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
    foreach ($name in 'text', 'formula', 'numberFormat') {
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

$excel = New-Object -ComObject Excel.Application
try {
    $excel.Visible = [bool]$Visible
    $excel.DisplayAlerts = $false
    $excel.AskToUpdateLinks = $false
    $excel.Iteration = $false
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

            if (Has-Prop $case 'oracleSkip') { $result.status = 'blocked'; $result.reason = [string]$case.oracleSkip }
            elseif ($culture -ne $machineCulture) {
                $result.status = 'blocked'; $result.reason = "needs $culture; this machine's regional format is $machineCulture"
            }
            else {
                $workbook = $null
                try {
                    $workbook = $excel.Workbooks.Add()
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
                    Set-Cells $sheet $case.cells $useFormula2
                    $formats = Get-Prop $case 'formats'
                    if ($null -ne $formats) { foreach ($p in $formats.PSObject.Properties) { $sheet.Range($p.Name).NumberFormat = [string]$p.Value } }

                    $refused = $false; $refusal = $null
                    foreach ($action in @(Get-Prop $case 'action')) {
                        if ($null -eq $action) { continue }
                        try { Invoke-Action $excel $sheet $action $useFormula2 }
                        catch [Runtime.InteropServices.COMException] { $refused = $true; $refusal = $_.Exception.Message; break }
                    }
                    $excel.Calculate()
                    $answer = Read-Answer $sheet ([string]$case.check) $useFormula2 $keepWidth
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
                    if ($message.StartsWith('excel refused: ')) {
                        $result.excel = [ordered]@{ refusedEntry = $message.Substring(15) }
                        if ($null -eq $target) { $result.status = 'recorded' }
                        else { $result.status = 'disagree'; $result.differences = @($message.Substring(15)) }
                    }
                    elseif ($message.StartsWith('blocked: ')) { $result.status = 'blocked'; $result.reason = $message.Substring(9) }
                    else { $result.status = 'blocked'; $result.reason = "the oracle failed: $message" }
                }
                finally {
                    if ($null -ne $workbook) { try { $workbook.Close($false) } catch { } }
                }
            }
            $results.Add([pscustomobject]$result)
            Write-Host ('{0,-12} {1}' -f $case.id, $result.status)
        }
    }
}
finally {
    try { foreach ($w in @($excel.Workbooks)) { $w.Close($false) } } catch { }
    $excelVersion = 'unknown'
    try { $excelVersion = '{0} (build {1})' -f $excel.Version, $excel.Build } catch { }
    $excel.Quit()
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}

$summary = [ordered]@{}
foreach ($s in 'agree', 'disagree', 'blocked', 'recorded') { $summary[$s] = @($results | Where-Object { $_.status -eq $s }).Count }
$report = [ordered]@{
    ran = (Get-Date).ToString('s', $Invariant)
    excel = $excelVersion
    windowsRegionalFormat = $machineCulture
    formulasEnteredThrough = if ($useFormula2) { 'Range.Formula2' } else { 'Range.Formula' }
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
