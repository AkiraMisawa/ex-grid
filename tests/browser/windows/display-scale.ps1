# Reads, and with -Set <percent> sets, the display scale of every active display without a sign-out,
# through DisplayConfigSetDeviceInfo's DPI scale (the call Settings' own scale list makes).
param([int]$Set = 0)
$ErrorActionPreference = 'Stop'
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class Dpi {
  [StructLayout(LayoutKind.Sequential)] public struct LUID { public uint Low; public int High; }
  [StructLayout(LayoutKind.Sequential)] public struct Header { public int type; public int size; public LUID adapterId; public uint id; }
  [StructLayout(LayoutKind.Sequential)] public struct Get { public Header header; public int min; public int cur; public int max; }
  [StructLayout(LayoutKind.Sequential)] public struct SetS { public Header header; public int rel; }
  [StructLayout(LayoutKind.Sequential, Size = 72)] public struct Path { public LUID srcAdapter; public uint srcId; }
  [StructLayout(LayoutKind.Sequential, Size = 64)] public struct Mode { public int a; }
  [DllImport("user32.dll")] public static extern int GetDisplayConfigBufferSizes(uint flags, out uint np, out uint nm);
  [DllImport("user32.dll")] public static extern int QueryDisplayConfig(uint flags, ref uint np, [Out] Path[] p, ref uint nm, [Out] Mode[] m, IntPtr t);
  [DllImport("user32.dll")] public static extern int DisplayConfigGetDeviceInfo(ref Get g);
  [DllImport("user32.dll")] public static extern int DisplayConfigSetDeviceInfo(ref SetS s);
  [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
  public static readonly int[] Steps = { 100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500 };
  // Throws, which fails the step, when a call fails, when the scale asked for is not one Windows offers
  // this display, or when the display does not read back at it: a step that went on at the old scale
  // would run its rows at a scale they do not name.
  static void Check(string call, int code) { if (code != 0) throw new InvalidOperationException(call + " failed: " + code); }
  static Get Read(Path path) {
    var g = new Get(); g.header.type = -3; g.header.size = Marshal.SizeOf(typeof(Get)); g.header.adapterId = path.srcAdapter; g.header.id = path.srcId;
    Check("DisplayConfigGetDeviceInfo", DisplayConfigGetDeviceInfo(ref g));
    return g;
  }
  public static string Run(int want) {
    SetProcessDpiAwarenessContext(new IntPtr(-4));
    uint np, nm; Check("GetDisplayConfigBufferSizes", GetDisplayConfigBufferSizes(2, out np, out nm));
    var p = new Path[np]; var m = new Mode[nm];
    Check("QueryDisplayConfig", QueryDisplayConfig(2, ref np, p, ref nm, m, IntPtr.Zero));
    if (np == 0) throw new InvalidOperationException("no active display");
    var o = "paths=" + np + " systemDpi=" + GetDpiForSystem();
    for (int i = 0; i < np; i++) {
      var g = Read(p[i]);
      // The scale is relative to the recommended step, which sits -min steps above 100%.
      int recIdx = -g.min;
      if (recIdx < 0 || recIdx + g.max >= Steps.Length) throw new InvalidOperationException("display " + i + " offers steps " + g.min + ".." + g.max + " around its recommended one, beyond the known list");
      o += "\n path " + i + ": recommended=" + Steps[recIdx] + "% current=" + Steps[recIdx + g.cur] + "% max=" + Steps[recIdx + g.max] + "%";
      if (want > 0) {
        int idx = Array.IndexOf(Steps, want);
        if (idx < 0) throw new ArgumentException(want + "% is not a step Windows offers");
        int rel = idx - recIdx;
        if (rel < g.min || rel > g.max) throw new ArgumentException("display " + i + " offers " + Steps[recIdx + g.min] + "% to " + Steps[recIdx + g.max] + "%, not " + want + "%");
        var s = new SetS(); s.header.type = -4; s.header.size = Marshal.SizeOf(typeof(SetS)); s.header.adapterId = p[i].srcAdapter; s.header.id = p[i].srcId; s.rel = rel;
        Check("DisplayConfigSetDeviceInfo", DisplayConfigSetDeviceInfo(ref s));
        var now = Read(p[i]);
        if (now.cur != rel) throw new InvalidOperationException("display " + i + " reads back at " + Steps[recIdx + now.cur] + "% after " + want + "% was set");
        o += " set=" + want + "%";
      }
    }
    return o;
  }
}
"@
[Dpi]::Run($Set)
