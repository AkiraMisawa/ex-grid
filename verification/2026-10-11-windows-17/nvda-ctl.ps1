<#
.SYNOPSIS
    verify-on-windows-17.md, Part F: NVDA (C:\Program Files\NVDA, 2026.2) started, read and stopped, for the probe.

        powershell -File nvda-ctl.ps1 start          NVDA started; its windows listed
        powershell -File nvda-ctl.ps1 windows        every visible top-level window of NVDA's process: handle,
                                                     class, title, place
        powershell -File nvda-ctl.ps1 speech         the Speech Viewer's text, read with WM_GETTEXT from its own
                                                     edit control (no focus moves, no key is sent), as JSON
        powershell -File nvda-ctl.ps1 quit           NVDA asked to quit through its own command line (nvda -q)
        powershell -File nvda-ctl.ps1 running        whether NVDA runs

    The Speech Viewer itself is opened with NVDA's own keys by the probe (input-server.ps1), not here.
#>
param([string]$Cmd)
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System; using System.Collections.Generic; using System.Diagnostics; using System.Runtime.InteropServices; using System.Text;
namespace NvdaCtl {
public static class W {
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr p, EnumProc f, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, StringBuilder l, uint flags, uint ms, out IntPtr result);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint ms, out IntPtr result);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    public static List<string> Of(HashSet<int> pids) {
        var o = new List<string>();
        EnumWindows((h, l) => {
            if (!IsWindowVisible(h)) return true;
            uint p; GetWindowThreadProcessId(h, out p); if (!pids.Contains((int)p)) return true;
            var c = new StringBuilder(256); GetClassName(h, c, 256); var t = new StringBuilder(256); GetWindowText(h, t, 256); RECT r; GetWindowRect(h, out r);
            o.Add(string.Format("{0}|{1}|{2}|{3},{4},{5},{6}", (long)h, c, t, r.Left, r.Top, r.Right, r.Bottom));
            return true;
        }, IntPtr.Zero);
        return o;
    }
    // The text of every Edit child of the windows titled like the Speech Viewer.
    public static string Speech(HashSet<int> pids) {
        var sb = new StringBuilder();
        EnumWindows((h, l) => {
            uint p; GetWindowThreadProcessId(h, out p); if (!pids.Contains((int)p)) return true;
            var t = new StringBuilder(256); GetWindowText(h, t, 256);
            if (t.ToString().IndexOf("Speech Viewer", StringComparison.OrdinalIgnoreCase) < 0) return true;
            EnumChildWindows(h, (c, l2) => {
                var cls = new StringBuilder(256); GetClassName(c, cls, 256);
                if (cls.ToString().IndexOf("Edit", StringComparison.OrdinalIgnoreCase) < 0) return true;
                IntPtr len; SendMessageTimeout(c, 0x000E, IntPtr.Zero, IntPtr.Zero, 2, 1500, out len);   // WM_GETTEXTLENGTH
                var buf = new StringBuilder((int)len + 1); IntPtr got;
                SendMessageTimeout(c, 0x000D, new IntPtr((int)len + 1), buf, 2, 1500, out got);          // WM_GETTEXT
                sb.Append(buf.ToString());
                return true;
            }, IntPtr.Zero);
            return true;
        }, IntPtr.Zero);
        return sb.ToString();
    }
}
}
'@
$exe = 'C:\Program Files\NVDA\nvda.exe'
function Pids { $s = New-Object 'System.Collections.Generic.HashSet[int]'; Get-Process nvda* -ErrorAction SilentlyContinue | ForEach-Object { [void]$s.Add($_.Id) }; return , $s }
switch ($Cmd) {
    'start' {
        if ((Pids).Count -eq 0) { Start-Process $exe; $t0 = Get-Date; while ((Pids).Count -eq 0 -and ((Get-Date) - $t0).TotalSeconds -lt 15) { Start-Sleep -Milliseconds 300 } }
        Start-Sleep -Seconds 4
        'running: ' + ((Get-Process nvda* -ErrorAction SilentlyContinue | ForEach-Object { "$($_.ProcessName) $($_.Id)" }) -join ', ')
        [NvdaCtl.W]::Of((Pids)) | ForEach-Object { "window $_" }
    }
    'windows' { [NvdaCtl.W]::Of((Pids)) | ForEach-Object { "window $_" } }
    'speech' { ConvertTo-Json -InputObject ([NvdaCtl.W]::Speech((Pids))) -Compress }
    'quit' {
        if ((Pids).Count -gt 0) { & $exe -q; $t0 = Get-Date; while ((Pids).Count -gt 0 -and ((Get-Date) - $t0).TotalSeconds -lt 20) { Start-Sleep -Milliseconds 500 } }
        'running after quit: ' + ((Pids).Count -gt 0)
    }
    'running' { 'running: ' + ((Pids).Count -gt 0) }
}
