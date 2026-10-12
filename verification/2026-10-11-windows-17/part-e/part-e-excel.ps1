<#
.SYNOPSIS
    docs/specs/exsheet/verify-on-windows-17.md, Part E, the Excel half: each layout of items.json built as a
    PivotTable in Excel from the item's own CSV, and the PivotTable read cell by cell.

        powershell -File part-e-excel.ps1 -Items items.json -Dir <folder of the CSVs> -Out excel.json [-Id e02,e03]

    Excel is driven through COM, as docs/specs/expivot/excel-behaviours.md says: an Excel of the script's own
    (New-Object; refused if it is an Excel already running), visible; the CSV opened with Workbooks.Open as the
    user's regional format reads it (Local), as a double click would; a PivotCache over its used range and a
    PivotTable on a new sheet at A3; each row and column field's Orientation and Position in the item's order;
    each Value Field added with AddDataField and its Function (Sum, Count, Average, Max, Min, Product, Count
    Numbers, StdDev, StdDevp, Var, Varp), and its Calculation for Show Values As; an Item hidden by
    PivotItem.Visible; the form by RowAxisLayout. Then TableRange1 (and TableRange2, the filters too): each
    cell's Text, Value2 and NumberFormat, row by row; the Value Fields' captions as Excel named them; and a
    picture of Excel's window (PrintWindow), the account's corner of the title bar left out. Nothing is saved.
#>
param([string]$Items, [string]$Dir, [string]$Out, [string]$Id)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type -Namespace E -Name N -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
'@
[void][E.N]::SetProcessDPIAware()
$Utf8 = New-Object Text.UTF8Encoding $false
$spec = Get-Content $Items -Raw -Encoding UTF8 | ConvertFrom-Json
$ids = if ($Id) { $Id -split ',' } else { @() }
$Functions = @{ 'Sum' = -4157; 'Count' = -4112; 'Average' = -4106; 'Max' = -4136; 'Min' = -4139; 'Product' = -4149; 'Count Numbers' = -4113; 'StdDev' = -4155; 'StdDevp' = -4156; 'Var' = -4164; 'Varp' = -4165 }
$Calculations = @{ '% of Grand Total' = 8; '% of Column Total' = 7; '% of Row Total' = 6 }
$Layouts = @{ 'compact' = 0; 'tabular' = 1; 'outline' = 2 }
$M = [Type]::Missing
$results = @()

function Cells-Of($Range) {
    $rows = @()
    for ($r = 1; $r -le $Range.Rows.Count; $r++) {
        $row = @()
        for ($c = 1; $c -le $Range.Columns.Count; $c++) {
            $cell = $Range.Cells.Item($r, $c); $v = $cell.Value2
            $vt = if ($null -eq $v) { $null } elseif ($v -is [double]) { $v.ToString('R', [Globalization.CultureInfo]::InvariantCulture) } elseif ($v -is [int]) { "error $v" } else { [string]$v }
            $row += [ordered]@{ text = [string]$cell.Text; value2 = $vt; format = [string]$cell.NumberFormat }
        }
        $rows += , $row
    }
    return , $rows
}

