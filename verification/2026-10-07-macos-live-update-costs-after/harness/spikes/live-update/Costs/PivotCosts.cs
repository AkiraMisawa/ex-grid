using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using ExGrid;
using ExGrid.Components;
using ExPivot;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Time.Testing;
using PivotComponent = ExPivot.Components.ExPivot;

namespace Costs;

/// <summary>A record of D10's report: two text fields, A00000… × B0000…, and a number.</summary>
public sealed record Cell(string Id, string A, string B, decimal Value);

/// <summary>
/// Ticket 13: ticket 01's ExPivot measurement repeated after ADR-0161 (the engine makes the next cube
/// and report from the last), ADR-0160 (the grid holds no row beyond its Window) and ADR-0141's vouch.
/// D10's report (two row fields, A × B with A's subtotals, 1,000 × B leaves), step by step as
/// src/ExPivot/Components/ExPivot.Asking.cs now runs a live redraw: the bundled source folding the
/// batch (SnapshotPivotSource.Apply) and answering with the leaves it touched (AggregateAsync, a
/// question naming the version on screen); PivotEngine.NextCubeAsync; PivotEngine.NextReportAsync;
/// HasSameRowsAsAsync; ChangesSinceAsync (what the Change Highlight records); LabelWidthsAsync,
/// which a report made from the one on screen skips (WasMadeFrom); Show, with the history's Record;
/// the grid's take-in (vouched: no walk) and the render, which checks the painted rows' keys. Each
/// step alone on the same inputs, the least of the runs, and the whole redraw as ExPivot runs it
/// (slicing off, so it runs inline), with the collector's pauses and collections inside it. Beside
/// them, on the same inputs, the fresh build (CubeAsync, ReportAsync) the redraw ran before.
/// </summary>
public static class PivotCosts
{
    private static readonly PivotSlicing Never = new() { Budget = TimeSpan.FromDays(1) };

    public static readonly PivotFields<Cell> Fields = PivotFields.Of<Cell>()
        .Key("Id", c => c.Id)
        .Text("A", c => c.A)
        .Text("B", c => c.B)
        .Number("Value", c => c.Value, format: "#,##0.00");

    private static readonly PivotLayout Layout = new()
    {
        Rows = [new PivotFieldPlacement("A"), new PivotFieldPlacement("B")],
        Values = [new PivotValueField("Value", PivotAggregation.Sum) { NumberFormat = "#,##0.00" }],
    };

    // The engine's internal steps (ExPivot reaches them through InternalsVisibleTo; this harness by name).
    private static readonly MethodInfo NextCubeMethod = Reflect.Method(typeof(PivotEngine), "NextCube");
    private static readonly MethodInfo NextReportMethod = Reflect.Method(typeof(PivotEngine), "NextReport");
    private static readonly MethodInfo WasMadeFromMethod = Reflect.Method(typeof(PivotReport), "WasMadeFrom");
    private static readonly MethodInfo ChangesSinceSync = typeof(PivotReport)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Single(m => m.Name == "ChangesSince" && m.GetParameters().Length == 1);

    public static PivotCube NextCube(PivotCube previous, PivotQuery query, PivotAnswer answer, IReadOnlyList<PivotField> fields)
        => (PivotCube)NextCubeMethod.Invoke(null, [previous, query, answer, fields])!;

    public static PivotReport NextReport(PivotReport previous, PivotCube cube, PivotOptions options)
        => (PivotReport)NextReportMethod.Invoke(null, [previous, cube, Layout, options])!;

    public static bool WasMadeFrom(PivotReport report, PivotReport earlier) => (bool)WasMadeFromMethod.Invoke(report, [earlier])!;

    public static object ChangesSince(PivotReport report, PivotReport earlier) => ChangesSinceSync.Invoke(report, [earlier])!;

    public static Cell[] Make(int aCount, int bCount)
    {
        var random = new Random(20261006);
        var cells = new Cell[aCount * bCount];
        for (var a = 0; a < aCount; a++)
        {
            for (var b = 0; b < bCount; b++)
            {
                var i = (a * bCount) + b;
                cells[i] = new Cell(
                    "R" + i.ToString(CultureInfo.InvariantCulture),
                    "A" + a.ToString("00000", CultureInfo.InvariantCulture),
                    "B" + b.ToString("0000", CultureInfo.InvariantCulture),
                    Math.Round((decimal)((random.NextDouble() - 0.45) * 100_000), 2));
            }
        }
        return cells;
    }

