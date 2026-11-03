<#
.SYNOPSIS
    Japanese input on CI's Windows runner, for ED-30's run with a real IME (ADR-0450): the Japanese
    language's basic features, which bring the Microsoft IME, and its fonts, installed as Windows
    capabilities; Japanese added to the user's languages, after the ones there, so that a window takes
    the Japanese keyboard only when os-keys.ps1's 'japanese' asks for it; and the IME's Predictive input
    turned off.

        powershell -NoProfile -ExecutionPolicy Bypass -File japanese-input.ps1

    Predictive input: with it on, the IME stands its list of suggestions under the composition, and a
    press there is the list's and never reaches the page (the sixteenth and seventeenth Windows runs'
    k8). ED-30's k8 is run with it off (decided with the user, 2026-10-16); here every case is, so that
    no press is the list's by where it happens to fall. Off is what the Settings app wrote on the desktop
    of those runs (verification/2026-10-25-windows-19/part-g/ed30/logs/): the input history and the
    system dictionary not used for suggestions. On the runner the Settings app has no IME pages to
    press, so the values are written before the IME first starts.

    Throws when anything is not as asked, so the run never goes on without the IME it reads.
#>
$ErrorActionPreference = 'Stop'

$started = Get-Date
$capabilities = Get-WindowsCapability -Online | Where-Object { $_.Name -like 'Language.Basic~~~ja-JP~*' -or $_.Name -like 'Language.Fonts.Jpan~~~*' }
foreach ($name in 'Language.Basic', 'Language.Fonts.Jpan') {
    if (-not ($capabilities | Where-Object { $_.Name -like "$name~*" })) { throw "Windows offers no capability $name for Japanese" }
}
foreach ($capability in $capabilities) {
    if ($capability.State -ne 'Installed') {
        "Installing $($capability.Name)"
        Add-WindowsCapability -Online -Name $capability.Name | Out-Null
    }
}
foreach ($capability in (Get-WindowsCapability -Online | Where-Object { $_.Name -in $capabilities.Name })) {
    if ($capability.State -ne 'Installed') { throw "$($capability.Name) is $($capability.State) after its install" }
}
"The capabilities took $([int]((Get-Date) - $started).TotalMinutes) minutes"
if (-not (Test-Path "$env:windir\System32\IME\IMEJP")) { throw 'The Microsoft IME (Japanese) is not under System32\IME after the install' }

$languages = Get-WinUserLanguageList
if (-not ($languages | Where-Object { $_.LanguageTag -eq 'ja' -or $_.LanguageTag -eq 'ja-JP' })) {
    $languages.Add('ja-JP')
    Set-WinUserLanguageList $languages -Force
}
$japanese = Get-WinUserLanguageList | Where-Object { $_.LanguageTag -eq 'ja' -or $_.LanguageTag -eq 'ja-JP' }
if (-not $japanese) { throw 'Japanese is not among the user''s languages' }
"Japanese input methods: $($japanese.InputMethodTips -join ', ')"
if (-not ($japanese.InputMethodTips | Where-Object { $_ -like '0411:*' })) { throw 'Japanese has no input method' }

$msime = 'HKCU:\Software\Microsoft\IME\15.0\IMEJP\MSIME'
if (-not (Test-Path $msime)) { New-Item -Path $msime -Force | Out-Null }
Set-ItemProperty -Path $msime -Name PredictionUseInputHistory -Type DWord -Value 0
Set-ItemProperty -Path $msime -Name PredictionUseSystemDictionary -Type DWord -Value 0
Get-ItemProperty -Path $msime | Select-Object PredictionUseInputHistory, PredictionUseSystemDictionary | Format-List | Out-String
