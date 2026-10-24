<#
.SYNOPSIS
    exsheet-workbook ticket 12, the questions COM can ask: an Excel of this script's own, a fresh
    workbook per case, the case's Sheet operations through the object model, and every Formula's text
    and value read back. One JSON object per case on a line of <out>.

        powershell -File ask-com.ps1 <out.jsonl> [<group> ...]

    Every case's workbook holds, in this order, Out, Sheet1, Sheet2, Sheet3, Sheet4, Sheet5, with B2 on
    SheetN holding 10^(N-1) (1, 10, 100, 1000, 10000), C3 holding 2*10^(N-1), so a sum names the Sheets
    it read. Out!A1 holds =SUM(Sheet2:Sheet4!B2) (1110) unless a case says otherwise.

    Groups: functions, forms, span, rows, copy, deleted, names.
#>
param([string]$Out, [string[]]$Groups)
$ErrorActionPreference = 'Stop'
if (-not $Groups) { $Groups = 'functions', 'forms', 'span', 'rows', 'copy', 'deleted', 'names' }
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

$xl = New-Object -ComObject Excel.Application
$xl.Visible = $false
$xl.DisplayAlerts = $false
$xl.AskToUpdateLinks = $false
$results = New-Object System.Collections.Generic.List[object]

function Book([int]$n = 5, [switch]$NoOut) {
    $b = $xl.Workbooks.Add()
    while ($b.Worksheets.Count -lt $n + 1) { [void]$b.Worksheets.Add([Type]::Missing, $b.Worksheets.Item($b.Worksheets.Count)) }
    # Named in two passes, so that no name is taken while the next is given.
    for ($i = 1; $i -le $b.Worksheets.Count; $i++) { $b.Worksheets.Item($i).Name = "tmp$i" }
    $b.Worksheets.Item(1).Name = 'Out'
    for ($i = 1; $i -le $n; $i++) {
        $s = $b.Worksheets.Item($i + 1); $s.Name = "Sheet$i"
        $s.Range('B2').Value2 = [double][math]::Pow(10, $i - 1)
        $s.Range('C3').Value2 = [double](2 * [math]::Pow(10, $i - 1))
    }
    if ($NoOut) { $b.Worksheets.Item('Out').Delete() }
    return $b
}
function Order($b) { (1..$b.Worksheets.Count | ForEach-Object { $b.Worksheets.Item($_).Name }) -join ',' }
function Read($b, [string[]]$cells) {
    $xl.Calculate()
    $o = [ordered]@{}
    foreach ($c in $cells) {
        $p = $c.Split('!'); $r = $b.Worksheets.Item($p[0]).Range($p[1])
        $o[$c] = [ordered]@{ formula = [string]$r.Formula2; text = [string]$r.Text; value = $r.Value2; format = [string]$r.NumberFormat }
    }
    return $o
}
function Case([string]$group, [string]$id, [string]$what, [scriptblock]$body) {
    $b = $null; $rec = [ordered]@{ group = $group; id = $id; what = $what }
    try { $b = Book; $r = & $body $b; foreach ($k in $r.Keys) { $rec[$k] = $r[$k] } }
    catch { $rec['error'] = $_.Exception.Message }
    finally { if ($b) { try { $b.Close($false) } catch { } } }
    $results.Add($rec)
    ($rec | ConvertTo-Json -Compress -Depth 6) | Add-Content -Encoding UTF8 $Out
    Write-Host "$id $(if ($rec.error) { 'ERR ' + $rec.error } else { 'ok' })"
}
# Enters a Formula through Formula2; Excel refusing it is a COM error, recorded as "refused".
function Enter($b, [string]$cell, [string]$formula) {
    $p = $cell.Split('!'); $r = $b.Worksheets.Item($p[0]).Range($p[1])
    try { $r.Formula2 = $formula; return $null } catch { return $_.Exception.Message }
}
function Sh($b, $name) { $b.Worksheets.Item($name) }

Set-Content -Encoding UTF8 $Out -Value $null
$meta = [ordered]@{ group = 'meta'; version = $xl.Version; build = $xl.Build; culture = (Get-Culture).Name; started = (Get-Date).ToString('o') }
($meta | ConvertTo-Json -Compress) | Add-Content -Encoding UTF8 $Out

