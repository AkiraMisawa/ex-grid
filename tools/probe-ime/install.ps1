# Temporary probe: whether the hosted Windows runner can take the Microsoft IME (Japanese).
$ErrorActionPreference = 'Continue'
(Get-CimInstance Win32_OperatingSystem).Caption
Test-Path C:\Windows\System32\IME\IMEJP; Get-ChildItem C:\Windows\System32\IME -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name
Get-WinUserLanguageList | Format-List | Out-String
$started = Get-Date
foreach ($cap in (Get-WindowsCapability -Online | Where-Object { $_.Name -like 'Language.Basic~~~ja-JP*' -or $_.Name -like 'Language.Fonts.Jpan*' })) {
  "capability $($cap.Name) state $($cap.State)"
  if ($cap.State -ne 'Installed') { try { Add-WindowsCapability -Online -Name $cap.Name | Out-String } catch { "add $($cap.Name): $_" } }
}
"capabilities took $((Get-Date) - $started)"
$list = Get-WinUserLanguageList
$list.Add('ja-JP')
Set-WinUserLanguageList $list -Force
Get-WinUserLanguageList | Format-List | Out-String
Get-ItemProperty 'HKCU:\Keyboard Layout\Preload' | Out-String
Test-Path C:\Windows\System32\IME\IMEJP