    /// <summary>k records, each picked once, uniformly, their value moved (as /pivot-live amends).</summary>
    public static Cell[] Amend(Cell[] cells, int count, Random random)
    {
        var picked = new HashSet<int>();
        while (picked.Count < count)
            picked.Add(random.Next(cells.Length));
        var amended = new Cell[picked.Count];
        var i = 0;
        foreach (var at in picked)
        {
            var cell = cells[at];
            amended[i++] = cells[at] = cell with { Value = cell.Value + Math.Round((decimal)((random.NextDouble() - 0.5) * 200), 2) };
        }
        return amended;
    }

    /// <summary>ExPivot bound to D10's report on the counting renderer, on a clock the harness moves,
    /// slicing off; optionally scrolled to report row <paramref name="scrollToRow"/> (the paint-text
    /// record's GC runs scrolled to row 1,000).</summary>
    internal static async Task<(CountingRenderer Renderer, PivotComponent Pivot, SnapshotPivotSource Source, Cell[] Cells, FakeTimeProvider Clock, PivotCaps Caps)> StartAsync(int aCount, int bCount, int scrollToRow = 0)
    {
        var cells = Make(aCount, bCount);
        var caps = new PivotCaps { MaxLeaves = 2_000_000 };
        var source = PivotSource.From(cells, Fields, Never);
        var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch.AddYears(56));
        var renderer = new CountingRenderer(new Services(clock)) { Watched = typeof(ExGridRow<>) };
        var pivot = renderer.Create<PivotComponent>();
        var pivotId = renderer.Attach(pivot);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RenderRootAsync(pivotId, ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(PivotComponent.Source)] = source,
            [nameof(PivotComponent.Layout)] = Layout,
            [nameof(PivotComponent.RedrawInterval)] = TimeSpan.FromMilliseconds(250),
            [nameof(PivotComponent.ChangeHighlightDuration)] = TimeSpan.FromSeconds(1),
            [nameof(PivotComponent.Culture)] = CultureInfo.GetCultureInfo("en-US"),
            [nameof(PivotComponent.ViewportHeight)] = (ViewportSize)300,
            [nameof(PivotComponent.ViewportWidth)] = (ViewportSize)900,
            [nameof(PivotComponent.Slicing)] = Never,
            [nameof(PivotComponent.Caps)] = caps,
        })));
        for (var wait = 0; pivot.Report is null; wait++)
        {
            if (wait > 6000)
                throw new TimeoutException("ExPivot showed no report.");
            await Task.Delay(10);
            await GridCosts.SettleAsync(renderer);
        }
        await GridCosts.SettleAsync(renderer);
        if (scrollToRow > 0)
        {
            var grid = Reflect.Get<ExGrid<PivotReportRow>>(pivot, "_grid");
            var height = Reflect.Get<GridMetrics>(grid, "_metrics").RowHeightPx;
            var applyScroll = Reflect.Method(typeof(ExGrid<PivotReportRow>), "ApplyScrollOffset");
            var onSettled = Reflect.Method(typeof(ExGrid<PivotReportRow>), "OnSettled");
            await renderer.Dispatcher.InvokeAsync(() =>
            {
                applyScroll.Invoke(grid, [new ScrollOffset(scrollToRow * height, 0)]);
                onSettled.Invoke(grid, [null]);
            });
            await GridCosts.SettleAsync(renderer);
        }
        return (renderer, pivot, source, (Cell[])cells.Clone(), clock, caps);
    }

    public static async Task<object> RunAsync(int aCount, int bCount, int[] ks, int warmup, int runs)
    {
        var t0 = Stopwatch.GetTimestamp();
        var (renderer, pivot, source, newest, clock, caps) = await StartAsync(aCount, bCount);
        var cells = Make(aCount, bCount);
        var query = PivotQuery.For(Layout, caps.MaxLeaves);
        var reportRows = pivot.Report!.Rows.Count;
        Console.WriteLine($"== ExPivot, {aCount:N0} x {bCount:N0} leaves ({cells.Length:N0} records): first report {reportRows:N0} rows, {pivot.Report.ValueColumns.Count} value column(s), ready in {Clock.Ms(t0):F0} ms ==");

        // A second source, bound to nothing: the fold and the answer alone, and the chain of cubes and
        // reports made from the last, as ExPivot makes them, to time each step alone on.
        var alone = PivotSource.From(cells, Fields, Never);
        var aloneNewest = (Cell[])cells.Clone();
        var options = Reflect.Get<PivotOptions>(pivot, "_options");
        var firstAnswer = await alone.AggregateAsync(query);
        var chainCube = await PivotEngine.CubeAsync(query, firstAnswer, alone.Fields, Never);
        var chainReport = await PivotEngine.ReportAsync(chainCube, Layout, options, Never);

        var pivotType = typeof(PivotComponent);
        var paceType = pivotType.GetNestedType("Pace", BindingFlags.NonPublic)!;
        var builtType = pivotType.GetNestedType("Built", BindingFlags.NonPublic)!;
        var labelWidths = Reflect.Method(pivotType, "LabelWidthsAsync");
        var show = Reflect.Method(pivotType, "Show");
        var applyState = Reflect.Method(typeof(ExGrid<PivotReportRow>), "ApplyState");
        var requireKeys = (Action<IReadOnlyList<PivotReportRow>, int, Func<PivotReportRow, object>>)Reflect.Method(typeof(ExGrid<PivotReportRow>), "RequireDistinctKeys")
            .CreateDelegate(typeof(Action<IReadOnlyList<PivotReportRow>, int, Func<PivotReportRow, object>>));
        var requireRows = (Action<IReadOnlyList<PivotReportRow>>)Reflect.Method(typeof(ExGrid<PivotReportRow>), "RequireDistinctRows")
            .CreateDelegate(typeof(Action<IReadOnlyList<PivotReportRow>>));
        var reportRowKey = (Func<PivotReportRow, object>)Reflect.Field(pivotType, "ReportRowKey").GetValue(null)!;
        var grid = Reflect.Get<ExGrid<PivotReportRow>>(pivot, "_grid");

        var results = new List<object>();
        foreach (var k in ks)
        {
            Console.WriteLine($"-- k = {k:N0} changes a batch --");
            var random = new Random(20261006 + k);
            var machineBefore = Machine.Snapshot();

            // The source alone: the fold, and the answer — naming the version of the cube it is made
            // from, so it says which leaves changed — and the chain made from it, untimed.
            var fold = new List<double>();
            var answer = new List<double>();
            var named = new List<double>();
            PivotAnswer lastAnswer = firstAnswer;
            PivotCube previousCube = chainCube;
            PivotReport previousReport = chainReport;
            for (var r = 0; r < warmup + runs; r++)
            {
                var batch = Fields.Batch(changed: Amend(aloneNewest, k, random));
                Clock.Collect();
                var a = Stopwatch.GetTimestamp();
                alone.Apply(batch);
                var f = Clock.Ms(a);
                Clock.Collect();
                a = Stopwatch.GetTimestamp();
                lastAnswer = await alone.AggregateAsync(query.WithChangedSince(chainCube.SourceVersion));
                var q = Clock.Ms(a);
                previousCube = chainCube;
                previousReport = chainReport;
                chainCube = NextCube(chainCube, query, lastAnswer, alone.Fields);
                chainReport = NextReport(chainReport, chainCube, options);
                if (r >= warmup)
                {
                    fold.Add(f);
                    answer.Add(q);
                    named.Add(lastAnswer.ChangedLeaves is { } leafChanges ? NamedCount(leafChanges) : -1);
                }
            }

            // The whole redraw as ExPivot runs it: the clock moved on past the redraw interval, one
            // batch applied on the renderer's context, the report shown and rendered inline; the
            // collector's pauses and collections inside it.
            var whole = new List<double>();
            var wholePause = new List<double>();
            var wholeGen0 = new List<double>();
            var wholeGen1 = new List<double>();
            var wholeGen2 = new List<double>();
            var wholeAllocatedMB = new List<double>();
            var render = new List<double>();
            var painted = new List<double>();
            var rendered = new List<double>();
            var mounted = new List<double>();
            var changed = new List<double>();
            var sharedRows = new List<double>();
            var madeFrom = 0;
            var inline = 0;
            PivotReport before = pivot.Report!, after = pivot.Report!;
            object? historyBefore = null;
            for (var r = 0; r < warmup + runs; r++)
            {
                var amendedCells = Amend(newest, k, random);
                var batch = Fields.Batch(changed: amendedCells);
                before = pivot.Report!;
                historyBefore = Reflect.Get(pivot, "_history");
                await renderer.Dispatcher.InvokeAsync(() => clock.Advance(TimeSpan.FromMilliseconds(250)));
                await GridCosts.SettleAsync(renderer);
                double e = 0, pause = 0, allocated = 0;
                int g0 = 0, g1 = 0, g2 = 0;
                var shownInline = false;
                BatchCounts? counts = null;
                double renderMs = 0;
                await renderer.Dispatcher.InvokeAsync(() =>
                {
                    Clock.Collect();
                    renderer.Take();
                    var c0 = GC.CollectionCount(0);
                    var c1 = GC.CollectionCount(1);
                    var c2 = GC.CollectionCount(2);
                    var p0 = GC.GetTotalPauseDuration();
                    var b0 = GC.GetTotalAllocatedBytes(precise: false);
                    var a = Stopwatch.GetTimestamp();
                    source.Apply(batch);
                    e = Clock.Ms(a);
                    pause = (GC.GetTotalPauseDuration() - p0).TotalMilliseconds;
                    allocated = (GC.GetTotalAllocatedBytes(precise: false) - b0) / 1048576.0;
                    g0 = GC.CollectionCount(0) - c0;
                    g1 = GC.CollectionCount(1) - c1;
                    g2 = GC.CollectionCount(2) - c2;
                    shownInline = !ReferenceEquals(pivot.Report, before);
                    (counts, renderMs) = renderer.Take();
                });
                for (var wait = 0; ReferenceEquals(pivot.Report, before); wait++)
                {
                    if (wait > 6000)
                        throw new TimeoutException("ExPivot showed no new report.");
                    await Task.Delay(10);
                }
                await GridCosts.SettleAsync(renderer);
                after = pivot.Report!;
                if (r < warmup)
                    continue;
                inline += shownInline ? 1 : 0;
                madeFrom += WasMadeFrom(after, before) ? 1 : 0;
                whole.Add(e);
                wholePause.Add(pause);
                wholeGen0.Add(g0);
                wholeGen1.Add(g1);
                wholeGen2.Add(g2);
                wholeAllocatedMB.Add(allocated);
                render.Add(renderMs);
                var rows = renderer.LiveWatched.OfType<ExGridRow<PivotReportRow>>().Where(x => !x.Placeholder).ToList();
                painted.Add(rows.Count);
                rendered.Add(counts!.WatchedRendered);
                mounted.Add(counts.WatchedMounted);
                changed.Add(PaintedChanged(rows.Select(x => x.Row), before, after));
                sharedRows.Add(SharedRows(before, after));
            }

            // Each step alone, on the last link of the chain: the cube and report before it, its answer.
            var lastCube = chainCube;
            var lastReport = chainReport;
            var nextCubeStep = Clock.Repeat(warmup, runs, () => NextCube(previousCube, query, lastAnswer, alone.Fields));
            var unnamed = lastAnswer.WithChangedLeaves(null);
            var nextCubeComparedStep = Clock.Repeat(warmup, runs, () => NextCube(previousCube, query, unnamed, alone.Fields));
            var cubeAfreshStep = Clock.Repeat(warmup, runs, () => PivotEngine.CubeAsync(query, lastAnswer, alone.Fields, Never).GetAwaiter().GetResult());
            var nextReportStep = Clock.Repeat(warmup, runs, () => NextReport(previousReport, lastCube, options));
            var reportAfreshStep = Clock.Repeat(warmup, runs, () => PivotEngine.ReportAsync(lastCube, Layout, options, Never).GetAwaiter().GetResult());
            // A data redraw laid out afresh (after a compaction of the source, ADR-0161) compares every
            // row for the Change Highlight: ChangesSince over two reports laid out afresh, whose value
            // cells are read for the first time — each run on a new pair, made untimed.
            var changesAfresh = new List<double>();
            for (var r = 0; r < warmup + runs; r++)
            {
                var earlierAfresh = PivotEngine.ReportAsync(previousCube, Layout, options, Never).GetAwaiter().GetResult();
                var laterAfresh = PivotEngine.ReportAsync(lastCube, Layout, options, Never).GetAwaiter().GetResult();
                Clock.Collect();
                var a = Stopwatch.GetTimestamp();
                ChangesSince(laterAfresh, earlierAfresh);
                if (r >= warmup)
                    changesAfresh.Add(Clock.Ms(a));
            }
            var changesAfreshStep = Stat.Of(changesAfresh);
            var chainShared = SharedRows(previousReport, lastReport);
            var chainMadeFrom = WasMadeFrom(lastReport, previousReport);
            var sameRows = lastReport.HasSameRowsAs(previousReport);
            var sameRowsStep = Clock.Repeat(warmup, runs, () => lastReport.HasSameRowsAsAsync(previousReport, Never).GetAwaiter().GetResult());
            var changesStep = Clock.Repeat(warmup, runs, () => ChangesSince(lastReport, previousReport));

            // The ExPivot steps, on the bound pivot's last redraw: the label widths (skipped when the
            // report was made from the one on screen), and Show, with the history's Record.
            var metrics = Reflect.Get<GridMetrics>(pivot, "_metrics");
            double[]? widths = null;
            var labelStep = await renderer.Dispatcher.InvokeAsync(() => Clock.Repeat(warmup, runs, () =>
            {
                var pace = Activator.CreateInstance(paceType, Never, CancellationToken.None)!;
                widths = ((Task<double[]>)labelWidths.Invoke(pivot, [after, metrics, pace])!).GetAwaiter().GetResult();
            }));
            var changes = ChangesSince(after, before);
            var built = Activator.CreateInstance(builtType, after, after.HasSameRowsAs(before), changes, widths, metrics)!;
            var at = clock.GetUtcNow();
            var showStep = await renderer.Dispatcher.InvokeAsync(() => Clock.Repeat(warmup, runs, () =>
            {
                Reflect.Set(pivot, "_report", before);
                Reflect.Set(pivot, "_history", historyBefore);
                show.Invoke(pivot, [built, Layout, at, true]);
            }));
            var keysStep = Clock.Repeat(warmup, runs, () => requireKeys(after.Rows, 0, reportRowKey));
            var rowsStep = Clock.Repeat(warmup, runs, () => requireRows(after.Rows));
            var flip = false;
            var takeInStep = await renderer.Dispatcher.InvokeAsync(() => Clock.Repeat(warmup, runs, () =>
            {
                grid.Window = (flip = !flip) ? after.Rows : before.Rows;
                applyState.Invoke(grid, null);
            }));
            var vouched = Reflect.Get<bool>(grid, "_windowVouched");
            await renderer.Dispatcher.InvokeAsync(() =>
            {
                grid.Window = after.Rows;
                applyState.Invoke(grid, null);
                Reflect.StateHasChanged(pivot);
            });
            await GridCosts.SettleAsync(renderer);

            var result = new
            {
                leaves = new { a = aCount, b = bCount },
                records = cells.Length,
                reportRows = after.Rows.Count,
                k,
                sourceFold = Stat.Of(fold),
                sourceAnswer = Stat.Of(answer),
                answerNamedLeaves = Stat.Of(named),
                nextCubeAsync = nextCubeStep,
                nextCubeEngineCompares = nextCubeComparedStep,
                cubeAsyncAfresh = cubeAfreshStep,
                nextReportAsync = nextReportStep,
                reportAsyncAfresh = reportAfreshStep,
                chainRowsSharedOfReport = chainShared,
                chainWasMadeFrom = chainMadeFrom,
                hasSameRowsAsAsync = sameRowsStep,
                sameRows,
                changesSinceAsync = changesStep,
                changesSinceAfreshBothCold = changesAfreshStep,
                labelWidthsAsync = labelStep,
                labelWidthsSkippedInRedraws = $"{madeFrom} of {runs}",
                show = showStep,
                gridRequireDistinctKeys = keysStep,
                gridRequireDistinctRows = rowsStep,
                gridApplyState = takeInStep,
                gridWindowVouched = vouched,
                renderInRenderer = Stat.Of(render),
                wholeRedraw = Stat.Of(whole),
                wholeRedrawGcPauseMs = Stat.Of(wholePause),
                wholeRedrawGen0 = Stat.Of(wholeGen0),
                wholeRedrawGen1 = Stat.Of(wholeGen1),
                wholeRedrawGen2 = Stat.Of(wholeGen2),
                wholeRedrawAllocatedMB = Stat.Of(wholeAllocatedMB),
                redrawsShownInline = $"{inline} of {runs}",
                reportRowsSharedWithPrevious = Stat.Of(sharedRows),
                paintedRows = Stat.Of(painted),
                paintedRowsRendered = Stat.Of(rendered),
                paintedRowsMounted = Stat.Of(mounted),
                paintedRowsWithAChangedValue = Stat.Of(changed),
                machineBefore,
                machineAfter = Machine.Snapshot(),
            };
            results.Add(result);
            Print("fold (SnapshotPivotSource.Apply)", result.sourceFold);
            Print("answer (AggregateAsync, named)", result.sourceAnswer);
            Console.WriteLine($"   leaves the answer named: median {result.answerNamedLeaves.Median}");
            Print("NextCube (leaves named)", nextCubeStep);
            Print("NextCube (engine compares)", nextCubeComparedStep);
            Print("  CubeAsync afresh (before's path)", cubeAfreshStep);
            Print("NextReport", nextReportStep);
            Print("  ReportAsync afresh (before's path)", reportAfreshStep);
            Console.WriteLine($"   chain: {chainShared:N0} of {lastReport.Rows.Count:N0} rows shared; made from: {chainMadeFrom}");
            Print("HasSameRowsAsAsync", sameRowsStep);
            Print("ChangesSinceAsync", changesStep);
            Print("  ChangesSince, both laid out afresh", changesAfreshStep);
            Print("LabelWidthsAsync (skipped if made from)", labelStep);
            Print("Show (history Record)", showStep);
            Print("grid RequireDistinctKeys (not run)", keysStep);
            Print("grid RequireDistinctRows (not run)", rowsStep);
            Print($"grid ApplyState (vouched {vouched})", takeInStep);
            Print("render (incl. painted keys)", result.renderInRenderer);
            Print("whole redraw", result.wholeRedraw);
            Print("  GC pause in it", result.wholeRedrawGcPauseMs);
            Console.WriteLine($"   GCs per redraw gen0/1/2 median {result.wholeRedrawGen0.Median}/{result.wholeRedrawGen1.Median}/{result.wholeRedrawGen2.Median}; allocated {result.wholeRedrawAllocatedMB.Median:F1} MB; label widths skipped {result.labelWidthsSkippedInRedraws}; rows shared {result.reportRowsSharedWithPrevious.Median:N0}");
            Console.WriteLine($"   painted {result.paintedRows.Median}, rendered {result.paintedRowsRendered.Median} (min {result.paintedRowsRendered.Min}, max {result.paintedRowsRendered.Max}), mounted {result.paintedRowsMounted.Median}, with a changed value {result.paintedRowsWithAChangedValue.Median} (max {result.paintedRowsWithAChangedValue.Max}); inline {result.redrawsShownInline}");
        }
        await renderer.DisposeAsync();
        return new { aCount, bCount, reportRows, results };
    }

    /// <summary>The leaves an answer named as changed, or -2 when it says they were made afresh.</summary>
    private static int NamedCount(PivotLeafChanges changes) => changes.SameLeaves ? changes.Leaves.Count : -2;

    /// <summary>How many of <paramref name="after"/>'s rows are the same instance as
    /// <paramref name="before"/>'s row under the same key.</summary>
    private static int SharedRows(PivotReport before, PivotReport after)
    {
        var previous = new Dictionary<PivotRowKey, PivotReportRow>(before.Rows.Count);
        foreach (var row in before.Rows)
            previous.TryAdd(row.Key, row);
        var shared = 0;
        foreach (var row in after.Rows)
        {
            if (previous.TryGetValue(row.Key, out var old) && ReferenceEquals(old, row))
                shared++;
        }
        return shared;
    }

    private static void Print(string name, Stat stat) => Console.WriteLine($"   {name,-40} {stat}");

    /// <summary>
    /// The memory a run of live redraws keeps (the browser ran out of it, ticket 01): ExPivot as
    /// <see cref="RunAsync"/> drives it, and after each redraw a full collection, the managed heap,
    /// and how many of the reports and cubes shown so far are still alive (weak references to each).
    /// </summary>
    public static async Task<object> MemoryAsync(int aCount, int bCount, int k, int redraws)
    {
        var (renderer, pivot, source, newest, clock, _) = await StartAsync(aCount, bCount);
        var random = new Random(7);
        var reports = new List<WeakReference>();
        var cubes = new List<WeakReference>();
        var samples = new List<object>();
        Clock.Collect();
        var baseline = GC.GetTotalMemory(forceFullCollection: true);
        Console.WriteLine($"== ExPivot memory, {aCount:N0} x {bCount:N0}, k {k}: after the first report {baseline / 1048576.0:F1} MB ==");
        for (var r = 0; r < redraws; r++)
        {
            reports.Add(new WeakReference(pivot.Report));
            cubes.Add(new WeakReference(pivot.Report!.Cube));
            PivotReport? before = pivot.Report;
            var batch = Fields.Batch(changed: Amend(newest, k, random));
            // As the browser page paces it: past the redraw interval, so each batch is redrawn at once.
            await renderer.Dispatcher.InvokeAsync(() => clock.Advance(TimeSpan.FromMilliseconds(400)));
            await renderer.Dispatcher.InvokeAsync(() => source.Apply(batch!));
            while (ReferenceEquals(pivot.Report, before))
                await Task.Delay(5);
            await GridCosts.SettleAsync(renderer);
            // Whether this redraw was made from the report before (ADR-0161) or laid out afresh.
            var madeFrom = WasMadeFrom(pivot.Report!, before!);
            // The loop's own hold on the report before (a local hoisted into the state machine) goes,
            // so that what stays alive is what ExPivot and the grid hold.
            before = null;
            batch = null;
            Clock.Collect();
            var heap = GC.GetTotalMemory(forceFullCollection: true);
            var aliveReports = reports.Count(w => w.IsAlive);
            var aliveCubes = cubes.Count(w => w.IsAlive);
            var history = Reflect.Get(pivot, "_history");
            var kept = history is null ? 0 : (int)history.GetType().GetProperty("Kept", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(history)!;
            samples.Add(new { redraw = r + 1, madeFrom, heapMB = Math.Round(heap / 1048576.0, 1), aliveEarlierReports = aliveReports, aliveEarlierCubes = aliveCubes, historyTimesKept = kept, workingSetMB = Math.Round(Environment.WorkingSet / 1048576.0, 1) });
            Console.WriteLine($"   redraw {r + 1,3}: heap {heap / 1048576.0,8:F1} MB; earlier reports alive {aliveReports,3} of {reports.Count}, cubes {aliveCubes,3}; history times kept {kept}; made from the last {madeFrom}");
        }
        await renderer.DisposeAsync();
        return new { aCount, bCount, k, redraws, baselineMB = Math.Round(baseline / 1048576.0, 1), samples };
    }

    /// <summary>The painted rows whose painted cells — labels and value texts — read otherwise than
    /// the previous report's row under the same key (M3's "unchanged", inverted), or that have no
    /// such row.</summary>
    private static int PaintedChanged(IEnumerable<PivotReportRow> rows, PivotReport before, PivotReport after)
    {
        var previous = new Dictionary<PivotRowKey, PivotReportRow>(before.Rows.Count);
        foreach (var row in before.Rows)
            previous.TryAdd(row.Key, row);
        var changed = 0;
        foreach (var row in rows)
        {
            if (!previous.TryGetValue(row.Key, out var old) || old.Labels.Count != row.Labels.Count)
            {
                changed++;
                continue;
            }
            var same = true;
            for (var i = 0; same && i < row.Labels.Count; i++)
                same = row.Labels[i] == old.Labels[i];
            for (var j = 0; same && j < after.ValueColumns.Count; j++)
                same = string.Equals(after.ValueAt(row, j)?.Text ?? "", before.ValueAt(old, j)?.Text ?? "", StringComparison.Ordinal);
            if (!same)
                changed++;
        }
        return changed;
    }
}
