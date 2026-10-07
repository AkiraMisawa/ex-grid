using System.Globalization;
using System.Text.Json;
using Bunit;
using ExGrid;
using ExGrid.Components;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using PivotComponent = ExPivot.Components.ExPivot;
using PivotRedraw;

// dotnet run -c Release -- <out.json> [redraws]
//
// For each configuration — a layout, with or without subtotals, a number of trades and of changes
// per batch — ExPivot over the bundled source of /pivot-live's trades, on a fake clock, as
// tests/ExPivot.Components does (PV-35). Each redraw: the clock moves on by the redraw interval, one
// Change Batch is applied, and the report on screen is waited for (a condition, not a time). Then
// the report grid's painted rows (ExGridRow components) are counted:
//   - mounted: a component instance not painted before this redraw;
//   - rendered: mounted, or painted before and rendered again;
//   - unchanged: its row stands for what a row of the previous report stood for (role, Value Field
//     and Items, as ReportHistory pairs them) and every cell it paints — labels and value texts —
//     reads the same, under the same value columns. These are the rows candidate 1 would hand over
//     as the same instance.

var output = args.Length > 0 ? args[0] : "pivot-redraw.json";
var redraws = args.Length > 1 ? int.Parse(args[1]) : 300;
var only = args.Length > 2 ? args[2] : null;

var interval = TimeSpan.FromMilliseconds(250);
var highlight = TimeSpan.FromSeconds(1);
var never = new PivotSlicing { Budget = TimeSpan.FromDays(1) };

var layouts = new (string Name, Func<bool, PivotLayout> Make)[]
{
    // /pivot-live's own: P&L by region and desk across products, filtered by currency.
    ("pivot-live (Region > Desk x Product)", subtotals => new PivotLayout
    {
        Filters = [new PivotFieldPlacement("Currency")],
        Rows = [new PivotFieldPlacement("Region") { Subtotals = subtotals }, new PivotFieldPlacement("Desk")],
        Columns = [new PivotFieldPlacement("Product")],
        Values = [new PivotValueField("Pnl", PivotAggregation.Sum) { NumberFormat = "#,##0" }],
    }),
    // A taller report, so that the painted rows are a slice of it: book by month.
    ("Book > Month x Product", subtotals => new PivotLayout
    {
        Rows = [new PivotFieldPlacement("Book") { Subtotals = subtotals }, new PivotFieldPlacement("Month")],
        Columns = [new PivotFieldPlacement("Product")],
        Values = [new PivotValueField("Pnl", PivotAggregation.Sum) { NumberFormat = "#,##0" }],
    }),
};

