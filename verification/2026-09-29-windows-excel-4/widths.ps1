<#
.SYNOPSIS
    Part C of docs/specs/exsheet/verify-on-windows-4.md, "Column widths" (SH-26), Excel's side, with
    real keys and the real mouse:

        powershell -File widths.ps1

    In an empty sheet: 1234567890 typed into F2, then 12345678901 into F3; column F's width read
    after each. Then F narrowed by dragging the right-hand edge of its header 40 screen pixels to the
    left with the real mouse, and 123456789012 typed into F4. ColumnWidth (characters), Width
    (points), each cell's text, and customWidth as Excel's file writes it (a copy saved as .xlsx) are
    read after each step. Steps go to widths.jsonl beside this script and screenshots to shots\
    (C-W-...).
#>
#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel\excel-driver.ps1')
$script:Shots = Join-Path $PSScriptRoot 'shots'
$Log = Join-Path $PSScriptRoot 'widths.jsonl'
$Utf8 = New-Object Text.UTF8Encoding $false
$xl = Connect-Excel
. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel-2\case-guard.ps1')
Start-CaseGuard
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Write-Row($Row) { [IO.File]::AppendAllText($Log, (($Row | ConvertTo-Json -Compress -Depth 5) + "`n"), $Utf8) }

# customWidth on column F, as the .xlsx Excel writes says it (absent is false).
function Get-CustomWidth($ws) {
    $tmp = Join-Path $env:TEMP ('exgrid-widths-{0}.xlsx' -f [Guid]::NewGuid().ToString('N'))
    $copy = $ws.Parent
    $copy.SaveCopyAs($tmp)
    try {
        $zip = [IO.Compression.ZipFile]::OpenRead($tmp)
        try {
            $entry = $zip.Entries | Where-Object { $_.FullName -eq 'xl/worksheets/sheet1.xml' } | Select-Object -First 1
            $reader = New-Object IO.StreamReader($entry.Open())
            $xml = $reader.ReadToEnd(); $reader.Dispose()
        } finally { $zip.Dispose() }
    } finally { Remove-Item $tmp -ErrorAction SilentlyContinue }
    $cols = [regex]::Matches($xml, '<col [^>]*/>') | ForEach-Object { $_.Value }
    $f = $cols | Where-Object { $_ -match 'min="(\d+)"' -and [int]$Matches[1] -le 6 -and $_ -match 'max="(\d+)"' -and [int]$Matches[1] -ge 6 } | Select-Object -First 1
    return [pscustomobject]@{ col = $f; customWidth = [bool]($f -and $f -match 'customWidth="(1|true)"') }
}

function Snap([string]$Step, [string]$Method, [switch]$Shot) {
    $ws = $xl.ActiveSheet
    $f = $ws.Range('F1')
    $cw = Get-CustomWidth $ws
    $row = [ordered]@{ step = $Step; method = $Method
        columnWidth = [double]$f.ColumnWidth; widthPoints = [double]$f.Width; standardWidth = [double]$ws.StandardWidth
        customWidth = $cw.customWidth; colXml = $cw.col
        F2 = [string]$ws.Range('F2').Text; F3 = [string]$ws.Range('F3').Text; F4 = [string]$ws.Range('F4').Text
        activeCell = [string]$xl.ActiveCell.Address($false, $false); shot = $null }
    if ($Shot) {
        $name = ('C-W-{0}' -f ($Step -replace '[^A-Za-z0-9]+', '-')).Trim('-')
        Save-View $xl $name 'H6'
        $row.shot = "shots/$name.png"
    }
    Write-Row $row
    Write-Host ('{0,-48} F width {1,6} chars ({2} pt) custom {3}  F2 "{4}" F3 "{5}" F4 "{6}"' -f $Step, $row.columnWidth, $row.widthPoints, $row.customWidth, $row.F2, $row.F3, $row.F4)
}
function Keys([string]$K, [int]$Settle = 450) { Send-Keys $xl $K $Settle }
function ClickCell([string]$A1) { $r = Get-CellRect $xl $A1; Click-At $xl $r.X $r.Y; Start-Sleep -Milliseconds 250 }

Set-CaseDue 150
try {
    [void](Reset-Book $xl @{}); Show-Excel $xl; Keys '{ESC}' 200
    Snap 'empty sheet' 'COM' -Shot
    ClickCell 'F2'; Keys '1234567890~'
    Snap 'typed 1234567890 in F2' 'keys' -Shot
    Keys '12345678901~'
    Snap 'typed 12345678901 in F3' 'keys' -Shot
    # F's header edge: the right-hand edge of F1, in the header row above row 1.
    $f1 = Get-CellRect $xl 'F1'
    $px = ($f1.Bottom - $f1.Top) / [double]$xl.ActiveSheet.Range('F1').Height
    $y = [int]($f1.Top - 8 * $px)
    Write-Row ([ordered]@{ step = 'drag'; method = 'screen pixels'; fromX = $f1.Right; y = $y; toX = $f1.Right - 40; pixelsPerPoint = $px })
    Drag-From $xl $f1.Right $y ($f1.Right - 40) $y 16
    Snap 'F dragged 40 px narrower' 'mouse' -Shot
    ClickCell 'F4'; Keys '123456789012~'
    Snap 'typed 123456789012 in F4' 'keys' -Shot
}
catch {
    Write-Row ([ordered]@{ step = 'failed'; error = $_.Exception.Message; line = $_.InvocationInfo.ScriptLineNumber; stack = $_.ScriptStackTrace })
    Write-Host "failed: $($_.Exception.Message) (line $($_.InvocationInfo.ScriptLineNumber))"
}
Clear-CaseDue
if ($script:Guard.Ended) { Write-Row ([ordered]@{ step = 'Excel ended'; error = $script:Guard.Ended }) }
