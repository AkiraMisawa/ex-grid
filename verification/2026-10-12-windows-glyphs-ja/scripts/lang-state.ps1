# The display language's settings, as JSON: what Windows reports, and the registry values behind them, so the state
# before the switch can be compared with the state after the switch back.
#     powershell -File lang-state.ps1 <out.json>
param([string]$Out)
$d = 'HKCU:\Control Panel\Desktop'
$val = { param($k, $n) $p = Get-ItemProperty -Path $k -Name $n -ErrorAction SilentlyContinue; if ($p) { $p.$n } else { $null } }
$profile = 'HKCU:\Control Panel\International\User Profile'
$state = [ordered]@{
    taken = (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz')
    uiLanguageOverride = (Get-WinUILanguageOverride | % { $_.Name })
    userLanguageList = @(Get-WinUserLanguageList | % { $_.LanguageTag })
    culture = (Get-Culture).Name
    uiCulture = (Get-UICulture).Name
    systemLocale = (Get-WinSystemLocale).Name
    desktop = [ordered]@{
        PreferredUILanguages = & $val $d 'PreferredUILanguages'
        PreferredUILanguagesPending = & $val $d 'PreferredUILanguagesPending'
        PreviousPreferredUILanguages = & $val $d 'PreviousPreferredUILanguages'
        MuiCached = (Get-ItemProperty -Path "$d\MuiCached" -ErrorAction SilentlyContinue | Select-Object * -ExcludeProperty PS* | ConvertTo-Json -Compress)
        LogPixels = & $val $d 'LogPixels'
        Win8DpiScaling = & $val $d 'Win8DpiScaling'
    }
    userProfile = (Get-ItemProperty -Path $profile -ErrorAction SilentlyContinue | Select-Object * -ExcludeProperty PS* | ConvertTo-Json -Compress)
    userProfileSubkeys = @(Get-ChildItem -Path $profile -ErrorAction SilentlyContinue | % { $_.PSChildName + '=' + ((Get-ItemProperty $_.PSPath | Select-Object * -ExcludeProperty PS* | ConvertTo-Json -Compress)) })
}
Add-Type -AssemblyName System.Windows.Forms
$state.messageFont = [System.Windows.Forms.SystemInformation]::MenuFont.Name + ' / ' + [System.Drawing.SystemFonts]::MessageBoxFont.Name
$state.screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Size.ToString()
$json = $state | ConvertTo-Json -Depth 4
[IO.File]::WriteAllText($Out, $json, (New-Object System.Text.UTF8Encoding $false))
$json
