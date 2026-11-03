<#
.SYNOPSIS
    Keys and presses through Windows' own input queue, as a keyboard and a mouse send them (ADR-0450),
    for os-input.mjs. A key Playwright sends reaches the page alone; one sent here reaches the browser
    first, so its own shortcuts and menu meet it, as they meet a person's, and so does the IME. Every key
    goes out as a virtual-key and its scan code through SendInput, never as a Unicode character, so that
    an IME composes from it. The process is DPI-aware, so a point is the screen's physical pixels.

        powershell -NoProfile -ExecutionPolicy Bypass -File os-keys.ps1     (commands on stdin)

    keys <chord> <chord> ...   each chord pressed and released, in one SendInput call for all of
                               them: ctrl+1, alt, f10, ctrl+plus, h. A chord's keys go down in order
                               and come up in reverse.
    press <x> <y>              the pointer put at that screen pixel, then a left press and release,
                               in one SendInput call
    move <x> <y>               the pointer put at that screen pixel
    japanese                   the window in front to the Japanese keyboard and its IME (ED-30): asked
                               with WM_INPUTLANGCHANGEREQUEST, and answered once the window's thread
                               has taken it, or refused after five seconds
    foreground                 the title of the window in front
    quit

    Each command is answered by one line: "ok <the title of the window in front>" or "err <why>".
#>
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Text;
public static class OsKeys {
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit, Size = 40)] struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public KEYBDINPUT ki; [FieldOffset(8)] public MOUSEINPUT mi; }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] i, int size);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint message, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint process);
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint thread);
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
        Out(events.ToArray());
    }
    static void Out(INPUT[] a) {
        if (SendInput((uint)a.Length, a, Marshal.SizeOf(typeof(INPUT))) != a.Length)
            throw new InvalidOperationException("SendInput sent fewer events than asked: " + Marshal.GetLastWin32Error());
    }
    // Its answer is not read: a helper whose points were scaled would fail os-input.mjs's calibration, which
    // reads back where each point landed in the page.
    public static void DpiAware() { SetProcessDPIAware(); }
    public static void Move(int x, int y) { if (!SetCursorPos(x, y)) throw new InvalidOperationException("the pointer could not be put at " + x + ", " + y); }
    // The left button pressed and released where the pointer is (MOUSEEVENTF_LEFTDOWN 0x2, LEFTUP 0x4).
    public static void Press(int x, int y) {
        Move(x, y);
        var down = new INPUT(); down.type = 0; down.mi.dwFlags = 2;
        var up = new INPUT(); up.type = 0; up.mi.dwFlags = 4;
        Out(new[] { down, up });
    }
    static uint Layout(IntPtr window) { uint process; return (uint)(long)GetKeyboardLayout(GetWindowThreadProcessId(window, out process)); }
    // The Japanese keyboard with the Microsoft IME is 0x04110411; its language is the low word, 0x0411.
    public static string Japanese() {
        var window = GetForegroundWindow();
        if (!PostMessage(window, 0x0050, IntPtr.Zero, (IntPtr)0x04110411)) throw new InvalidOperationException("WM_INPUTLANGCHANGEREQUEST could not be posted: " + Marshal.GetLastWin32Error());
        var until = DateTime.UtcNow.AddSeconds(5);
        while ((Layout(window) & 0xFFFF) != 0x0411) {
            if (DateTime.UtcNow > until) throw new InvalidOperationException("the window in front kept the keyboard 0x" + Layout(window).ToString("x8") + ": is Japanese installed?");
            System.Threading.Thread.Sleep(20);
        }
        return "0x" + Layout(window).ToString("x8");
    }
    public static string Foreground() { var s = new StringBuilder(512); GetWindowText(GetForegroundWindow(), s, 512); return s.ToString(); }
}
'@
[OsKeys]::DpiAware()
while ($null -ne ($line = [Console]::In.ReadLine())) {
    $words = $line.Trim().Split(' ', [StringSplitOptions]::RemoveEmptyEntries)
    if ($words.Length -eq 0) { continue }
    try {
        switch ($words[0]) {
            'keys' {
                if ($words.Length -lt 2) { throw 'keys names no chord' }
                [OsKeys]::Send($words[1..($words.Length - 1)])
            }
            'press' {
                if ($words.Length -ne 3) { throw 'press names no point' }
                [OsKeys]::Press([int]$words[1], [int]$words[2])
            }
            'move' {
                if ($words.Length -ne 3) { throw 'move names no point' }
                [OsKeys]::Move([int]$words[1], [int]$words[2])
            }
            'japanese' { [void][OsKeys]::Japanese() }
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
