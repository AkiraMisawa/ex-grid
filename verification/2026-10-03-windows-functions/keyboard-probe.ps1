<#
.SYNOPSIS
Uses the repository's existing Excel Oracle SendInput/dialog watcher to ask LET's grammar by keys.
.DESCRIPTION
The original Oracle conservatively types formulas only under en-US. This wrapper is specific to
this en-GB run, with English Excel UI and invariant comma/dot syntax. It forces the same SendInput
path and switches only its own Excel window to the English (UK) keyboard. It leaves the repository's
Oracle unchanged and records its source hash. Cases have no invented expected Value: every answer
or refused entry is recorded, not declared to agree with an implementation.
#>
#Requires -Version 5.1
[CmdletBinding()]
param([string]$Oracle,[string]$Cases,[string]$Out)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if(-not $Oracle){$Oracle=Join-Path $PSScriptRoot '..\..\tests\ExSheet.Engine.Tests\ExcelOracle\oracle.ps1'}
if(-not $Cases){$Cases=Join-Path $PSScriptRoot 'keyboard-cases'}
if(-not $Out){$Out=Join-Path $PSScriptRoot 'keyboard-results.json'}
if((Get-Culture).Name -ne 'en-GB'){throw 'This wrapper is specific to en-GB; it never changes regional settings.'}
$utf8=New-Object Text.UTF8Encoding $false
$source=[IO.File]::ReadAllText($Oracle,$utf8)
$old='$script:KeysForFormulas = [bool]$Keys -and ($machineCulture -eq ''en-US'')'
if(-not $source.Contains($old)){throw 'Oracle typing guard changed: review the wrapper against the current Oracle.'}
$source=$source.Replace($old,'$script:KeysForFormulas = [bool]$Keys')
$declarations=@'
    public static class Native {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr LoadKeyboardLayout(string id, uint flags);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint thread);
'@
$source=$source.Replace('    public static class Native {',$declarations)
$setup=@'
    $script:ExcelProcess = Get-ProcessOf $script:ExcelWindow
    if ($Keys) {
        $layout = [OracleKeys.Native]::LoadKeyboardLayout('00000809', 0)
        [void][OracleKeys.Native]::PostMessage($script:ExcelWindow, 0x0050, [IntPtr]::Zero, $layout)
        Start-Sleep -Milliseconds 200
        [uint32]$owner = 0
        $thread = [OracleKeys.Native]::GetWindowThreadProcessId($script:ExcelWindow, [ref]$owner)
        if ([OracleKeys.Native]::GetKeyboardLayout($thread) -ne $layout) {throw 'Could not select English (UK) for this Excel window.'}
    }
'@
$source=$source.Replace('    $script:ExcelProcess = Get-ProcessOf $script:ExcelWindow',$setup)
$tempScript=Join-Path ([IO.Path]::GetTempPath()) ('exsheet-function-keys-'+[Guid]::NewGuid().ToString('N')+'.ps1')
try {
    [IO.File]::WriteAllText($tempScript,$source,$utf8)
    & $tempScript -Cases $Cases -Out $Out -Keys -Visible
    $report=[IO.File]::ReadAllText($Out,$utf8) | ConvertFrom-Json
    $report | Add-Member NoteProperty originalOracleSha256 (Get-FileHash $Oracle -Algorithm SHA256).Hash
    $report | Add-Member NoteProperty caseFileSha256 (Get-FileHash (Join-Path $Cases 'let.json') -Algorithm SHA256).Hash
    $report | Add-Member NoteProperty wrapperSha256 (Get-FileHash $PSCommandPath -Algorithm SHA256).Hash
    [IO.File]::WriteAllText($Out,($report | ConvertTo-Json -Depth 16),$utf8)
    if(@($report.cases | Where-Object {$_.status -ne 'recorded'}).Count -gt 0){throw 'A keyboard observation is blocked or failed; inspect its record.'}
} finally {Remove-Item -LiteralPath $tempScript -ErrorAction SilentlyContinue}
