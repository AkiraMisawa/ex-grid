# What Japanese input the runner has, and an attempt to add the Microsoft IME to the user's languages.
$ErrorActionPreference = 'Continue'
Get-WinUserLanguageList | Format-List | Out-String | Write-Output
Get-WindowsCapability -Online | Where-Object Name -like '*ja-JP*' | Format-Table Name, State | Out-String | Write-Output
$started = Get-Date
foreach ($cap in (Get-WindowsCapability -Online | Where-Object { $_.Name -like 'Language.Basic~~~ja-JP*' -or $_.Name -like 'Language.Fonts.Jpan*' })) {
  try { Add-WindowsCapability -Online -Name $cap.Name | Out-String | Write-Output } catch { Write-Output "add $($cap.Name): $_" }
}
Write-Output "capabilities took $((Get-Date) - $started)"
$list = Get-WinUserLanguageList
$list.Add('ja-JP')
Set-WinUserLanguageList $list -Force
Get-WinUserLanguageList | Format-List | Out-String | Write-Output
