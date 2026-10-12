Add-Type -Name K -Namespace N -MemberDefinition @'
[DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint t);
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
'@
Get-Process excel -ErrorAction SilentlyContinue | % { "EXCEL $($_.Id) $($_.StartTime) [$($_.MainWindowTitle)]" }
$p = 0; $t = [N.K]::GetWindowThreadProcessId([N.K]::GetForegroundWindow(), [ref]$p)
"front layout 0x{0:x8} process {1}" -f [long][N.K]::GetKeyboardLayout($t), (Get-Process -Id $p).ProcessName
"culture " + (Get-Culture).Name
Get-Process nvda, Narrator -ErrorAction SilentlyContinue | % { "$($_.ProcessName) $($_.Id)" }
