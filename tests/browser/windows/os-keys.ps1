<#
.SYNOPSIS
    Keys through Windows' own input queue, as a keyboard sends them (ADR-0450), for os-input.mjs.
    A key Playwright sends reaches the page alone; one sent here reaches the browser first, so its own
    shortcuts and menu meet it, as they meet a person's. Every key goes out as a virtual-key and its
    scan code through SendInput, never as a Unicode character, so that an IME composes from it.

        powershell -NoProfile -ExecutionPolicy Bypass -File os-keys.ps1     (commands on stdin)

    keys <chord> <chord> ...   each chord pressed and released, in one SendInput call for all of
                               them: ctrl+1, alt, f10, ctrl+plus, h. A chord's keys go down in order
                               and come up in reverse.
    foreground                 the title of the window in front
    quit

    Each command is answered by one line: "ok <the title of the window in front>" or "err <why>".
#>
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Text;
public static class OsKeys {
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit, Size = 40)] struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public KEYBDINPUT ki; }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] i, int size);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    static readonly Dictionary<string, ushort> Named = new Dictionary<string, ushort> {
        { "ctrl", 0x11 }, { "shift", 0x10 }, { "alt", 0x12 }, { "space", 0x20 }, { "enter", 0x0D }, { "escape", 0x1B },
        { "tab", 0x09 }, { "backspace", 0x08 }, { "delete", 0x2E }, { "plus", 0xBB }, { "minus", 0xBD },
        { "left", 0x25 }, { "up", 0x26 }, { "right", 0x27 }, { "down", 0x28 }, { "home", 0x24 }, { "end", 0x23 },
        { "pageup", 0x21 }, { "pagedown", 0x22 }, { "imeon", 0x16 }, { "imeoff", 0x1A },
        { "f1", 0x70 }, { "f2", 0x71 }, { "f3", 0x72 }, { "f4", 0x73 }, { "f5", 0x74 }, { "f6", 0x75 },
        { "f7", 0x76 }, { "f8", 0x77 }, { "f9", 0x78 }, { "f10", 0x79 }, { "f11", 0x7A }, { "f12", 0x7B },
        // F23 and F24: keys nothing binds, which the browser hands the page; os-input.mjs sends one last
        // to learn where every key before it went.
        { "f23", 0x86 }, { "f24", 0x87 },
    };
    // The keys Windows marks extended: the arrows and the block above them.
    static readonly HashSet<ushort> Extended = new HashSet<ushort> { 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2E };
    static ushort Vk(string name) {
        ushort vk;
        if (Named.TryGetValue(name, out vk)) return vk;
        if (name.Length == 1 && char.IsLetterOrDigit(name[0])) return (ushort)char.ToUpperInvariant(name[0]);
        throw new ArgumentException("no key named " + name);
    }
    static INPUT Key(ushort vk, bool up) {
        var i = new INPUT(); i.type = 1; i.ki.wVk = vk; i.ki.wScan = (ushort)MapVirtualKey(vk, 0);
        i.ki.dwFlags = (up ? 2u : 0u) | (Extended.Contains(vk) ? 1u : 0u); return i;
    }
    public static void Send(string[] chords) {
        var events = new List<INPUT>();
        foreach (var chord in chords) {
            var vks = new List<ushort>();
            foreach (var part in chord.ToLowerInvariant().Split('+')) vks.Add(Vk(part));
            foreach (var vk in vks) events.Add(Key(vk, false));
            for (int k = vks.Count - 1; k >= 0; k--) events.Add(Key(vks[k], true));
        }
        var a = events.ToArray();
        if (SendInput((uint)a.Length, a, Marshal.SizeOf(typeof(INPUT))) != a.Length)
            throw new InvalidOperationException("SendInput sent fewer keys than asked: " + Marshal.GetLastWin32Error());
    }
    public static string Foreground() { var s = new StringBuilder(512); GetWindowText(GetForegroundWindow(), s, 512); return s.ToString(); }
}
'@
while ($null -ne ($line = [Console]::In.ReadLine())) {
    $words = $line.Trim().Split(' ', [StringSplitOptions]::RemoveEmptyEntries)
    if ($words.Length -eq 0) { continue }
    try {
        switch ($words[0]) {
            'keys' {
                if ($words.Length -lt 2) { throw 'keys names no chord' }
                [OsKeys]::Send($words[1..($words.Length - 1)])
            }
            'foreground' { }
            'quit' { exit 0 }
            default { throw "no command $($words[0])" }
        }
        [Console]::Out.WriteLine("ok $([OsKeys]::Foreground())")
    } catch {
        [Console]::Out.WriteLine("err $($_.Exception.Message -replace '\s+', ' ')")
    }
    [Console]::Out.Flush()
}
