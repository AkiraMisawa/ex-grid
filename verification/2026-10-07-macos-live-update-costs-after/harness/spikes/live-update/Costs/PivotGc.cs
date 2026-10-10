using System.Diagnostics;
using System.Runtime;

namespace Costs;

/// <summary>
/// The collector inside ExPivot's live redraw, by the paint-text record's method
/// (verification/2026-10-06-macos-paint-text-cost, "Follow-up"), ported unchanged in what it reads:
/// <list type="bullet">
/// <item><c>pivot-gc</c>: one redraw at a time (one record amended, past the redraw interval, slicing
/// off, so it runs inline), a collection before each — full, as the rig makes it, compacting, or none
/// — with every collection inside the redraw read from the runtime's GC events, and the GC's own
/// memory info before and after.</item>
/// <item><c>pivot-steady</c>: consecutive live redraws with no forced collection at all (H3), each
/// timed, with every collection inside it and the heap after it.</item>
/// </list>
/// ExPivot is scrolled to report row 1,000, as the paint-text record's runs were.
/// </summary>
public static class PivotGc
{
    private static void PreCollect(string how)
    {
        switch (how)
        {
            case "full":
                Clock.Collect();
                break;
            case "compact":
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                break;
            case "none":
                break;
            default:
                throw new ArgumentException($"no pre-collection {how}");
        }
    }

    private static object Info(GCMemoryInfo info) => new
    {
        index = info.Index,
        generation = info.Generation,
        compacted = info.Compacted,
        concurrent = info.Concurrent,
        heapSizeMB = Math.Round(info.HeapSizeBytes / 1048576.0, 1),
        committedMB = Math.Round(info.TotalCommittedBytes / 1048576.0, 1),
        fragmentedMB = Math.Round(info.FragmentedBytes / 1048576.0, 1),
        promotedMB = Math.Round(info.PromotedBytes / 1048576.0, 1),
        pauseMs = info.PauseDurations.ToArray().Select(p => Math.Round(p.TotalMilliseconds, 3)).ToArray(),
        generations = info.GenerationInfo.ToArray().Select(g => new
        {
            sizeBeforeMB = Math.Round(g.SizeBeforeBytes / 1048576.0, 1),
            fragmentationBeforeMB = Math.Round(g.FragmentationBeforeBytes / 1048576.0, 1),
            sizeAfterMB = Math.Round(g.SizeAfterBytes / 1048576.0, 1),
            fragmentationAfterMB = Math.Round(g.FragmentationAfterBytes / 1048576.0, 1),
        }).ToArray(),
    };

    private static object Gc(GcRecord g) => new
    {
        g.Index, g.Generation, type = g.Type switch { 0 => "blocking", 1 => "background", 2 => "foreground", _ => g.Type.ToString() },
        g.Reason, durationMs = Math.Round(g.DurationMs, 3), pauseMs = Math.Round(g.PauseMs, 3), g.Compacting, g.GlobalMechanisms,
        freeListAllocatedMB = g.FreeListAllocated is { } f && f >= 0 ? Math.Round(f / 1048576.0, 2) : (double?)null,
        endOfSegAllocatedMB = g.EndOfSegAllocated is { } e && e >= 0 ? Math.Round(e / 1048576.0, 2) : (double?)null,
        condemnedAllocatedMB = g.CondemnedAllocated is { } c && c >= 0 ? Math.Round(c / 1048576.0, 2) : (double?)null,
        g.FreeListRejected, g.PinnedAllocated, g.CompactMechanisms, g.ExpandMechanisms,
        promotedMB = g.PromotedBytes.Select(p => Math.Round(p / 1048576.0, 2)).ToArray(),
        generationSizesMB = g.GenerationSizes.Select(p => Math.Round(p / 1048576.0, 1)).ToArray(),
    };

