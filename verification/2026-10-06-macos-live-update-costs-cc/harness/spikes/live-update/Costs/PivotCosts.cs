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
/// ExPivot's live redraw over a report of two row fields (D10's shape: A × B with A's subtotals,
/// 1,000 × B leaves), step by step as src/ExPivot/Components/ExPivot.Asking.cs runs them:
/// the bundled source folding the batch (SnapshotPivotSource.Apply) and answering (AggregateAsync,
/// from the answer it holds); PivotEngine.CubeAsync; PivotEngine.ReportAsync, with its keys;
/// HasSameRowsAsAsync; LabelWidthsAsync; Show; the grid's check of the report's keys
/// (RequireDistinctKeys) and its ApplyState; and the render. Each step alone on the same inputs —
/// the last redraw's — and the whole redraw as ExPivot runs it (slicing off, so it runs inline:
/// the steps cost what they cost; with slicing on they are spread over turns). And M3's count of the
/// painted rows rendered per redraw against those whose painted values changed.
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

    public static async Task<object> RunAsync(int aCount, int bCount, int[] ks, int warmup, int runs)
    {
        var t0 = Stopwatch.GetTimestamp();
        var cells = Make(aCount, bCount);
        Console.WriteLine($"== ExPivot, {aCount:N0} x {bCount:N0} leaves ({cells.Length:N0} records), made in {Clock.Ms(t0):F0} ms ==");
        var caps = new PivotCaps { MaxLeaves = 2_000_000 };
        var query = PivotQuery.For(Layout, caps.MaxLeaves);

        // The source ExPivot is bound to, and ExPivot on a clock the harness moves.
        var source = PivotSource.From(cells, Fields, Never);
        var newest = (Cell[])cells.Clone();
        var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch.AddYears(56));
        var renderer = new CountingRenderer(new Services(clock)) { Watched = typeof(ExGridRow<>) };
        var pivot = renderer.Create<PivotComponent>();
        var pivotId = renderer.Attach(pivot);
        t0 = Stopwatch.GetTimestamp();
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
        var reportRows = pivot.Report.Rows.Count;
        Console.WriteLine($"   first report: {reportRows:N0} rows, {pivot.Report.ValueColumns.Count} value column(s), in {Clock.Ms(t0):F0} ms");

        // A second source, bound to nothing: the fold and the answer alone.
        var alone = PivotSource.From(cells, Fields, Never);
        var aloneNewest = (Cell[])cells.Clone();
        var answerHeld = await alone.AggregateAsync(query);

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
        var options = Reflect.Get<PivotOptions>(pivot, "_options");

        var results = new List<object>();
        foreach (var k in ks)
        {
            Console.WriteLine($"-- k = {k:N0} changes a batch --");
            var random = new Random(20261006 + k);
            var machineBefore = Machine.Snapshot();

            // The source alone: the fold, and the answer from the pass it holds.
            var fold = new List<double>();
            var answer = new List<double>();
            PivotAnswer? lastAnswer = null;
            for (var r = 0; r < warmup + runs; r++)
            {
                var batch = Fields.Batch(changed: Amend(aloneNewest, k, random));
                Clock.Collect();
                var a = Stopwatch.GetTimestamp();
                alone.Apply(batch);
                var f = Clock.Ms(a);
                Clock.Collect();
                a = Stopwatch.GetTimestamp();
                lastAnswer = await alone.AggregateAsync(query);
                var q = Clock.Ms(a);
                if (r >= warmup)
                {
                    fold.Add(f);
                    answer.Add(q);
                }
            }

            // The whole redraw as ExPivot runs it: the clock moved on past the redraw interval, one
            // batch applied on the renderer's context, the report shown and rendered inline.
            var whole = new List<double>();
            var render = new List<double>();
            var painted = new List<double>();
            var rendered = new List<double>();
            var mounted = new List<double>();
            var changed = new List<double>();
            var inline = 0;
            PivotReport before = pivot.Report!, after = pivot.Report!;
            object? historyBefore = null, historyAfter = null;
            for (var r = 0; r < warmup + runs; r++)
            {
                var amendedCells = Amend(newest, k, random);
                var batch = Fields.Batch(changed: amendedCells);
                before = pivot.Report!;
                historyBefore = Reflect.Get(pivot, "_history");
                await renderer.Dispatcher.InvokeAsync(() => clock.Advance(TimeSpan.FromMilliseconds(250)));
                await GridCosts.SettleAsync(renderer);
                double e = 0;
                var shownInline = false;
                BatchCounts? counts = null;
                double renderMs = 0;
                await renderer.Dispatcher.InvokeAsync(() =>
                {
                    Clock.Collect();
                    renderer.Take();
                    var a = Stopwatch.GetTimestamp();
                    source.Apply(batch);
                    e = Clock.Ms(a);
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
                historyAfter = Reflect.Get(pivot, "_history");
                if (r < warmup)
                    continue;
                inline += shownInline ? 1 : 0;
                whole.Add(e);
                render.Add(renderMs);
                var rows = renderer.LiveWatched.OfType<ExGridRow<PivotReportRow>>().Where(x => !x.Placeholder).ToList();
                painted.Add(rows.Count);
                rendered.Add(counts!.WatchedRendered);
                mounted.Add(counts.WatchedMounted);
                changed.Add(PaintedChanged(rows.Select(x => x.Row), before, after));
                if (Environment.GetEnvironmentVariable("COSTS_DEBUG") == "1")
                {
                    foreach (var x in rows.OrderBy(x => x.RowIndex).Take(4))
                    {
                        var old = before.Rows.FirstOrDefault(o => o.Key.Equals(x.Row.Key));
                        Console.WriteLine($"     row {x.RowIndex}: {string.Join("|", x.Row.Labels.Select(l => l.Text))} = {x.Row.ValueAt(0)?.Text} (was {old?.ValueAt(0)?.Text}); same instance as after.Rows[{x.RowIndex}]: {ReferenceEquals(after.Rows[x.RowIndex], x.Row)}");
                    }
                    Console.WriteLine($"     batch: {string.Join(", ", amendedCells.Take(5).Select(c => c.A + "/" + c.B))}");
                }
            }

            // Each step alone, on the last redraw's inputs: its answer, its cube, the report before
            // it and after it.
            var cube = after.Cube;
            var cubeStep = Clock.Repeat(warmup, runs, () => PivotEngine.CubeAsync(query, lastAnswer!, alone.Fields, Never).GetAwaiter().GetResult());
            var reportStep = Clock.Repeat(warmup, runs, () => PivotEngine.ReportAsync(cube, Layout, options, Never).GetAwaiter().GetResult());
            var sameRows = after.HasSameRowsAs(before);
            var sameRowsStep = Clock.Repeat(warmup, runs, () => after.HasSameRowsAsAsync(before, Never).GetAwaiter().GetResult());
            var metrics = Reflect.Get<GridMetrics>(pivot, "_metrics");
            double[]? widths = null;
            var labelStep = await renderer.Dispatcher.InvokeAsync(() => Clock.Repeat(warmup, runs, () =>
            {
                var pace = Activator.CreateInstance(paceType, Never, CancellationToken.None)!;
                widths = ((Task<double[]>)labelWidths.Invoke(pivot, [after, metrics, pace])!).GetAwaiter().GetResult();
            }));
            var built = Activator.CreateInstance(builtType, after, before, sameRows, widths, metrics)!;
            var at = clock.GetUtcNow();
            var showStep = await renderer.Dispatcher.InvokeAsync(() => Clock.Repeat(warmup, runs, () =>
            {
                Reflect.Set(pivot, "_report", before);
                Reflect.Set(pivot, "_history", historyBefore);
                show.Invoke(pivot, [built, Layout, at, true]);
            }));
            var keysStep = Clock.Repeat(warmup, runs, () => requireKeys(after.Rows, 0, reportRowKey));
            var rowsStep = Clock.Repeat(warmup, runs, () => requireRows(after.Rows));
            var grid = Reflect.Get<ExGrid<PivotReportRow>>(pivot, "_grid");
            var flip = false;
            var takeInStep = await renderer.Dispatcher.InvokeAsync(() => Clock.Repeat(warmup, runs, () =>
            {
                grid.Window = (flip = !flip) ? after.Rows : before.Rows;
                applyState.Invoke(grid, null);
            }));
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
                cubeAsync = cubeStep,
                reportAsync = reportStep,
                hasSameRowsAsAsync = sameRowsStep,
                sameRows,
                labelWidthsAsync = labelStep,
                show = showStep,
                gridRequireDistinctKeys = keysStep,
                gridRequireDistinctRows = rowsStep,
                gridApplyState = takeInStep,
                renderInRenderer = Stat.Of(render),
                wholeRedraw = Stat.Of(whole),
                redrawsShownInline = $"{inline} of {runs}",
                paintedRows = Stat.Of(painted),
                paintedRowsRendered = Stat.Of(rendered),
                paintedRowsMounted = Stat.Of(mounted),
                paintedRowsWithAChangedValue = Stat.Of(changed),
                machineBefore,
                machineAfter = Machine.Snapshot(),
            };
            results.Add(result);
            Print("fold (SnapshotPivotSource.Apply)", result.sourceFold);
            Print("answer (AggregateAsync, held)", result.sourceAnswer);
            Print("CubeAsync", cubeStep);
            Print("ReportAsync", reportStep);
            Print("HasSameRowsAsAsync", sameRowsStep);
            Print("LabelWidthsAsync", labelStep);
            Print("Show", showStep);
            Print("grid RequireDistinctKeys", keysStep);
            Print("grid RequireDistinctRows", rowsStep);
            Print("grid ApplyState", takeInStep);
            Print("render (incl. grid ApplyState)", result.renderInRenderer);
            Print("whole redraw", result.wholeRedraw);
            Console.WriteLine($"   painted {result.paintedRows.Median}, rendered {result.paintedRowsRendered.Median} (min {result.paintedRowsRendered.Min}), mounted {result.paintedRowsMounted.Median}, with a changed value {result.paintedRowsWithAChangedValue.Median} (max {result.paintedRowsWithAChangedValue.Max}); inline {result.redrawsShownInline}");
        }
        await renderer.DisposeAsync();
        return new { aCount, bCount, reportRows, results };
    }

    private static void Print(string name, Stat stat) => Console.WriteLine($"   {name,-34} {stat}");

    /// <summary>
    /// The memory a run of live redraws keeps (the browser ran out of it, ticket 01): ExPivot as
    /// <see cref="RunAsync"/> drives it, and after each redraw a full collection, the managed heap,
    /// and how many of the reports and cubes shown so far are still alive (weak references to each).
    /// </summary>
    public static async Task<object> MemoryAsync(int aCount, int bCount, int k, int redraws)
    {
        var cells = Make(aCount, bCount);
        var caps = new PivotCaps { MaxLeaves = 2_000_000 };
        var source = PivotSource.From(cells, Fields, Never);
        var newest = (Cell[])cells.Clone();
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
        while (pivot.Report is null)
        {
            await Task.Delay(10);
            await GridCosts.SettleAsync(renderer);
        }
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
            var before = pivot.Report;
            var batch = Fields.Batch(changed: Amend(newest, k, random));
            // As the browser page paces it: past the redraw interval, so each batch is redrawn at once.
            await renderer.Dispatcher.InvokeAsync(() => clock.Advance(TimeSpan.FromMilliseconds(400)));
            await renderer.Dispatcher.InvokeAsync(() => source.Apply(batch));
            while (ReferenceEquals(pivot.Report, before))
                await Task.Delay(5);
            await GridCosts.SettleAsync(renderer);
            Clock.Collect();
            var heap = GC.GetTotalMemory(forceFullCollection: true);
            var aliveReports = reports.Count(w => w.IsAlive);
            var aliveCubes = cubes.Count(w => w.IsAlive);
            var history = Reflect.Get(pivot, "_history");
            var versions = history is null ? 0 : (int)history.GetType().GetProperty("Count")!.GetValue(history)!;
            samples.Add(new { redraw = r + 1, heapMB = Math.Round(heap / 1048576.0, 1), aliveEarlierReports = aliveReports, aliveEarlierCubes = aliveCubes, historyVersions = versions, workingSetMB = Math.Round(Environment.WorkingSet / 1048576.0, 1) });
            Console.WriteLine($"   redraw {r + 1,3}: heap {heap / 1048576.0,8:F1} MB; earlier reports alive {aliveReports,3} of {reports.Count}, cubes {aliveCubes,3}; history versions {versions}");
        }
        // The retention's path, checked: the report grid's kept paints (ADR-0142's seen text, up to
        // 64) hold the row instances each paint painted, and a report row points at its report. With
        // them let go — harness only, by reflection — the earlier reports should go too.
        var grid = Reflect.Get<ExGrid<PivotReportRow>>(pivot, "_grid");
        var paints = (System.Collections.IList)Reflect.Get(grid, "_paints")!;
        var paintsKept = paints.Count;
        await renderer.Dispatcher.InvokeAsync(() => paints.Clear());
        Clock.Collect();
        var heapAfterClearing = GC.GetTotalMemory(forceFullCollection: true);
        var reportsAfterClearing = reports.Count(w => w.IsAlive);
        var cubesAfterClearing = cubes.Count(w => w.IsAlive);
        Console.WriteLine($"   the grid kept {paintsKept} paints; with them let go: heap {heapAfterClearing / 1048576.0:F1} MB, earlier reports alive {reportsAfterClearing} of {reports.Count}, cubes {cubesAfterClearing}");
        await renderer.DisposeAsync();
        return new
        {
            aCount, bCount, k, redraws, baselineMB = Math.Round(baseline / 1048576.0, 1), samples,
            paintsKept, heapAfterClearingPaintsMB = Math.Round(heapAfterClearing / 1048576.0, 1),
            earlierReportsAliveAfterClearingPaints = reportsAfterClearing, earlierCubesAliveAfterClearingPaints = cubesAfterClearing,
        };
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
                same = string.Equals(row.ValueAt(j)?.Text ?? "", old.ValueAt(j)?.Text ?? "", StringComparison.Ordinal);
            if (!same)
                changed++;
        }
        return changed;
    }
}
