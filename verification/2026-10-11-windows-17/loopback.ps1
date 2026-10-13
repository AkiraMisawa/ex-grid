# What the default playback device plays, recorded to a WAV file (WASAPI loopback), for Part F under Narrator: Narrator
# has no Speech Viewer, so what it says is taken from the sound it makes, and transcribed afterwards (part_f_heard.py).
#
#     powershell -File loopback.ps1 <out.wav> <stop-file> [max-seconds]
#
# Records until <stop-file> exists (or max-seconds pass). A loopback stream delivers nothing while nothing plays, so
# each packet is placed at its own time (its QPC position) and the gaps are written as silence: the file's timeline is
# the clock's. <out.wav>.json says when the first frame was (UTC, Windows' clock) and the format.
param([string]$Out, [string]$Stop, [int]$Max = 1800)

Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Loopback {
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice {
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        int OpenPropertyStore(int access, out IntPtr store);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioClient {
        int Initialize(int shareMode, uint streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr sessionGuid);
        int GetBufferSize(out uint frames);
        int GetStreamLatency(out long latency);
        int GetCurrentPadding(out uint frames);
        int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
        int GetMixFormat(out IntPtr format);
        int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        int Start();
        int Stop();
        int Reset();
        int SetEventHandle(IntPtr handle);
        int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioCaptureClient {
        int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devicePosition, out ulong qpcPosition);
        int ReleaseBuffer(uint frames);
        int GetNextPacketSize(out uint frames);
    }

    public static class Recorder {
        [DllImport("kernel32.dll")] static extern bool QueryPerformanceCounter(out long count);
        [DllImport("kernel32.dll")] static extern bool QueryPerformanceFrequency(out long frequency);

        static void Check(int hr, string what) { if (hr < 0) throw new Exception(what + " failed: 0x" + hr.ToString("X8")); }

        // Returns a line of JSON for the sidecar.
        public static string Record(string path, string stopFile, int maxSeconds) {
            IMMDeviceEnumerator en = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            IMMDevice dev;
            Check(en.GetDefaultAudioEndpoint(0, 0, out dev), "GetDefaultAudioEndpoint");   // eRender, eConsole
            string id; dev.GetId(out id);
            Guid iidClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
            object o;
            Check(dev.Activate(ref iidClient, 23, IntPtr.Zero, out o), "Activate");          // CLSCTX_ALL
            IAudioClient client = (IAudioClient)o;
            IntPtr fmt;
            Check(client.GetMixFormat(out fmt), "GetMixFormat");
            short cbSize = Marshal.ReadInt16(fmt, 16);
            byte[] header = new byte[18 + cbSize];
            Marshal.Copy(fmt, header, 0, header.Length);
            int channels = BitConverter.ToInt16(header, 2), rate = BitConverter.ToInt32(header, 4), blockAlign = BitConverter.ToInt16(header, 12);
            Check(client.Initialize(0, 0x00020000, 10000000, 0, fmt, IntPtr.Zero), "Initialize");   // shared, AUDCLNT_STREAMFLAGS_LOOPBACK, 1 s
            Guid iidCapture = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
            object c;
            Check(client.GetService(ref iidCapture, out c), "GetService");
            IAudioCaptureClient capture = (IAudioCaptureClient)c;

            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            BinaryWriter w = new BinaryWriter(fs);
            w.Write(new char[] { 'R', 'I', 'F', 'F' }); w.Write(0); w.Write(new char[] { 'W', 'A', 'V', 'E' });
            w.Write(new char[] { 'f', 'm', 't', ' ' }); w.Write(header.Length); w.Write(header);
            w.Write(new char[] { 'd', 'a', 't', 'a' }); long dataSizeAt = fs.Position; w.Write(0);

            long freq; QueryPerformanceFrequency(out freq);
            long q0; QueryPerformanceCounter(out q0);
            DateTime utc0 = DateTime.UtcNow;
            long start100ns = (long)(q0 * (10000000.0 / freq));
            Check(client.Start(), "Start");
            long written = 0, packets = 0, padded = 0;
            byte[] buf = new byte[0];
            DateTime until = utc0.AddSeconds(maxSeconds);
            while (!File.Exists(stopFile) && DateTime.UtcNow < until) {
                System.Threading.Thread.Sleep(10);
                uint next;
                Check(capture.GetNextPacketSize(out next), "GetNextPacketSize");
                while (next > 0) {
                    IntPtr data; uint frames, flags; ulong devPos, qpc;
                    Check(capture.GetBuffer(out data, out frames, out flags, out devPos, out qpc), "GetBuffer");
                    long at = (long)((((long)qpc) - start100ns) * (rate / 10000000.0));
                    if (at > written) {   // the gap while nothing played, as silence
                        long gap = at - written;
                        w.Write(new byte[gap * blockAlign]); written += gap; padded += gap;
                    }
                    int bytes = (int)frames * blockAlign;
                    if (buf.Length < bytes) buf = new byte[bytes];
                    if ((flags & 2) != 0) Array.Clear(buf, 0, bytes); else Marshal.Copy(data, buf, 0, bytes);   // AUDCLNT_BUFFERFLAGS_SILENT
                    w.Write(buf, 0, bytes); written += frames; packets++;
                    capture.ReleaseBuffer(frames);
                    Check(capture.GetNextPacketSize(out next), "GetNextPacketSize");
                }
            }
            // The silence after the last sound, up to the stop.
            long q1; QueryPerformanceCounter(out q1);
            long end = (long)((q1 - q0) * ((double)rate / freq));
            if (end > written) { w.Write(new byte[(end - written) * blockAlign]); padded += end - written; written = end; }
            client.Stop();
            w.Flush();
            long dataBytes = written * blockAlign;
            fs.Seek(dataSizeAt, SeekOrigin.Begin); w.Write((int)dataBytes);
            fs.Seek(4, SeekOrigin.Begin); w.Write((int)(fs.Length - 8));
            w.Close();
            Marshal.FreeCoTaskMem(fmt);
            long ms0 = (long)(utc0 - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            return "{\"startUtcMs\":" + ms0 + ",\"rate\":" + rate + ",\"channels\":" + channels + ",\"blockAlign\":" + blockAlign
                + ",\"frames\":" + written + ",\"packets\":" + packets + ",\"paddedFrames\":" + padded
                + ",\"device\":\"" + id.Replace("\\", "\\\\").Replace("\"", "'") + "\"}";
        }
    }
}
'@

$json = [Loopback.Recorder]::Record($Out, $Stop, $Max)
[IO.File]::WriteAllText($Out + '.json', $json)
$json