var configs = new List<(int Trades, int Changes)> { (2_000, 5), (2_000, 10), (2_000, 100), (2_000, 1_000), (1_000_000, 1_000) };
var results = new List<object>();
Console.WriteLine($"{"layout",-38} {"subt",4} {"trades",9} {"chg",5} {"rows",5} {"painted",7} {"mounted",8} {"rendered",8} {"unchanged (min/med/mean/max)",30} {"report unchanged",16}");
foreach (var (layoutName, makeLayout) in layouts)
{
    foreach (var subtotals in new[] { true, false })
    {
        foreach (var (tradeCount, changes) in configs)
        {
            var label = $"{layoutName} subtotals={subtotals} trades={tradeCount} changes={changes}";
            if (only is not null && !label.Contains(only, StringComparison.Ordinal))
                continue;
            var trades = LiveTrades.Generate(tradeCount);
            var source = PivotSource.From(trades, LiveTrades.Fields, never);
            using var context = new PivotContext();
            var layout = makeLayout(subtotals);
            var cut = context.Render<PivotComponent>(ps => ps
                .Add(p => p.Source, source)
                .Add(p => p.Layout, layout)
                .Add(p => p.RedrawInterval, interval)
                .Add(p => p.ChangeHighlightDuration, highlight)
                .Add(p => p.Culture, CultureInfo.GetCultureInfo("en-US"))
                .Add(p => p.ViewportHeight, (ViewportSize)300)
                .Add(p => p.ViewportWidth, (ViewportSize)900)
                .Add(p => p.Slicing, never));
            cut.WaitForState(() => cut.Instance.Report is not null, TimeSpan.FromSeconds(120));

            var random = new Random(20261001 + changes);
            var samples = new List<Sample>(redraws);
            var seen = Painted(cut).ToDictionary(r => (object)r.Instance, r => r.RenderCount, ReferenceEqualityComparer.Instance);
            for (var i = 0; i < redraws; i++)
            {
                var previous = cut.Instance.Report!;
                context.Clock.Advance(interval);
                var amended = LiveTrades.Amend(trades, changes, random);
                await cut.InvokeAsync(() => source.Apply(LiveTrades.Fields.Batch(changed: amended)));
                cut.WaitForState(() => !ReferenceEquals(cut.Instance.Report, previous), TimeSpan.FromSeconds(120));
                var report = cut.Instance.Report!;

                var painted = Painted(cut);
                var mounted = 0;
                var rendered = 0;
                var unchanged = 0;
                var keys = KeysOf(previous);
                var sameColumns = SameColumns(report, previous);
                foreach (var row in painted)
                {
                    if (!seen.TryGetValue(row.Instance, out var before))
                    {
                        mounted++;
                        rendered++;
                    }
                    else if (row.RenderCount > before)
                    {
                        rendered++;
                    }
                    if (sameColumns && Unchanged(report, row.Instance.Row, previous, keys))
                        unchanged++;
                }
                var reportUnchanged = 0;
                if (sameColumns)
                {
                    foreach (var row in report.Rows)
                    {
                        if (Unchanged(report, row, previous, keys))
                            reportUnchanged++;
                    }
                }
                samples.Add(new Sample(report.Rows.Count, painted.Count, mounted, rendered, unchanged, reportUnchanged));
                seen = painted.ToDictionary(r => (object)r.Instance, r => r.RenderCount, ReferenceEqualityComparer.Instance);
            }

            var summary = Summary.Of(layoutName, subtotals, tradeCount, changes, samples);
            results.Add(summary);
            Console.WriteLine($"{layoutName,-38} {(subtotals ? "on" : "off"),4} {tradeCount,9:N0} {changes,5} {summary.ReportRowsMedian,5} {summary.PaintedMedian,7} "
                + $"{summary.MountedMedian,8} {summary.RenderedMedian,8} "
                + $"{$"{summary.UnchangedMin}/{summary.UnchangedMedian}/{summary.UnchangedMean:F2}/{summary.UnchangedMax}",30} "
                + $"{$"{summary.ReportUnchangedMedian} of {summary.ReportRowsMedian}",16}");
        }
    }
}