    public static async Task<object> RunAsync(int a, int b, int warmup, int runs, string precollect, string listen)
    {
        GcRecorder.Level = listen == "info" ? System.Diagnostics.Tracing.EventLevel.Informational : System.Diagnostics.Tracing.EventLevel.Verbose;
        using var recorder = listen == "off" ? null : new GcRecorder();
        var (renderer, pivot, source, newest, clock, _) = await PivotCosts.StartAsync(a, b, scrollToRow: 1_000);
        var random = new Random(7);
        var rows = new List<Dictionary<string, object?>>();
        Console.WriteLine($"== pivot-gc {a:N0} x {b:N0}, pre-collection {precollect}, GC events {listen}, GCRetainVM={Environment.GetEnvironmentVariable("DOTNET_GCRetainVM") ?? "(default)"} ==");
        for (var r = 0; r < warmup + runs; r++)
        {
            var amended = PivotCosts.Amend(newest, 1, random);
            await renderer.Dispatcher.InvokeAsync(() => clock.Advance(TimeSpan.FromMilliseconds(400)));
            var preIndex = GC.GetGCMemoryInfo(GCKind.Any).Index;
            PreCollect(precollect);
            var before = GC.GetGCMemoryInfo(GCKind.Any);
            var index0 = before.Index;
            var workingSet0 = Environment.WorkingSet;
            double ms = 0, pause = 0, allocated = 0;
            await renderer.Dispatcher.InvokeAsync(() =>
            {
                var p0 = GC.GetTotalPauseDuration();
                var b0 = GC.GetTotalAllocatedBytes(precise: false);
                var t0 = Stopwatch.GetTimestamp();
                var report = pivot.Report;
                source.Apply(PivotCosts.Fields.Batch(changed: amended));
                ms = Clock.Ms(t0);
                pause = (GC.GetTotalPauseDuration() - p0).TotalMilliseconds;
                allocated = (GC.GetTotalAllocatedBytes(precise: false) - b0) / 1048576.0;
                if (ReferenceEquals(report, pivot.Report))
                    throw new InvalidOperationException("The redraw did not run inline.");
            });
            var after = GC.GetGCMemoryInfo(GCKind.Any);
            var ephemeral = GC.GetGCMemoryInfo(GCKind.Ephemeral);
            if (r < warmup)
                continue;
            rows.Add(new()
            {
                ["run"] = r - warmup + 1,
                ["redrawMs"] = Math.Round(ms, 3),
                ["gcPauseMs"] = Math.Round(pause, 3),
                ["allocatedMB"] = Math.Round(allocated, 2),
                ["preIndex"] = preIndex,
                ["index0"] = index0,
                ["index1"] = after.Index,
                ["workingSetBeforeMB"] = Math.Round(workingSet0 / 1048576.0, 1),
                ["workingSetAfterMB"] = Math.Round(Environment.WorkingSet / 1048576.0, 1),
                ["afterPreCollection"] = Info(before),
                ["lastEphemeralInRedraw"] = ephemeral.Index > index0 ? Info(ephemeral) : null,
                ["lastInRedraw"] = after.Index > index0 ? Info(after) : null,
            });
            Console.WriteLine($"   run {r - warmup + 1,2}: {ms,8:F2} ms, GC pause {pause,7:F2} ms, GCs {after.Index - index0}, allocated {allocated,6:F1} MB, committed {before.TotalCommittedBytes / 1048576.0:F0} -> {after.TotalCommittedBytes / 1048576.0:F0} MB");
        }
        if (recorder is not null)
        {
            await recorder.WaitForAsync(GC.GetGCMemoryInfo(GCKind.Any).Index);
            foreach (var row in rows)
            {
                row["collections"] = recorder.Between((long)row["index0"]!, (long)row["index1"]!).Select(Gc).ToArray();
                row["preCollections"] = recorder.Between((long)row["preIndex"]!, (long)row["index0"]!).Select(Gc).ToArray();
            }
        }
        var times = rows.Select(r => (double)r["redrawMs"]!).ToArray();
        var pauses = rows.Select(r => (double)r["gcPauseMs"]!).ToArray();
        var allocations = rows.Select(r => (double)r["allocatedMB"]!).ToArray();
        Console.WriteLine($"   redraw {Stat.Of(times)}   GC pause {Stat.Of(pauses)}   allocated MB {Stat.Of(allocations)}");
        await renderer.DisposeAsync();
        return new
        {
            a, b, warmup, runs, precollect, listen,
            events = recorder?.EventCounts, firstPerHeapHistory = recorder?.FirstPerHeap,
            gcRetainVM = Environment.GetEnvironmentVariable("DOTNET_GCRetainVM"),
            gcSettings = new { GCSettings.IsServerGC, latency = GCSettings.LatencyMode.ToString() },
            redrawMs = Stat.Of(times), gcPauseMs = Stat.Of(pauses), allocatedMB = Stat.Of(allocations), rows,
        };
    }

