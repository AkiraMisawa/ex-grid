<#
.SYNOPSIS
    docs/specs/exsheet/verify-on-windows-17.md, Part E, item 27 of docs/specs/expivot/excel-behaviours.md: the words
    of Excel's Japanese edition, read from Excel's own screens. Excel's display language is Japanese while this runs
    (HKCU\Software\Microsoft\Office\16.0\Common\LanguageResources\UILanguageTag, set and put back by the caller).

        powershell -File item27-excel.ps1 -Dir <folder> -Out item27-excel.json

    One CSV with Japanese field names (地域, 商品, デスク, 金額), a blank Region among its records. In an Excel of
    the script's own, visible, opened with Workbooks.Open (Local), a PivotTable at A3 of a new sheet:
      1. Compact form: rows 地域, columns 商品, values Sum of 金額 and Count of 地域 (two Value Fields, so the Values
         field stands in the columns), report filter デスク: every cell's text, then the filter with nothing hidden,
         then with one Item hidden.
      2. Tabular form: rows 地域 and デスク, values Sum of 金額: the subtotal rows' text.
      3. The Field List shown (Workbook.ShowPivotTableFieldList): its pane's name through UI Automation.
    Pictures of the PivotTable and of the pane (PrintWindow, cut clear of the title bar). Nothing is saved.
