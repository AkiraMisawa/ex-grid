# Office's languages as installed and as set: Click-to-Run's configuration, the UI resource folders, LanguageResources.
$cfg = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration'
"VersionToReport=$($cfg.VersionToReport) ClientCulture=$($cfg.ClientCulture) ProductReleaseIds=$($cfg.ProductReleaseIds) Platform=$($cfg.Platform)"
$cfg.PSObject.Properties | Where-Object { $_.Name -match 'Language|\.Cultures|culture' } | ForEach-Object { "Configuration $($_.Name)=$($_.Value)" }
foreach ($lcid in '1033', '2057', '1041') {
  $f = "C:\Program Files\Microsoft Office\root\Office16\$lcid\XLINTL32.DLL"
  if (Test-Path $f) { $i = Get-Item $f; "XLINTL32 $lcid $($i.VersionInfo.ProductVersion) $($i.LastWriteTime.ToString('yyyy-MM-dd HH:mm'))" } else { "XLINTL32 $lcid none" }
}
Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\ProductReleaseIDs' -ErrorAction SilentlyContinue | ForEach-Object {
  Get-ChildItem $_.PSPath -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -match '^culture$' } | ForEach-Object { Get-ChildItem $_.PSPath | ForEach-Object { "culture key: " + $_.PSChildName } } }
$lr = Get-ItemProperty 'HKCU:\Software\Microsoft\Office\16.0\Common\LanguageResources' -ErrorAction SilentlyContinue
"LanguageResources UILanguageTag=$($lr.UILanguageTag) FollowSystemUI=$($lr.FollowSystemUI) UISnapshotLanguages=$($lr.UISnapshotLanguages) PreviousUILanguage=$($lr.PreviousUILanguage)"
reg export "HKCU\Software\Microsoft\Office\16.0\Common\LanguageResources" "$env:LOCALAPPDATA\exgrid-layer3\pivot-en-us-2026-10-12\languageresources-$($args[0]).reg" /y | Out-Null
"LanguageResources export ($($args[0])): " + (Get-FileHash "$env:LOCALAPPDATA\exgrid-layer3\pivot-en-us-2026-10-12\languageresources-$($args[0]).reg").Hash
"excel running: " + @(Get-Process excel -ErrorAction SilentlyContinue).Count