    public static async Task<object> SteadyAsync(int a, int b, int redraws)
    {
        GcRecorder.Level = System.Diagnostics.Tracing.EventLevel.Verbose;
        using var recorder = new GcRecorder();
        var (renderer, pivot, source, newest, clock, _) = await PivotCosts.StartAsync(a, b, scrollToRow: 1_000);
        var random = new Random(7);
        var rows = new List<Dictionary<string, object?>>();
        Console.WriteLine($"== pivot-steady {a:N0} x {b:N0}, {redraws} redraws, no forced collection ==");
        for (var r = 0; r < redraws; r++)
        {
            var amended = PivotCosts.Amend(newest, 1, random);
            await renderer.Dispatcher.InvokeAsync(() => clock.Advance(TimeSpan.FromMilliseconds(400)));
            var index0 = GC.GetGCMemoryInfo(GCKind.Any).Index;
            double ms = 0, pause = 0;
            var madeFrom = false;
            await renderer.Dispatcher.InvokeAsync(() =>
            {
                var p0 = GC.GetTotalPauseDuration();
                var t0 = Stopwatch.GetTimestamp();
                var report = pivot.Report;
                source.Apply(PivotCosts.Fields.Batch(changed: amended));
                ms = Clock.Ms(t0);
                pause = (GC.GetTotalPauseDuration() - p0).TotalMilliseconds;
                if (ReferenceEquals(report, pivot.Report))
                    throw new InvalidOperationException("The redraw did not run inline.");
                madeFrom = PivotCosts.WasMadeFrom(pivot.Report!, report!);
            });
            var after = GC.GetGCMemoryInfo(GCKind.Any);
            rows.Add(new()
            {
                ["redraw"] = r + 1,
                ["madeFrom"] = madeFrom,
                ["redrawMs"] = Math.Round(ms, 3),
                ["gcPauseMs"] = Math.Round(pause, 3),
                ["index0"] = index0,
                ["index1"] = after.Index,
                ["allocatedNowMB"] = Math.Round(GC.GetTotalMemory(false) / 1048576.0, 1),
                ["workingSetMB"] = Math.Round(Environment.WorkingSet / 1048576.0, 1),
                ["lastGc"] = Info(after),
            });
            Console.WriteLine($"   redraw {r + 1,3}: {ms,8:F2} ms, GC pause {pause,7:F2} ms, GCs {after.Index - index0}, heap after last GC {after.HeapSizeBytes / 1048576.0:F0} MB, committed {after.TotalCommittedBytes / 1048576.0:F0} MB, made from the last {madeFrom}");
        }
        await recorder.WaitForAsync(GC.GetGCMemoryInfo(GCKind.Any).Index);
        foreach (var row in rows)
            row["collections"] = recorder.Between((long)row["index0"]!, (long)row["index1"]!).Select(Gc).ToArray();
        var times = rows.Select(r => (double)r["redrawMs"]!).ToArray();
        await renderer.DisposeAsync();
        return new
        {
            a, b, redraws,
            events = recorder.EventCounts, firstPerHeapHistory = recorder.FirstPerHeap,
            gcSettings = new { GCSettings.IsServerGC, latency = GCSettings.LatencyMode.ToString() },
            redrawMs = Stat.Of(times), rows,
        };
    }
}