# ---------------------------------------------------------------------------------------------------
if ($Groups -contains 'functions') {
    # Every function ExSheet declares, with Sheet2:Sheet4!B2 as each of its first arguments in turn
    # (up to three, and the second of one that takes a list), the others 1.
    foreach ($line in Get-Content (Join-Path $here 'functions.txt')) {
        $f = $line.Split('|'); $name = $f[0]; $min = [int]$f[1]; $max = if ($f[2] -eq 'Open') { 255 } else { [int]$f[2] }
        if ($name -eq 'LET') { $forms = @('=LET(x,Sheet2:Sheet4!B2,SUM(x))', '=LET(x,Sheet2:Sheet4!B2,x)') }
        elseif ($max -eq 0) { continue }
        else {
            $n = [math]::Max($min, 1); if ($f[2] -eq 'Open' -and $n -lt 2) { $n = 2 }; $n = [math]::Min($n, 3)
            $forms = foreach ($pos in 0..($n - 1)) {
                $parts = foreach ($i in 0..([math]::Max($min, $pos + 1) - 1)) { if ($i -eq $pos) { 'Sheet2:Sheet4!B2' } else { '1' } }
                "=$name(" + ($parts -join ',') + ')'
            }
        }
        foreach ($form in $forms) {
            Case 'functions' "$name/$form" $form {
                param($b)
                $refused = Enter $b 'Out!A1' $form
                $o = [ordered]@{ refused = $refused }
                if (-not $refused) { $o.read = Read $b 'Out!A1' }
                $o
            }
        }
    }
}

# ---------------------------------------------------------------------------------------------------
if ($Groups -contains 'forms') {
    $forms = @(
        '=Sheet2:Sheet4!B2', '=Sheet2:Sheet4!B2+1', '=-Sheet2:Sheet4!B2', '=(Sheet2:Sheet4!B2)',
        '=SUM((Sheet2:Sheet4!B2))', '=SUM(Sheet2:Sheet4!B2+0)', '=SUM(Sheet2:Sheet4!B2*1)', '=SUM(-Sheet2:Sheet4!B2)',
        '=SUM(Sheet2:Sheet4!B2:C3)', '=SUM(Sheet2:Sheet4!B:B)', '=SUM(Sheet2:Sheet4!2:2)', '=SUM(Sheet2:Sheet4!$B$2)',
        '=SUM(Sheet2:Sheet4!B2,Sheet1!B2)', '=SUM(Sheet2:Sheet4!B2,Sheet2:Sheet4!C3)', '=SUM(Sheet2:Sheet4!B2:C3 Sheet2:Sheet4!B2)',
        '=SUM(Sheet2:Sheet4!B2,1)', '=SUM(Sheet2:Sheet2!B2)', '=SUM(Sheet4:Sheet2!B2)', '=SUM(sheet2:SHEET4!b2)',
        "=SUM('Sheet2:Sheet4'!B2)", '=SUM(Out:Sheet5!B2)', '=SUM(Sheet1:Sheet5!B2:C3)', '=SUM(Sheet2:Sheet4!B2)+SUM(Sheet2:Sheet4!B2)',
        '=IF(TRUE,SUM(Sheet2:Sheet4!B2))', '=IF(TRUE,Sheet2:Sheet4!B2)', '=SUM(IF(TRUE,Sheet2:Sheet4!B2))',
        '=SUM(Sheet2:Sheet4!B2:Sheet2:Sheet4!C3)', '=SUM(Sheet2:Sheet4!A1:INDEX(Sheet2!A:A,2))',
        '=COUNTA(Sheet2:Sheet4!B2:C3)', '=COUNT(Sheet2:Sheet4!A1:C3)', '=COUNTBLANK(Sheet2:Sheet4!A1:C3)',
        '=ROWS(Sheet2:Sheet4!B2:C3)', '=AVERAGE(Sheet2:Sheet4!B2,Sheet1!B2)', '=ISREF(Sheet2:Sheet4!B2)', '=AREAS(Sheet2:Sheet4!B2)',
        '=SUM(Sheet2:Sheet4!B2#)', '=SUM(Sheet2:Nope!B2)'
    )
    foreach ($form in $forms) {
        Case 'forms' $form $form {
            param($b)
            $refused = Enter $b 'Out!A1' $form
            $o = [ordered]@{ refused = $refused }
            if (-not $refused) { $o.read = Read $b 'Out!A1' }
            $o
        }
    }
    # Where the Formula itself sits inside its span: its own Sheet is read.
    Case 'forms' 'inside' 'Sheet3!A1 =SUM(Sheet2:Sheet4!B2)' {
        param($b); $refused = Enter $b 'Sheet3!A1' '=SUM(Sheet2:Sheet4!B2)'; [ordered]@{ refused = $refused; read = Read $b 'Sheet3!A1' }
    }
    Case 'forms' 'inside-circular' 'Sheet3!B2 =SUM(Sheet2:Sheet4!B2)' {
        param($b); $refused = Enter $b 'Sheet3!B2' '=SUM(Sheet2:Sheet4!B2)'; [ordered]@{ refused = $refused; read = Read $b 'Sheet3!B2' }
    }
    # A Sheet name with a space at either end, and both.
    Case 'forms' 'quoted' "names with spaces" {
        param($b)
        (Sh $b 'Sheet2').Name = 'Q 2'; (Sh $b 'Sheet4').Name = 'Q 4'
        $o = [ordered]@{}
        $i = 1
        foreach ($form in @("=SUM('Q 2:Sheet3'!B2)", "=SUM('Q 2:Q 4'!B2)", "=SUM('Sheet3:Q 4'!B2)", "=SUM('Q 2':'Q 4'!B2)", "=SUM('Sheet3':Sheet5!B2)")) {
            $o["e$i"] = Enter $b "Out!A$i" $form; $i++
        }
        $o.read = Read $b 'Out!A1', 'Out!A2', 'Out!A3', 'Out!A4', 'Out!A5'
        $o
    }
    # A rename of each end, for the text Excel writes.
    Case 'forms' 'rename' 'rename Sheet2 to Q 2, then Sheet4 to Jan' {
        param($b)
        [void](Enter $b 'Out!A1' '=SUM(Sheet2:Sheet4!B2)')
        (Sh $b 'Sheet2').Name = 'Q 2'; $one = Read $b 'Out!A1'
        (Sh $b 'Sheet4').Name = 'Jan'; [ordered]@{ afterFirst = $one; read = Read $b 'Out!A1' }
    }
}