File.WriteAllText(output, JsonSerializer.Serialize(new { redraws, interval = interval.TotalMilliseconds, highlightSeconds = highlight.TotalSeconds, viewportHeight = 300, results },
    new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"wrote {output}");
return 0;

static List<IRenderedComponent<ExGridRow<PivotReportRow>>> Painted(IRenderedComponent<PivotComponent> cut)
    => [.. cut.FindComponents<ExGridRow<PivotReportRow>>().Where(r => !r.Instance.Placeholder)];

// What a report row stands for, as ReportHistory's RowKey pairs rows (role, Value Field, Items).
static string KeyOf(PivotReport report, PivotReportRow row)
    => $"{row.Role}|{row.ValueField}|" + string.Join("\u001F", report.RowPath(row).Select(p => p.Field + "=" + p.Item));

static Dictionary<string, PivotReportRow> KeysOf(PivotReport report)
{
    var keys = new Dictionary<string, PivotReportRow>(StringComparer.Ordinal);
    foreach (var row in report.Rows)
        keys.TryAdd(KeyOf(report, row), row);
    return keys;
}

static bool SameColumns(PivotReport report, PivotReport previous)
    => report.ValueColumns.Select(c => c.Name).SequenceEqual(previous.ValueColumns.Select(c => c.Name), StringComparer.Ordinal)
       && report.LabelColumns.Select(c => c.Name).SequenceEqual(previous.LabelColumns.Select(c => c.Name), StringComparer.Ordinal);

static bool Unchanged(PivotReport report, PivotReportRow row, PivotReport previous, Dictionary<string, PivotReportRow> keys)
{
    if (!keys.TryGetValue(KeyOf(report, row), out var before))
        return false;
    if (row.CarriesValues != before.CarriesValues || row.Labels.Count != before.Labels.Count)
        return false;
    for (var i = 0; i < row.Labels.Count; i++)
    {
        if (row.Labels[i] != before.Labels[i])
            return false;
    }
    for (var j = 0; j < report.ValueColumns.Count; j++)
    {
        if (!string.Equals(report.ValueAt(row, j)?.Text ?? "", previous.ValueAt(before, j)?.Text ?? "", StringComparison.Ordinal))
            return false;
    }
    return true;
}

internal sealed record Sample(int ReportRows, int Painted, int Mounted, int Rendered, int Unchanged, int ReportUnchanged);

internal sealed record Summary(
    string Layout, bool Subtotals, int Trades, int Changes, int Redraws,
    int ReportRowsMedian, int PaintedMedian, int PaintedMin, int PaintedMax,
    int MountedMin, int MountedMedian, int MountedMax,
    int RenderedMin, int RenderedMedian, int RenderedMax,
    int UnchangedMin, int UnchangedMedian, double UnchangedMean, int UnchangedMax,
    Dictionary<int, int> UnchangedHistogram,
    int ReportUnchangedMin, int ReportUnchangedMedian, double ReportUnchangedMean, int ReportUnchangedMax)
{
    public static Summary Of(string layout, bool subtotals, int trades, int changes, List<Sample> s)
    {
        static int Median(IEnumerable<int> values)
        {
            var sorted = values.OrderBy(v => v).ToArray();
            return sorted[sorted.Length / 2];
        }
        return new Summary(layout, subtotals, trades, changes, s.Count,
            Median(s.Select(x => x.ReportRows)), Median(s.Select(x => x.Painted)), s.Min(x => x.Painted), s.Max(x => x.Painted),
            s.Min(x => x.Mounted), Median(s.Select(x => x.Mounted)), s.Max(x => x.Mounted),
            s.Min(x => x.Rendered), Median(s.Select(x => x.Rendered)), s.Max(x => x.Rendered),
            s.Min(x => x.Unchanged), Median(s.Select(x => x.Unchanged)), s.Average(x => x.Unchanged), s.Max(x => x.Unchanged),
            s.GroupBy(x => x.Unchanged).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()),
            s.Min(x => x.ReportUnchanged), Median(s.Select(x => x.ReportUnchanged)), s.Average(x => x.ReportUnchanged), s.Max(x => x.ReportUnchanged));
    }
}

/// <summary>
/// tests/ExPivot.Components' PivotTestContext, reduced to what a redraw needs: ExGrid's JavaScript
/// module stood in for strictly, the clock, and an interactive Server renderer.
/// </summary>
internal sealed class PivotContext : BunitContext
{
    private const string ModulePath = "./_content/ExGrid/ex-grid.min.js";

    public PivotContext()
    {
        Services.AddSingleton<TimeProvider>(Clock);
        var module = JSInterop.SetupModule(ModulePath);
        var handle = module.SetupModule("attach", _ => true);
        handle.Setup<bool>("metaIsPrimary").SetResult(false);
        handle.Setup<ScrollOffset>("getScrollOffset").SetResult(default);
        handle.Setup<bool>("anchorScrollTop", _ => true).SetResult(true);
        foreach (var name in new[] { "setScrollOffset", "releaseTab", "setEditing", "setInnerPopup", "setClaims", "setCaret", "setPointerReporting", "forgetPointer", "writeCopy", "reclaimFocus", "focusEditor", "handKeyboardTo", "dispose" })
            handle.SetupVoid(name, _ => true).SetVoidResult();
        JSInterop.SetupVoid("Blazor._internal.domWrapper.focus", _ => true).SetVoidResult();
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    public FakeTimeProvider Clock { get; } = new();
}