foreach ($it in $spec.items) {
    if ($ids.Count -and $ids -notcontains $it.id) { continue }
    $res = [ordered]@{ id = $it.id; item = $it.item; steps = @() }
    $xl = $null; $xpid = 0
    try {
        $file = Join-Path $Dir "$($it.id).csv"
        [IO.File]::WriteAllText($file, $it.csv, $Utf8)
        $before = @(Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
        $xl = New-Object -ComObject Excel.Application
        [uint32]$q = 0; [void][E.N]::GetWindowThreadProcessId([IntPtr][long]$xl.Hwnd, [ref]$q); $xpid = [int]$q
        if ($before -contains $xpid) { throw "New-Object gave an Excel already running ($xpid)" }
        $xl.Visible = $true; $xl.DisplayAlerts = $false
        $res.excelProcess = $xpid
        # Workbooks.Open(Filename, ..., Local): the 14th argument true, as the user's regional format reads it.
        $wb = $xl.Workbooks.Open($file, $M, $M, $M, $M, $M, $M, $M, $M, $M, $M, $M, $false, $true)
        $wb.Windows.Item(1).WindowState = -4137
        $src = $wb.Worksheets.Item(1).UsedRange
        $res.source = Cells-Of $src
        $ws = $wb.Worksheets.Add()
        $pc = $wb.PivotCaches().Create(1, $src)
        $pt = $pc.CreatePivotTable($ws.Range('A3'), 'PT')
        $i = 1; foreach ($f in $it.rows) { $pf = $pt.PivotFields($f); $pf.Orientation = 1; $pf.Position = $i; $i++; $res.steps += "$f to Rows" }
        $i = 1; foreach ($f in $it.cols) { $pf = $pt.PivotFields($f); $pf.Orientation = 2; $pf.Position = $i; $i++; $res.steps += "$f to Columns" }
        $captions = @()
        foreach ($v in $it.values) {
            $df = $pt.AddDataField($pt.PivotFields($v.field), $M, $Functions[$v.agg])
            if ($v.show) { $df.Calculation = $Calculations[$v.show] }
            $captions += [ordered]@{ field = $v.field; agg = $v.agg; show = $v.show; caption = [string]$df.Caption; function = [int]$df.Function; calculation = [int]$df.Calculation; numberFormat = [string]$df.NumberFormat }
        }
        $res.valueFields = $captions
        if ($it.hide) {
            foreach ($p in $it.hide.PSObject.Properties) { foreach ($h in $p.Value) { $pt.PivotFields($p.Name).PivotItems($h).Visible = $false; $res.steps += "$h hidden in $($p.Name)" } }
        }
        if ($it.layout) { $pt.RowAxisLayout($Layouts[$it.layout]); $res.steps += "RowAxisLayout $($it.layout)" }
        $pt.RefreshTable() | Out-Null
        Start-Sleep -Milliseconds 400
        $res.tableRange1 = [string]$pt.TableRange1.Address($false, $false)
        $res.tableRange2 = [string]$pt.TableRange2.Address($false, $false)
        $res.cells = Cells-Of $pt.TableRange2
        $res.layout = [ordered]@{ rowAxisLayout = $it.layout; compactLayoutRowHeader = [string]$pt.CompactLayoutRowHeader; compactLayoutColumnHeader = [string]$pt.CompactLayoutColumnHeader; grandTotalName = [string]$pt.GrandTotalName; rowGrand = [bool]$pt.RowGrand; columnGrand = [bool]$pt.ColumnGrand }
        # The picture: Excel's window drawn by itself, cut to the PivotTable's area (clear of the title bar).
        [void]$ws.Activate(); [void]$ws.Range('A1').Select()
        Start-Sleep -Milliseconds 500
        $h = [IntPtr][long]$xl.Hwnd; $r = New-Object E.N+RECT; [void][E.N]::GetWindowRect($h, [ref]$r)
        $w = $r.Right - $r.Left; $hh = $r.Bottom - $r.Top
        $bmp = New-Object Drawing.Bitmap $w, $hh; $g = [Drawing.Graphics]::FromImage($bmp); $hdc = $g.GetHdc()
        [void][E.N]::PrintWindow($h, $hdc, 2); $g.ReleaseHdc($hdc); $g.Dispose()
        $pane = $xl.ActiveWindow.ActivePane; $tr = $pt.TableRange2
        $x0 = [Math]::Max(0, $pane.PointsToScreenPixelsX($ws.Range('A1').Left) - $r.Left - 60); $y0 = [Math]::Max(0, $pane.PointsToScreenPixelsY($ws.Range('A1').Top) - $r.Top - 40)
        $x1 = [Math]::Min($w, $pane.PointsToScreenPixelsX($tr.Left + $tr.Width) - $r.Left + 300); $y1 = [Math]::Min($hh, $pane.PointsToScreenPixelsY($tr.Top + $tr.Height) - $r.Top + 60)
        $crop = $bmp.Clone((New-Object Drawing.Rectangle $x0, $y0, ($x1 - $x0), ($y1 - $y0)), $bmp.PixelFormat)
        $shot = Join-Path $Dir "$($it.id)-excel.png"; $crop.Save($shot, [Drawing.Imaging.ImageFormat]::Png); $crop.Dispose(); $bmp.Dispose()
        $res.shot = Split-Path $shot -Leaf
    } catch { $res.error = $_.Exception.Message + ' (line ' + $_.InvocationInfo.ScriptLineNumber + ')' }
    finally {
        try { foreach ($b in @($xl.Workbooks)) { $b.Close($false) } } catch { }
        try { $xl.Quit() } catch { }
        try { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl) } catch { }
        $xl = $null; [GC]::Collect(); [GC]::WaitForPendingFinalizers()
        if ($xpid) { for ($k = 0; $k -lt 20; $k++) { if (-not (Get-Process -Id $xpid -ErrorAction SilentlyContinue)) { break }; Start-Sleep -Milliseconds 500 }
            if (Get-Process -Id $xpid -ErrorAction SilentlyContinue) { Stop-Process -Id $xpid -Force; $res.ended = 'Stop-Process (its own)' } }
    }
    $results += $res
    [IO.File]::WriteAllText($Out, (ConvertTo-Json -InputObject @($results) -Depth 12), $Utf8)
    Write-Host ("{0}: {1}" -f $it.id, $(if ($res.error) { $res.error } else { "$($res.tableRange2), $(@($res.cells).Count) rows" }))
}