# ---------------------------------------------------------------------------------------------------
# What the first pass left open: a 3D Reference handed on by a function that returns its argument, the
# functions whose other arguments COM refused as written (a number where a range is asked), and cells that
# hold text, a logical, an error or nothing on the Sheets of a span. Out!C1:C2 hold 1 and 2 as a range of
# one Sheet; Sheet2!D4 holds the text "5", Sheet3!D4 "x", Sheet3!D5 TRUE, Sheet4!D6 #DIV/0!, Sheet2!E1
# #N/A and Sheet4!E1 #DIV/0!.
if ($Groups -contains 'more') {
    $forms = @(
        '=SUM(CHOOSE(1,Sheet2:Sheet4!B2))', '=SUM(IFS(TRUE,Sheet2:Sheet4!B2))', '=SUM(SWITCH(1,1,Sheet2:Sheet4!B2))',
        '=SUM(INDEX(Sheet2:Sheet4!B2,1))', '=SUM(IFERROR(Sheet2:Sheet4!B2,1))', '=SUM(IF(FALSE,1,Sheet2:Sheet4!B2))',
        '=COUNT(IF(TRUE,Sheet2:Sheet4!B2))', '=SUM(LET(x,Sheet2:Sheet4!B2,x))', '=SUM(IF(TRUE,Sheet2:Sheet4!B2)+0)',
        '=SUM(IFNA(Sheet2:Sheet4!B2,1))', '=SUM(LET(x,Sheet2:Sheet4!B2,y,x,y))', '=LARGE(IF(TRUE,Sheet2:Sheet4!B2),1)',
        '=CONCAT(IF(TRUE,Sheet2:Sheet4!B2))', '=AND(IF(TRUE,Sheet2:Sheet4!B2))', '=IF(TRUE,Sheet2:Sheet4!B2)+1',
        '=ROWS(IF(TRUE,Sheet2:Sheet4!B2))', '=ISREF(IF(TRUE,Sheet2:Sheet4!B2))', '=SUM(IF(Sheet2:Sheet4!B2,1))',
        '=SUMIF(Sheet2:Sheet4!B2,">0")', '=SUMIF(Out!C1:C2,">0",Sheet2:Sheet4!B2)', '=COUNTIF(Sheet2:Sheet4!B2,">0")',
        '=COUNTIFS(Sheet2:Sheet4!B2,">0")', '=COUNTIFS(Out!C1,">0",Sheet2:Sheet4!B2,">0")', '=SUMIFS(Out!C1,Sheet2:Sheet4!B2,">0")',
        '=SUMIFS(Sheet2:Sheet4!B2,Out!C1,">0")', '=AVERAGEIF(Sheet2:Sheet4!B2,">0")', '=AVERAGEIF(Out!C1,">0",Sheet2:Sheet4!B2)',
        '=AVERAGEIFS(Sheet2:Sheet4!B2,Out!C1,">0")', '=AVERAGEIFS(Out!C1,Sheet2:Sheet4!B2,">0")',
        '=MAXIFS(Sheet2:Sheet4!B2,Out!C1,">0")', '=MAXIFS(Out!C1,Sheet2:Sheet4!B2,">0")',
        '=MINIFS(Sheet2:Sheet4!B2,Out!C1,">0")', '=MINIFS(Out!C1,Sheet2:Sheet4!B2,">0")',
        '=RANK.EQ(10,Sheet2:Sheet4!B2)', '=RANK.EQ(1000,Sheet2:Sheet4!B2,1)', '=RANK.EQ(Sheet2:Sheet4!B2,Out!C1:C2)',
        '=OFFSET(Out!A1,Sheet2:Sheet4!B2,1)', '=OFFSET(Out!A1,1,Sheet2:Sheet4!B2)', '=OFFSET(Out!A1,1,1,Sheet2:Sheet4!B2)',
        '=SUBTOTAL(Sheet2:Sheet4!B2,Out!C1:C2)', '=SUBTOTAL(9,Sheet2:Sheet4!B2)', '=AGGREGATE(9,6,Sheet2:Sheet4!B2)',
        '=AGGREGATE(14,6,Sheet2:Sheet4!B2,1)', '=AGGREGATE(9,Sheet2:Sheet4!B2,Out!C1:C2)',
        '=LARGE(Sheet2:Sheet4!B2:C3,2)', '=SMALL(Sheet2:Sheet4!B2:C3,2)', '=CONCAT(Sheet2:Sheet4!B2:C3)',
        '=TEXTJOIN(",",TRUE,Sheet2:Sheet4!B2:C3)', '=TEXTJOIN(",",FALSE,Sheet2:Sheet4!B2:C3)', '=TEXTJOIN(Sheet2:Sheet4!B2,TRUE,"a","b","c")',
        '=TEXTJOIN(Sheet2:Sheet4!B2:C3,TRUE,"a","b","c","d","e","f","g","h")',
        '=SUM(Sheet2:Sheet4!D4:D5)', '=COUNT(Sheet2:Sheet4!D4:D5)', '=COUNTA(Sheet2:Sheet4!D4:D5)', '=AVERAGE(Sheet2:Sheet4!D4:D5,Sheet2:Sheet4!B2)',
        '=MAX(Sheet2:Sheet4!D4:D5)', '=AND(Sheet2:Sheet4!D5)', '=AND(Sheet2:Sheet4!D4:D5)', '=OR(Sheet2:Sheet4!D4)', '=PRODUCT(Sheet2:Sheet4!D4:D5)',
        '=CONCAT(Sheet2:Sheet4!D4:D5)', '=MEDIAN(Sheet2:Sheet4!D4:D5,Sheet2:Sheet4!B2)', '=COUNTA(Sheet2:Sheet4!A1:E9)', '=COUNT(Sheet2:Sheet4!A1:E9)',
        '=SUM(Sheet2:Sheet4!D6)', '=SUM(Sheet2:Sheet4!E1)', '=COUNT(Sheet2:Sheet4!E1)', '=COUNTA(Sheet2:Sheet4!E1)', '=MAX(Sheet2:Sheet4!E1)',
        '=AND(Sheet2:Sheet4!E1)', '=CONCAT(Sheet2:Sheet4!E1)', '=SUM(Sheet2:Sheet4!E1,Sheet2:Sheet4!D6)', '=SUM(Sheet2:Sheet4!D6:E6)',
        '=AVERAGE(Sheet2:Sheet4!F1)', '=MAX(Sheet2:Sheet4!F1)', '=AND(Sheet2:Sheet4!F1)', '=STDEV.S(Sheet3:Sheet3!B2)', '=CONCAT(Sheet2:Sheet4!F1)',
        '=SUM(Sheet2:Sheet4!A:A)', '=SUM(Sheet2:Sheet4!B2:C3,Sheet2:Sheet4!B2:C3)', '=XOR(Sheet2:Sheet4!D5)'
    )
    foreach ($form in $forms) {
        Case 'more' $form $form {
            param($b)
            $o = (Sh $b 'Out'); $o.Range('C1').Value2 = 1.0; $o.Range('C2').Value2 = 2.0
            (Sh $b 'Sheet2').Range('D4').NumberFormat = '@'; (Sh $b 'Sheet2').Range('D4').Value2 = '5'
            (Sh $b 'Sheet3').Range('D4').Value2 = 'x'; (Sh $b 'Sheet3').Range('D5').Value2 = $true
            (Sh $b 'Sheet4').Range('D6').Formula = '=1/0'; (Sh $b 'Sheet2').Range('E1').Formula = '=NA()'; (Sh $b 'Sheet4').Range('E1').Formula = '=1/0'
            $refused = Enter $b 'Out!A1' $form
            $r = [ordered]@{ refused = $refused }
            if (-not $refused) { $r.read = Read $b 'Out!A1' }
            $r
        }
    }
}

