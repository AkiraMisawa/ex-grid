using System.Diagnostics.Tracing;

namespace Costs;

/// <summary>One collection as the runtime's own GC events describe it.</summary>
public sealed class GcRecord
{
    public long Index { get; set; }
    public int Generation { get; set; }
    /// <summary>0 blocking (non-concurrent), 1 background, 2 foreground (during a background GC).</summary>
    public int Type { get; set; }
    public int Reason { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    /// <summary>GCStart to GCEnd, ms.</summary>
    public double DurationMs { get; set; }
    /// <summary>The suspension that holds this GC: SuspendEEBegin before it to RestartEEEnd after it,
    /// ms; for a background GC, only the suspensions inside its span, summed.</summary>
    public double PauseMs { get; set; }
    /// <summary>GCGlobalHeapHistory's GlobalMechanisms: 0x1 concurrent, 0x2 compaction, 0x4
    /// promotion, 0x8 demotion, 0x10 card bundles, 0x20 elevation.</summary>
    public long GlobalMechanisms { get; set; } = -1;
    public bool? Compacting => GlobalMechanisms < 0 ? null : (GlobalMechanisms & 0x2) != 0;
    public long? FreeListAllocated { get; set; }
    public long? FreeListRejected { get; set; }
    public long? EndOfSegAllocated { get; set; }
    public long? CondemnedAllocated { get; set; }
    public long? PinnedAllocated { get; set; }
    public long? CompactMechanisms { get; set; }
    public long? ExpandMechanisms { get; set; }
    public long[] PromotedBytes { get; set; } = [];
    public long[] GenerationSizes { get; set; } = [];
}

/// <summary>
/// Listens to the runtime's GC events in-process (Microsoft-Windows-DotNETRuntime, keyword GC,
/// Informational): every collection's generation, type, reason, duration, pause, mechanisms
/// (compacting or sweeping), how its survivors were placed (free list or end of segment) and what
/// it promoted. Events arrive on the listener's own thread, a little after the fact; they are
/// paired with a redraw by GC index afterwards.
/// </summary>
public sealed class GcRecorder : EventListener
{
    private readonly object _lock = new();
    private readonly Dictionary<long, GcRecord> _gcs = [];
    private readonly List<(DateTime At, bool Begin)> _suspensions = [];
    private long _current = -1;

    /// <summary>How many of each event arrived (a check that the listener sees what it reads).</summary>
    public Dictionary<string, int> EventCounts { get; } = [];

    /// <summary>The first GCPerHeapHistory's payload, as it arrived.</summary>
    public string? FirstPerHeap { get; private set; }