#>
param([string]$Dir, [string]$Out)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, UIAutomationClient, UIAutomationTypes
Add-Type -Namespace E -Name N -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
'@
[void][E.N]::SetProcessDPIAware()
$M = [Type]::Missing
$res = [ordered]@{ ran = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'); steps = @() }
$csv = "地域,商品,デスク,金額`r`n東,りんご,D1,10`r`n東,なし,D2,20`r`n西,りんご,D3,30`r`n,なし,D1,5`r`n西,なし,D2,15`r`n"
$file = Join-Path $Dir 'item27.csv'
[IO.File]::WriteAllText($file, $csv, (New-Object Text.UTF8Encoding $true))

function Cells-Of($Range) {
    $rows = @()
    for ($r = 1; $r -le $Range.Rows.Count; $r++) {
        $row = @(); for ($c = 1; $c -le $Range.Columns.Count; $c++) { $row += [string]$Range.Cells.Item($r, $c).Text }
        $rows += , $row
    }
    return , $rows
}
function Shot($xl, $name, $x0, $y0, $x1, $y1) {
    $h = [IntPtr][long]$xl.Hwnd; $r = New-Object E.N+RECT; [void][E.N]::GetWindowRect($h, [ref]$r)
    $w = $r.Right - $r.Left; $hh = $r.Bottom - $r.Top
    $bmp = New-Object Drawing.Bitmap $w, $hh; $g = [Drawing.Graphics]::FromImage($bmp); $hdc = $g.GetHdc()
    [void][E.N]::PrintWindow($h, $hdc, 2); $g.ReleaseHdc($hdc); $g.Dispose()
    $x0 = [Math]::Max(0, $x0 - $r.Left); $y0 = [Math]::Max(0, $y0 - $r.Top); $x1 = [Math]::Min($w, $x1 - $r.Left); $y1 = [Math]::Min($hh, $y1 - $r.Top)
    $crop = $bmp.Clone((New-Object Drawing.Rectangle $x0, $y0, ($x1 - $x0), ($y1 - $y0)), $bmp.PixelFormat)
    $crop.Save((Join-Path $Dir $name), [Drawing.Imaging.ImageFormat]::Png); $crop.Dispose(); $bmp.Dispose()
    return $name
}
function Pivot-Shot($xl, $ws, $pt, $name) {
    [void]$ws.Activate(); [void]$ws.Range('A1').Select(); Start-Sleep -Milliseconds 600
    $pane = $xl.ActiveWindow.ActivePane; $tr = $pt.TableRange2
    $x0 = $pane.PointsToScreenPixelsX($ws.Range('A1').Left) - 60; $y0 = $pane.PointsToScreenPixelsY($ws.Range('A1').Top) - 40
    $x1 = $pane.PointsToScreenPixelsX($tr.Left + $tr.Width) + 200; $y1 = $pane.PointsToScreenPixelsY($tr.Top + $tr.Height) + 60
    return Shot $xl $name $x0 $y0 $x1 $y1
}

$xl = $null; $xpid = 0
try {
    $before = @(Get-Process excel -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
    $xl = New-Object -ComObject Excel.Application
    [uint32]$q = 0; [void][E.N]::GetWindowThreadProcessId([IntPtr][long]$xl.Hwnd, [ref]$q); $xpid = [int]$q
    if ($before -contains $xpid) { throw "New-Object gave an Excel already running ($xpid)" }
    $xl.Visible = $true; $xl.DisplayAlerts = $false
    $res.uiLanguage = $xl.LanguageSettings.LanguageID(2)      # msoLanguageIDUI
    $res.excel = "$($xl.Version) (build $($xl.Build))"
    $wb = $xl.Workbooks.Open($file, $M, $M, $M, $M, $M, $M, $M, $M, $M, $M, $M, $false, $true)
    $wb.Windows.Item(1).WindowState = -4137
    $src = $wb.Worksheets.Item(1).UsedRange
    $res.source = Cells-Of $src

    # 1. Compact form, two Value Fields, a column field, a report filter.
    $ws = $wb.Worksheets.Add(); $ws.Name = 'compact'
    $pt = $wb.PivotCaches().Create(1, $src).CreatePivotTable($ws.Range('A3'), 'PT1')
    $d1 = $pt.AddDataField($pt.PivotFields('金額'), $M, -4157)
    $d2 = $pt.AddDataField($pt.PivotFields('地域'), $M, -4112)
    $res.captions = @([string]$d1.Caption, [string]$d2.Caption)
    $pf = $pt.PivotFields('地域'); $pf.Orientation = 1; $pf.Position = 1
    $pf = $pt.PivotFields('商品'); $pf.Orientation = 2; $pf.Position = 1
    $pf = $pt.PivotFields('デスク'); $pf.Orientation = 3; $pf.Position = 1
    $pt.RefreshTable() | Out-Null; Start-Sleep -Milliseconds 400
    $res.compact = [ordered]@{ tableRange2 = [string]$pt.TableRange2.Address($false, $false); cells = Cells-Of $pt.TableRange2
        compactLayoutRowHeader = [string]$pt.CompactLayoutRowHeader; compactLayoutColumnHeader = [string]$pt.CompactLayoutColumnHeader
        grandTotalName = [string]$pt.GrandTotalName; dataPivotFieldCaption = [string]$pt.DataPivotField.Caption
        blankItem = @($pt.PivotFields('地域').PivotItems() | ForEach-Object { [string]$_.Caption }) }
    $res.compact.shot = Pivot-Shot $xl $ws $pt 'item27-compact-excel.png'
    $pageCell = $pt.PageRange.Cells.Item(1, 2)
    $res.filterAll = [string]$pageCell.Text
    $pt.PivotFields('デスク').EnableMultiplePageItems = $true
    $pt.PivotFields('デスク').PivotItems('D2').Visible = $false
    $pt.RefreshTable() | Out-Null; Start-Sleep -Milliseconds 300
    $res.filterMultiple = [string]$pt.PageRange.Cells.Item(1, 2).Text
    $res.compactFiltered = Cells-Of $pt.TableRange2
    $res.compactFilteredShot = Pivot-Shot $xl $ws $pt 'item27-compact-filtered-excel.png'

    # 2. Tabular form, two row fields: the subtotal rows.
    $ws2 = $wb.Worksheets.Add(); $ws2.Name = 'tabular'
    $pt2 = $wb.PivotCaches().Create(1, $src).CreatePivotTable($ws2.Range('A3'), 'PT2')
    [void]$pt2.AddDataField($pt2.PivotFields('金額'), $M, -4157)
    $pf = $pt2.PivotFields('地域'); $pf.Orientation = 1; $pf.Position = 1
    $pf = $pt2.PivotFields('デスク'); $pf.Orientation = 1; $pf.Position = 2
    $pt2.RowAxisLayout(1); $pt2.RefreshTable() | Out-Null; Start-Sleep -Milliseconds 400
    $res.tabular = [ordered]@{ cells = Cells-Of $pt2.TableRange2 }
    $res.tabular.shot = Pivot-Shot $xl $ws2 $pt2 'item27-tabular-excel.png'

    # 3. The Field List pane, on the compact sheet.
    [void]$ws.Activate(); [void]$ws.Range('A4').Select(); $wb.ShowPivotTableFieldList = $true; Start-Sleep -Milliseconds 1500
    $root = [Windows.Automation.AutomationElement]::FromHandle([IntPtr][long]$xl.Hwnd)
    $names = @(); $paneRect = $null
    foreach ($e in $root.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition)) {
        $n = $e.Current.Name
        if ($n -match 'ピボット|フィールド|レポートに追加|ボックス間|フィルター|行|列|値') {
            $names += "$n | $($e.Current.ControlType.ProgrammaticName)"
            if ($n -eq 'ピボットテーブルのフィールド' -and -not $paneRect) { $paneRect = $e.Current.BoundingRectangle }
        }
    }
    $res.fieldListNames = @($names | Select-Object -Unique | Select-Object -First 60)
    if ($paneRect) {
        $res.fieldListRect = "$($paneRect.Left),$($paneRect.Top),$($paneRect.Width),$($paneRect.Height)"
        $res.fieldListShot = Shot $xl 'item27-field-list-excel.png' ([int]$paneRect.Left) ([int]$paneRect.Top) ([int]($paneRect.Left + $paneRect.Width)) ([int]($paneRect.Top + [Math]::Min($paneRect.Height, 900)))
    }
} catch { $res.error = $_.Exception.Message + ' (line ' + $_.InvocationInfo.ScriptLineNumber + ')' }
finally {
    try { foreach ($b in @($xl.Workbooks)) { $b.Close($false) } } catch { }
    try { $xl.Quit() } catch { }
    try { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl) } catch { }
    $xl = $null; [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    if ($xpid) { for ($k = 0; $k -lt 20; $k++) { if (-not (Get-Process -Id $xpid -ErrorAction SilentlyContinue)) { break }; Start-Sleep -Milliseconds 500 }
        if (Get-Process -Id $xpid -ErrorAction SilentlyContinue) { Stop-Process -Id $xpid -Force; $res.ended = 'Stop-Process (its own)' } }
}
[IO.File]::WriteAllText($Out, (ConvertTo-Json -InputObject $res -Depth 10), (New-Object Text.UTF8Encoding $false))
if ($res.error) { "error: $($res.error)" } else { 'done' }