# ---------------------------------------------------------------------------------------------------
if ($Groups -contains 'span') {
    # Out!A1 =SUM(Sheet2:Sheet4!B2), Out!A2 =SUM(Sheet4:Sheet2!B2) typed last to first,
    # Out!A3 =SUM(Sheet2:Sheet3!B2) of two Sheets, Out!A4 =SUM(Sheet3:Sheet3!B2) of one.
    function SpanBook($b) {
        [void](Enter $b 'Out!A1' '=SUM(Sheet2:Sheet4!B2)'); [void](Enter $b 'Out!A2' '=SUM(Sheet4:Sheet2!B2)')
        [void](Enter $b 'Out!A3' '=SUM(Sheet2:Sheet3!B2)'); [void](Enter $b 'Out!A4' '=SUM(Sheet3:Sheet3!B2)')
    }
    $cells = 'Out!A1', 'Out!A2', 'Out!A3', 'Out!A4'
    $ops = [ordered]@{
        'move-5-before-3'   = { param($b) (Sh $b 'Sheet5').Move((Sh $b 'Sheet3')) }
        'move-1-after-2'    = { param($b) (Sh $b 'Sheet1').Move([Type]::Missing, (Sh $b 'Sheet2')) }
        'move-3-to-end'     = { param($b) (Sh $b 'Sheet3').Move([Type]::Missing, (Sh $b 'Sheet5')) }
        'move-3-before-1'   = { param($b) (Sh $b 'Sheet3').Move((Sh $b 'Sheet1')) }
        'move-2-to-end'     = { param($b) (Sh $b 'Sheet2').Move([Type]::Missing, (Sh $b 'Sheet5')) }
        'move-2-after-3'    = { param($b) (Sh $b 'Sheet2').Move([Type]::Missing, (Sh $b 'Sheet3')) }
        'move-2-after-4'    = { param($b) (Sh $b 'Sheet2').Move([Type]::Missing, (Sh $b 'Sheet4')) }
        'move-2-before-1'   = { param($b) (Sh $b 'Sheet2').Move((Sh $b 'Sheet1')) }
        'move-2-to-front'   = { param($b) (Sh $b 'Sheet2').Move((Sh $b 'Out')) }
        'move-4-before-2'   = { param($b) (Sh $b 'Sheet4').Move((Sh $b 'Sheet2')) }
        'move-4-before-3'   = { param($b) (Sh $b 'Sheet4').Move((Sh $b 'Sheet3')) }
        'move-4-to-end'     = { param($b) (Sh $b 'Sheet4').Move([Type]::Missing, (Sh $b 'Sheet5')) }
        'move-4-to-front'   = { param($b) (Sh $b 'Sheet4').Move((Sh $b 'Out')) }
        'move-3-after-4'    = { param($b) (Sh $b 'Sheet3').Move([Type]::Missing, (Sh $b 'Sheet4')) }
        'move-out-after-2'  = { param($b) (Sh $b 'Out').Move([Type]::Missing, (Sh $b 'Sheet2')) }
        'delete-2'          = { param($b) (Sh $b 'Sheet2').Delete() }
        'delete-3'          = { param($b) (Sh $b 'Sheet3').Delete() }
        'delete-4'          = { param($b) (Sh $b 'Sheet4').Delete() }
        'delete-1'          = { param($b) (Sh $b 'Sheet1').Delete() }
        'delete-2-then-3'   = { param($b) (Sh $b 'Sheet2').Delete(); (Sh $b 'Sheet3').Delete() }
        'delete-2-then-4'   = { param($b) (Sh $b 'Sheet2').Delete(); (Sh $b 'Sheet4').Delete() }
        'delete-2-3-4'      = { param($b) (Sh $b 'Sheet2').Delete(); (Sh $b 'Sheet3').Delete(); (Sh $b 'Sheet4').Delete() }
        'insert-after-2'    = { param($b) $s = $b.Worksheets.Add([Type]::Missing, (Sh $b 'Sheet2')); $s.Name = 'New'; $s.Range('B2').Value2 = 7 }
        'insert-before-2'   = { param($b) $s = $b.Worksheets.Add((Sh $b 'Sheet2')); $s.Name = 'New'; $s.Range('B2').Value2 = 7 }
        'insert-after-4'    = { param($b) $s = $b.Worksheets.Add([Type]::Missing, (Sh $b 'Sheet4')); $s.Name = 'New'; $s.Range('B2').Value2 = 7 }
        'insert-before-4'   = { param($b) $s = $b.Worksheets.Add((Sh $b 'Sheet4')); $s.Name = 'New'; $s.Range('B2').Value2 = 7 }
        'copy-1-after-2'    = { param($b) (Sh $b 'Sheet1').Copy([Type]::Missing, (Sh $b 'Sheet2')) }
        'copy-1-before-2'   = { param($b) (Sh $b 'Sheet1').Copy((Sh $b 'Sheet2')) }
        'copy-5-before-4'   = { param($b) (Sh $b 'Sheet5').Copy((Sh $b 'Sheet4')) }
        'copy-5-after-4'    = { param($b) (Sh $b 'Sheet5').Copy([Type]::Missing, (Sh $b 'Sheet4')) }
        'copy-3-after-3'    = { param($b) (Sh $b 'Sheet3').Copy([Type]::Missing, (Sh $b 'Sheet3')) }
        'copy-2-before-2'   = { param($b) (Sh $b 'Sheet2').Copy((Sh $b 'Sheet2')) }
        'copy-4-after-4'    = { param($b) (Sh $b 'Sheet4').Copy([Type]::Missing, (Sh $b 'Sheet4')) }
        'rename-3'          = { param($b) (Sh $b 'Sheet3').Name = 'Mid' }
    }
    foreach ($k in $ops.Keys) {
        $op = $ops[$k]
        Case 'span' $k $k {
            param($b)
            SpanBook $b
            $before = Read $b $cells
            & $op $b
            [ordered]@{ order = (Order $b); before = $before; read = (Read $b $cells) }
        }
    }
}

