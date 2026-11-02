# Reads, and with -Set <percent> sets, the display scale of every active display without a sign-out,
# through DisplayConfigSetDeviceInfo's DPI scale (the call Settings' own scale list makes).
param([int]$Set = 0)
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
  public static string Run(int want) {
    SetProcessDpiAwarenessContext(new IntPtr(-4));
    uint np, nm; GetDisplayConfigBufferSizes(2, out np, out nm);
    var p = new Path[np]; var m = new Mode[nm];
    int q = QueryDisplayConfig(2, ref np, p, ref nm, m, IntPtr.Zero);
    var o = "query=" + q + " paths=" + np + " systemDpi=" + GetDpiForSystem();
    for (int i = 0; i < np; i++) {
      var g = new Get(); g.header.type = -3; g.header.size = Marshal.SizeOf(typeof(Get)); g.header.adapterId = p[i].srcAdapter; g.header.id = p[i].srcId;
      int r = DisplayConfigGetDeviceInfo(ref g);
      int recommended = Array.IndexOf(Steps, 100) - g.min; // index of the recommended step
      o += "\n path " + i + ": get=" + r + " minRel=" + g.min + " curRel=" + g.cur + " maxRel=" + g.max;
      int recIdx = Math.Abs(g.min);
      o += " recommended=" + Steps[recIdx] + "% current=" + Steps[recIdx + g.cur] + "% max=" + Steps[recIdx + g.max] + "%";
      if (want > 0) {
        int idx = Array.IndexOf(Steps, want);
        var s = new SetS(); s.header.type = -4; s.header.size = Marshal.SizeOf(typeof(SetS)); s.header.adapterId = p[i].srcAdapter; s.header.id = p[i].srcId; s.rel = idx - recIdx;
        o += " set(" + want + "%, rel " + s.rel + ")=" + DisplayConfigSetDeviceInfo(ref s);
      }
    }
    return o;
  }
}
"@
[Dpi]::Run($Set)
