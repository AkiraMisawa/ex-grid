using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using BoundaryBench;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
var app = builder.Build();
var sessions = new Dictionary<string, Session>();
var wwwroot = Environment.GetEnvironmentVariable("BOUNDARY_WWWROOT") ?? throw new InvalidOperationException("BOUNDARY_WWWROOT is required.");
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = new PhysicalFileProvider(wwwroot) });
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(wwwroot), ServeUnknownFileTypes = true, DefaultContentType = "application/octet-stream" });
app.MapGet("/api/prepare", (string mode, int leaves, int records) =>
{
    sessions.Clear();
    GC.Collect();
    var start = Stopwatch.GetTimestamp();
    var source = new Source(records, leaves);
    var generatedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    var id = Guid.NewGuid().ToString("N");
    sessions[id] = new Session(mode, source);
    return Results.Json(new { session = id, generatedMs, records, leaves, groups = source.Groups }, Codec.Json);
});
app.MapGet("/api/run", async (HttpContext context, string session, string action, int batch, bool visible) =>
{
    var state = sessions[session];
    var metrics = new Dictionary<string, double>();
    var allocated = GC.GetTotalAllocatedBytes();
    using var process = Process.GetCurrentProcess();
    var cpu = process.TotalProcessorTime;
    var start = Stopwatch.GetTimestamp();
    var sourceMs = 0d;
    var reportMs = 0d;
    Answer answer;
    if (action == "initial")
    {
        var leaves = state.Source.Leaves();
        sourceMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        if (state.Mode == "A") answer = new(0, true, leaves, null, null, null, leaves.Length + state.Source.Groups + 1);
        else
        {
            var reportStart = Stopwatch.GetTimestamp();
            state.Report = new Report(leaves);
            var rows = state.Report.Window();
            reportMs = Stopwatch.GetElapsedTime(reportStart).TotalMilliseconds;
            answer = new(0, true, null, null, rows, null, state.Report.RowCount);
        }
    }
    else if (action == "update")
    {
        var changes = state.Source.Apply(batch, visible);
        sourceMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        if (state.Mode == "A") answer = new(state.Source.Version, false, null, changes, null, null, state.Source.LeavesCount + state.Source.Groups + 1);
        else
        {
            var reportStart = Stopwatch.GetTimestamp();
            var before = state.Report!.Window();
            state.Report.Apply(changes);
            var after = state.Report.Window();
            var patches = after.Select((r, i) => new Patch(i, r)).Where(p => !ReferenceEquals(before[p.Index], p.Row)).ToArray();
            reportMs = Stopwatch.GetElapsedTime(reportStart).TotalMilliseconds;
            answer = new(state.Source.Version, false, null, null, null, patches, state.Report.RowCount);
        }
    }
    else
    {
        var reportStart = Stopwatch.GetTimestamp();
        if (action == "expand") state.Report!.ToggleExpand();
        else if (action == "sort") state.Report!.Sort();
        else throw new InvalidOperationException("Unknown action.");
        var rows = state.Report!.Window();
        reportMs = Stopwatch.GetElapsedTime(reportStart).TotalMilliseconds;
        answer = new(state.Source.Version, true, null, null, rows, null, state.Report.RowCount);
    }
    var serialStart = Stopwatch.GetTimestamp();
    var bytes = JsonSerializer.SerializeToUtf8Bytes(answer, BenchJsonContext.Default.Answer);
    var serializeMs = Stopwatch.GetElapsedTime(serialStart).TotalMilliseconds;
    var zipStart = Stopwatch.GetTimestamp();
    using var buffer = new MemoryStream();
    using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true)) gzip.Write(bytes);
    var compressed = buffer.ToArray();
    metrics["gzipMs"] = Stopwatch.GetElapsedTime(zipStart).TotalMilliseconds;
    metrics["sourceMs"] = sourceMs;
    metrics["reportMs"] = reportMs;
    metrics["serializeMs"] = serializeMs;
    metrics["serverElapsedMs"] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    metrics["serverProcessCpuMs"] = (process.TotalProcessorTime - cpu).TotalMilliseconds;
    metrics["serverAllocatedBytes"] = GC.GetTotalAllocatedBytes() - allocated;
    metrics["rawBytes"] = bytes.Length;
    metrics["gzipBytes"] = compressed.Length;
    foreach (var (key, value) in metrics) context.Response.Headers[$"X-Bench-{key}"] = value.ToString(CultureInfo.InvariantCulture);
    context.Response.ContentType = "application/json";
    context.Response.Headers.ContentEncoding = "gzip";
    context.Response.ContentLength = compressed.Length;
    await context.Response.Body.WriteAsync(compressed);
});
app.MapGet("/api/verify", (string session, bool collapsed, bool sorted) =>
{
    var state = sessions[session];
    var fresh = state.Source.FreshReport();
    if (state.Report != null)
    {
        fresh.MatchLayout(state.Report);
        if (fresh.FullHash() != state.Report.FullHash()) throw new InvalidOperationException("Server incremental result differs from a fresh fold.");
    }
    else
    {
        if (collapsed) fresh.ToggleExpand();
        if (sorted) fresh.Sort();
    }
    return Results.Json(new Oracle(fresh.FullHash(), Codec.Hash(fresh.Window()), fresh.RowCount), Codec.Json);
});
app.MapGet("/api/memory", () =>
{
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    return Results.Json(new { managedLiveBytes = GC.GetTotalMemory(false), managedHeapBytes = GC.GetGCMemoryInfo().HeapSizeBytes });
});
app.Run();

sealed class Session(string mode, Source source)
{
    public string Mode { get; } = mode;
    public Source Source { get; } = source;
    public Report? Report { get; set; }
}