# ---------------------------------------------------------------------------------------------------
if ($Groups -contains 'rows') {
    # Out!A1 =SUM(Sheet2:Sheet4!B2), A2 =SUM(Sheet2:Sheet4!B2:C3), A3 =SUM(Sheet2:Sheet4!$B$2),
    # Sheet3!A1 =SUM(Sheet2:Sheet4!B2) (on a Sheet of its span), Sheet1!A1 =SUM(Sheet2:Sheet4!B2) (outside it),
    # Out!A5 =Sheet3!B2 for comparison.
    function RowBook($b) {
        [void](Enter $b 'Out!A1' '=SUM(Sheet2:Sheet4!B2)'); [void](Enter $b 'Out!A2' '=SUM(Sheet2:Sheet4!B2:C3)')
        [void](Enter $b 'Out!A3' '=SUM(Sheet2:Sheet4!$B$2)'); [void](Enter $b 'Sheet3!A1' '=SUM(Sheet2:Sheet4!B2)')
        [void](Enter $b 'Sheet1!A1' '=SUM(Sheet2:Sheet4!B2)'); [void](Enter $b 'Out!A5' '=Sheet3!B2')
    }
    $cells = 'Out!A1', 'Out!A2', 'Out!A3', 'Out!A5', 'Sheet1!A1'
    $ops = [ordered]@{
        'sheet3-insert-row-1'    = { param($b) [void](Sh $b 'Sheet3').Range('1:1').EntireRow.Insert() }
        'sheet3-insert-row-3'    = { param($b) [void](Sh $b 'Sheet3').Range('3:3').EntireRow.Insert() }
        'sheet3-insert-row-4'    = { param($b) [void](Sh $b 'Sheet3').Range('4:4').EntireRow.Insert() }
        'sheet3-delete-row-2'    = { param($b) [void](Sh $b 'Sheet3').Range('2:2').EntireRow.Delete() }
        'sheet3-delete-row-1'    = { param($b) [void](Sh $b 'Sheet3').Range('1:1').EntireRow.Delete() }
        'sheet3-delete-row-3'    = { param($b) [void](Sh $b 'Sheet3').Range('3:3').EntireRow.Delete() }
        'sheet3-insert-col-A'    = { param($b) [void](Sh $b 'Sheet3').Range('A:A').EntireColumn.Insert() }
        'sheet3-delete-col-B'    = { param($b) [void](Sh $b 'Sheet3').Range('B:B').EntireColumn.Delete() }
        'sheet2-insert-row-1'    = { param($b) [void](Sh $b 'Sheet2').Range('1:1').EntireRow.Insert() }
        'sheet4-delete-row-2'    = { param($b) [void](Sh $b 'Sheet4').Range('2:2').EntireRow.Delete() }
        'sheet1-insert-row-1'    = { param($b) [void](Sh $b 'Sheet1').Range('1:1').EntireRow.Insert() }
        'out-insert-row-1'       = { param($b) [void](Sh $b 'Out').Range('1:1').EntireRow.Insert() }
        'sheet3-insert-cells-B2' = { param($b) [void](Sh $b 'Sheet3').Range('B2').Insert(-4121) }
        'sheet3-delete-cells-B2' = { param($b) [void](Sh $b 'Sheet3').Range('B2').Delete(-4162) }
        'grouped-insert-row-1'   = { param($b) $b.Worksheets.Item([object[]]@('Sheet2', 'Sheet3', 'Sheet4')).Select(); [void]$xl.ActiveSheet.Range('1:1').EntireRow.Insert(); (Sh $b 'Out').Select() }
        'all-insert-row-1'       = { param($b) foreach ($n in 'Sheet2', 'Sheet3', 'Sheet4') { [void](Sh $b $n).Range('1:1').EntireRow.Insert() } }
    }
    foreach ($k in $ops.Keys) {
        $op = $ops[$k]
        Case 'rows' $k $k {
            param($b)
            RowBook $b
            $before = Read $b ($cells + 'Sheet3!A1')
            & $op $b
            # Sheet3's own Formula may have moved with its row or column.
            $own = (Sh $b 'Sheet3').UsedRange.Cells | Where-Object { $_.HasFormula } | ForEach-Object { "Sheet3!$($_.Address($false, $false))" }
            [ordered]@{ before = $before; read = (Read $b ($cells + @($own))) }
        }
    }
}