    /// <summary>The level the runtime's GC events are enabled at: Verbose carries GCPerHeapHistory
    /// (how survivors were placed), Informational does not. Static, because OnEventSourceCreated
    /// runs from the base constructor, before a field of this one is set.</summary>
    public static EventLevel Level { get; set; } = EventLevel.Verbose;

    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name == "Microsoft-Windows-DotNETRuntime")
            EnableEvents(source, Level, (EventKeywords)0x1);
    }

    private static long Long(EventWrittenEventArgs e, string name)
    {
        var at = e.PayloadNames?.IndexOf(name) ?? -1;
        // Pointer-sized fields (FreeListAllocated and its kin) arrive as IntPtr/UIntPtr, which
        // Convert does not take; an exception here would be swallowed by EventListener.
        return at < 0 ? -1 : e.Payload![at] switch
        {
            null => -1,
            IntPtr p => (long)p,
            UIntPtr u => (long)(ulong)u,
            var v => Convert.ToInt64(v, System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        var name = e.EventName ?? "";
        lock (_lock)
        {
            EventCounts[name] = EventCounts.GetValueOrDefault(name) + 1;
            if (name.StartsWith("GCPerHeapHistory", StringComparison.Ordinal) && FirstPerHeap is null)
                FirstPerHeap = string.Join(",", (e.PayloadNames ?? []).Zip(e.Payload ?? [], (n, v) => $"{n}={v}"));
            if (name.StartsWith("GCStart", StringComparison.Ordinal))
            {
                var index = Long(e, "Count");
                _current = index;
                _gcs[index] = new GcRecord
                {
                    Index = index,
                    Generation = (int)Long(e, "Depth"),
                    Type = (int)Long(e, "Type"),
                    Reason = (int)Long(e, "Reason"),
                    Start = e.TimeStamp,
                };
            }
            else if (name.StartsWith("GCEnd", StringComparison.Ordinal))
            {
                if (_gcs.TryGetValue(Long(e, "Count"), out var gc))
                {
                    gc.End = e.TimeStamp;
                    gc.DurationMs = (gc.End - gc.Start).TotalMilliseconds;
                }
            }
            else if (name.StartsWith("GCSuspendEEBegin", StringComparison.Ordinal))
            {
                _suspensions.Add((e.TimeStamp, true));
            }
            else if (name.StartsWith("GCRestartEEEnd", StringComparison.Ordinal))
            {
                _suspensions.Add((e.TimeStamp, false));
            }
            else if (name.StartsWith("GCGlobalHeapHistory", StringComparison.Ordinal))
            {
                if (_gcs.TryGetValue(_current, out var gc))
                    gc.GlobalMechanisms = Long(e, "GlobalMechanisms");
            }
            else if (name.StartsWith("GCPerHeapHistory", StringComparison.Ordinal))
            {
                if (_gcs.TryGetValue(_current, out var gc))
                {
                    gc.FreeListAllocated = Long(e, "FreeListAllocated");
                    gc.FreeListRejected = Long(e, "FreeListRejected");
                    gc.EndOfSegAllocated = Long(e, "EndOfSegAllocated");
                    gc.CondemnedAllocated = Long(e, "CondemnedAllocated");
                    gc.PinnedAllocated = Long(e, "PinnedAllocated");
                    gc.CompactMechanisms = Long(e, "CompactMechanisms");
                    gc.ExpandMechanisms = Long(e, "ExpandMechanisms");
                }
            }
            else if (name.StartsWith("GCHeapStats", StringComparison.Ordinal))
            {
                if (_gcs.TryGetValue(_current, out var gc))
                {
                    gc.PromotedBytes = [Long(e, "TotalPromotedSize0"), Long(e, "TotalPromotedSize1"), Long(e, "TotalPromotedSize2"), Long(e, "TotalPromotedSize3"), Long(e, "TotalPromotedSize4")];
                    gc.GenerationSizes = [Long(e, "GenerationSize0"), Long(e, "GenerationSize1"), Long(e, "GenerationSize2"), Long(e, "GenerationSize3"), Long(e, "GenerationSize4")];
                }
            }
        }
    }

    /// <summary>Waits until the events of collection <paramref name="index"/> have arrived (or a
    /// second has passed).</summary>
    public async Task WaitForAsync(long index)
    {
        for (var i = 0; i < 100; i++)
        {
            lock (_lock)
            {
                if (_gcs.TryGetValue(index, out var gc) && gc.End != default && gc.PromotedBytes.Length > 0)
                    return;
            }
            await Task.Delay(10);
        }
    }

    /// <summary>The collections with index in (<paramref name="after"/>, <paramref name="upTo"/>],
    /// their pauses computed from the suspensions around them.</summary>
    public List<GcRecord> Between(long after, long upTo)
    {
        lock (_lock)
        {
            var list = _gcs.Values.Where(g => g.Index > after && g.Index <= upTo).OrderBy(g => g.Index).ToList();
            foreach (var gc in list)
            {
                if (gc.End == default)
                    continue;
                if (gc.Type == 1)
                {
                    // A background GC: the suspensions inside its span.
                    double pause = 0;
                    DateTime? begun = null;
                    foreach (var (at, begin) in _suspensions.Where(s => s.At >= gc.Start.AddMilliseconds(-50) && s.At <= gc.End.AddMilliseconds(50)))
                    {
                        if (begin)
                            begun = at;
                        else if (begun is { } b)
                        {
                            pause += (at - b).TotalMilliseconds;
                            begun = null;
                        }
                    }
                    gc.PauseMs = pause;
                }
                else
                {
                    var begin = _suspensions.Where(s => s.Begin && s.At <= gc.Start).Select(s => s.At).DefaultIfEmpty(gc.Start).Max();
                    var end = _suspensions.Where(s => !s.Begin && s.At >= gc.End).Select(s => s.At).DefaultIfEmpty(gc.End).Min();
                    gc.PauseMs = (end - begin).TotalMilliseconds;
                }
            }
            return list;
        }
    }
}