# ---------------------------------------------------------------------------------------------------
if ($Groups -contains 'copy') {
    # A Sheet whose own cells hold, in A1, =SUM(Sheet2:Sheet4!B2); in A2, a Reference qualified with its
    # own name; in A3, one to another Sheet; in A4, an unqualified one.
    $ops = [ordered]@{
        'copy-2-to-end'     = @('Sheet2', { param($b) (Sh $b 'Sheet2').Copy([Type]::Missing, (Sh $b 'Sheet5')) })
        'copy-2-after-2'    = @('Sheet2', { param($b) (Sh $b 'Sheet2').Copy([Type]::Missing, (Sh $b 'Sheet2')) })
        'copy-2-before-2'   = @('Sheet2', { param($b) (Sh $b 'Sheet2').Copy((Sh $b 'Sheet2')) })
        'copy-2-after-3'    = @('Sheet2', { param($b) (Sh $b 'Sheet2').Copy([Type]::Missing, (Sh $b 'Sheet3')) })
        'copy-4-to-end'     = @('Sheet4', { param($b) (Sh $b 'Sheet4').Copy([Type]::Missing, (Sh $b 'Sheet5')) })
        'copy-4-after-4'    = @('Sheet4', { param($b) (Sh $b 'Sheet4').Copy([Type]::Missing, (Sh $b 'Sheet4')) })
        'copy-4-before-4'   = @('Sheet4', { param($b) (Sh $b 'Sheet4').Copy((Sh $b 'Sheet4')) })
        'copy-4-to-front'   = @('Sheet4', { param($b) (Sh $b 'Sheet4').Copy((Sh $b 'Out')) })
        'copy-3-to-end'     = @('Sheet3', { param($b) (Sh $b 'Sheet3').Copy([Type]::Missing, (Sh $b 'Sheet5')) })
        'copy-1-to-end'     = @('Sheet1', { param($b) (Sh $b 'Sheet1').Copy([Type]::Missing, (Sh $b 'Sheet5')) })
        'copy-2-new-book'   = @('Sheet2', { param($b) (Sh $b 'Sheet2').Copy(); $script:other = $xl.ActiveWorkbook })
    }
    foreach ($k in $ops.Keys) {
        $src = $ops[$k][0]; $op = $ops[$k][1]
        Case 'copy' $k "$k (formulas on $src)" {
            param($b)
            [void](Enter $b "$src!A1" '=SUM(Sheet2:Sheet4!B2)'); [void](Enter $b "$src!A2" "=$src!B2")
            [void](Enter $b "$src!A3" '=Sheet1!B2'); [void](Enter $b "$src!A4" '=B2')
            [void](Enter $b "$src!A5" "=SUM($src!B2:C3)"); [void](Enter $b "$src!A6" "=SUM(${src}:Sheet5!B2)")
            $script:other = $null
            $names = (1..$b.Worksheets.Count | ForEach-Object { $b.Worksheets.Item($_).Name })
            & $op $b
            $o = [ordered]@{ order = (Order $b) }
            if ($script:other) {
                $s = $script:other.Worksheets.Item(1); $xl.Calculate()
                $o.copy = [ordered]@{}
                foreach ($c in 'A1', 'A2', 'A3', 'A4', 'A5', 'A6') { $o.copy[$c] = [ordered]@{ formula = [string]$s.Range($c).Formula2; text = [string]$s.Range($c).Text } }
                $script:other.Close($false)
            }
            else {
                $new = (1..$b.Worksheets.Count | ForEach-Object { $b.Worksheets.Item($_).Name }) | Where-Object { $names -notcontains $_ }
                $cells = foreach ($c in 'A1', 'A2', 'A3', 'A4', 'A5', 'A6') { "$src!$c"; "$new!$c" }
                $o.copy = $new; $o.read = Read $b $cells
            }
            $o
        }
    }
}

# ---------------------------------------------------------------------------------------------------
if ($Groups -contains 'deleted') {
    Case 'deleted' 'delete-3' 'References to Sheet3, then Sheet3 deleted' {
        param($b)
        $forms = '=Sheet3!B2', '=Sheet3!B2:C3', '=SUM(Sheet3!B2:C3)', '=Sheet3!$B$2', '=Sheet3!B2+Sheet1!B2', '=SUM(Sheet3!B:B)', '=Sheet3!2:2', '=ROW(Sheet3!B5)', "='Sheet3'!B2"
        $i = 1; foreach ($f in $forms) { [void](Enter $b "Out!A$i" $f); $i++ }
        (Sh $b 'Sheet3').Name = 'Sheet 3'
        [void](Enter $b 'Out!A10' "='Sheet 3'!B2")
        $before = Read $b (1..10 | ForEach-Object { "Out!A$_" })
        (Sh $b 'Sheet 3').Delete()
        [ordered]@{ before = $before; read = (Read $b (1..10 | ForEach-Object { "Out!A$_" })) }
    }
}

# ---------------------------------------------------------------------------------------------------
if ($Groups -contains 'names') {
    function Fresh {
        $b = $xl.Workbooks.Add()
        while ($b.Worksheets.Count -gt 1) { $b.Worksheets.Item($b.Worksheets.Count).Delete() }
        return $b
    }
    $cases = [ordered]@{
        'add-after-gap'      = { param($b) $s2 = $b.Worksheets.Add([Type]::Missing, $b.Worksheets.Item(1)); $s3 = $b.Worksheets.Add([Type]::Missing, $s2); $s2.Delete(); [void]$b.Worksheets.Add([Type]::Missing, $s3) }
        'add-after-rename'   = { param($b) $b.Worksheets.Item(1).Name = 'Sheet5'; [void]$b.Worksheets.Add([Type]::Missing, $b.Worksheets.Item(1)) }
        'add-after-rename-3' = { param($b) $b.Worksheets.Item(1).Name = 'Sheet3'; [void]$b.Worksheets.Add([Type]::Missing, $b.Worksheets.Item(1)); [void]$b.Worksheets.Add([Type]::Missing, $b.Worksheets.Item(2)) }
        'add-after-other'    = { param($b) $b.Worksheets.Item(1).Name = 'Jan'; [void]$b.Worksheets.Add([Type]::Missing, $b.Worksheets.Item(1)) }
        'add-taken'          = { param($b) $s = $b.Worksheets.Add([Type]::Missing, $b.Worksheets.Item(1)); $s.Name = 'Sheet2x'; $b.Worksheets.Item(1).Name = 'Sheet2'; [void]$b.Worksheets.Add([Type]::Missing, $s) }
        'copy-31'            = { param($b) $b.Worksheets.Item(1).Name = ('A' * 31); $b.Worksheets.Item(1).Copy([Type]::Missing, $b.Worksheets.Item(1)) }
        'copy-30'            = { param($b) $b.Worksheets.Item(1).Name = ('A' * 30); $b.Worksheets.Item(1).Copy([Type]::Missing, $b.Worksheets.Item(1)) }
        'copy-27'            = { param($b) $b.Worksheets.Item(1).Name = ('A' * 27); $b.Worksheets.Item(1).Copy([Type]::Missing, $b.Worksheets.Item(1)) }
        'copy-28-twice'      = { param($b) $b.Worksheets.Item(1).Name = ('A' * 28); $b.Worksheets.Item(1).Copy([Type]::Missing, $b.Worksheets.Item(1)); $b.Worksheets.Item(1).Copy([Type]::Missing, $b.Worksheets.Item(2)) }
        'copy-31-twice'      = { param($b) $b.Worksheets.Item(1).Name = ('A' * 31); $b.Worksheets.Item(1).Copy([Type]::Missing, $b.Worksheets.Item(1)); $b.Worksheets.Item(1).Copy([Type]::Missing, $b.Worksheets.Item(2)) }
        'copy-copy'          = { param($b) $b.Worksheets.Item(1).Copy([Type]::Missing, $b.Worksheets.Item(1)); $b.Worksheets.Item(2).Copy([Type]::Missing, $b.Worksheets.Item(2)) }
        'copy-twice'         = { param($b) $b.Worksheets.Item(1).Copy([Type]::Missing, $b.Worksheets.Item(1)); $b.Worksheets.Item(1).Copy([Type]::Missing, $b.Worksheets.Item(2)) }
        'copy-paren-name'    = { param($b) $b.Worksheets.Item(1).Name = 'Jan (2)'; $b.Worksheets.Item(1).Copy([Type]::Missing, $b.Worksheets.Item(1)) }
    }
    foreach ($k in $cases.Keys) {
        $op = $cases[$k]
        $b = $null; $rec = [ordered]@{ group = 'names'; id = $k; what = $k }
        try { $b = Fresh; & $op $b; $rec.order = Order $b }
        catch { $rec.error = $_.Exception.Message; if ($b) { $rec.order = Order $b } }
        finally { if ($b) { $b.Close($false) } }
        ($rec | ConvertTo-Json -Compress) | Add-Content -Encoding UTF8 $Out
        Write-Host "$k $($rec.order) $($rec.error)"
    }
}

$xl.Quit()
[void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl)
Write-Host 'done'
