using System.Globalization;
using System.Runtime.CompilerServices;
using Bunit;
using ExGrid.Components;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// A live redraw on <c>/pivot-live</c>'s generator (ADR-0153; PV-42, PV-45; LV-30): P&amp;L by
/// region and desk across products, its trades amended a few at a time and handed to the bundled
/// source as Change Batches. The rows that render per redraw are exactly the painted rows whose
/// painted text changed, and no row component is built for a key painted before. And what a run of
/// redraws leaves alive (ADR-0160; LV-22), checked by reachability: weak references to each
/// redraw's engine report, Report Version, Window and display rows, and a full collection. The
/// clock is the test's.
/// </summary>
public class LiveRedrawTests : PivotTestContext
{
    /// <summary>/pivot-live's trade (<c>DemoPivotTrade</c>), with what its report reads.</summary>
    public sealed record LiveTrade(string Id, string Region, string Desk, string Book, string Product, decimal Notional, decimal Pnl);

    private static readonly (string Region, string[] Books)[] Regions =
    [
        ("Americas", ["NY-RATES-01", "NY-CREDIT-02", "TOR-FX-01"]),
        ("EMEA", ["LDN-RATES-01", "LDN-FX-02", "FRA-CREDIT-01", "ZRH-EQ-01"]),
        ("APAC", ["TKY-RATES-01", "HKG-EQ-01", "SGP-FX-01"]),
    ];

    private static readonly string[] Products = ["Swap", "Bond", "Option", "Future", "Spot"];

    private static readonly PivotFields<LiveTrade> TradeFields = PivotFields.Of<LiveTrade>()
        .Key("Id", t => t.Id)
        .Text("Region", t => t.Region)
        .Text("Desk", t => t.Desk)
        .Text("Book", t => t.Book)
        .Text("Product", t => t.Product)
        .Number("Notional", t => t.Notional, format: "#,##0.00")
        .Number("Pnl", t => t.Pnl, caption: "P&L", format: "#,##0.00");

    /// <summary>/pivot-live's layout: P&amp;L by region and desk across products, in whole units.</summary>
    private static readonly PivotLayout PnlByRegionAndDesk = new()
    {
        Rows = [P("Region"), P("Desk")],
        Columns = [P("Product")],
        Values = [new PivotValueField("Pnl", PivotAggregation.Sum) { NumberFormat = "#,##0" }],
    };

    // The report is made whole, without yielding: what is under test is what renders.
    private static readonly PivotSlicing Whole = new() { Budget = TimeSpan.FromDays(1) };

    /// <summary>DemoPivotData.Generate's trades, as many as asked.</summary>
    private static LiveTrade[] Generate(int count)
    {
        var random = new Random(20260930);
        var trades = new LiveTrade[count];
        for (var i = 0; i < count; i++)
        {
            var (region, books) = Regions[random.Next(Regions.Length)];
            var book = books[random.Next(books.Length)];
            var desk = book.Contains("RATES", StringComparison.Ordinal) ? "Rates"
                : book.Contains("CREDIT", StringComparison.Ordinal) ? "Credit"
                : book.Contains("FX", StringComparison.Ordinal) ? "FX"
                : "Equities";
            var notional = Math.Round((decimal)(random.Next(1, 500) * 10_000), 2);
            var pnl = Math.Round((decimal)(random.NextDouble() - 0.45) * notional / 100m, 2);
            trades[i] = new LiveTrade("T" + (100000 + i).ToString(CultureInfo.InvariantCulture), region, desk, book, Products[random.Next(Products.Length)], notional, pnl);
        }
        return trades;
    }

    /// <summary>PivotLivePage.Amend: <paramref name="count"/> trades, each picked once, their P&amp;L
    /// moved by up to 0.1% of their notional.</summary>
    private static LiveTrade[] Amend(LiveTrade[] trades, int count, Random random)
    {
        var amended = new List<LiveTrade>();
        foreach (var at in Enumerable.Range(0, trades.Length).OrderBy(_ => random.Next()).Take(count))
        {
            var move = Math.Round((decimal)(random.NextDouble() - 0.5) * trades[at].Notional / 500m, 2);
            amended.Add(trades[at] = trades[at] with { Pnl = trades[at].Pnl + move });
        }
        return [.. amended];
    }

    private static List<IRenderedComponent<ExGridRow<PivotDisplayRow>>> Painted(IRenderedComponent<PivotComponent> cut)
        => [.. cut.FindComponents<ExGridRow<PivotDisplayRow>>().Where(row => !row.Instance.Placeholder)];

    /// <summary>A row as it paints: its labels, then each value cell's text.</summary>
    private static string Painted(PivotDisplayRow row)
        => string.Join(" | ", row.Labels.Select(label => label.Text ?? "")) + " || " + string.Join(" | ", row.Values.Select(value => value?.Text ?? ""));

    [Fact] // ADR-0153/0003 (PV-45, PV-42): on /pivot-live's generator, the rows that render per live redraw are exactly the painted rows whose painted text changed, and no row component is built for a key painted before
    public async Task The_rows_that_render_are_the_painted_rows_whose_text_changed()
    {
        var trades = Generate(2_000);
        var source = PivotSource.From(trades, TradeFields, Whole);
        var cut = RenderPivot(PnlByRegionAndDesk, ps => ps.Add(p => p.Slicing, Whole), source: source);
        var components = Painted(cut).ToDictionary(row => row.Instance.Row.Key, row => row.Instance);
        var random = new Random(20261001);
        var changedRows = 0;
        var keptRows = 0;

        for (var redraw = 0; redraw < 16; redraw++)
        {
            // A second on: every mark of the redraw before has ended, and its row repainted, before
            // this redraw is counted (ADR-0068).
            Clock.Advance(TimeSpan.FromSeconds(1));
            cut.WaitForAssertion(() => Assert.Empty(ChangeHighlightTests.MarkedTexts(cut)));
            var previous = cut.Instance.Report!.Rows.ToDictionary(row => row.Key);
            var before = Painted(cut).ToDictionary(row => (object)row.Instance, row => row.RenderCount, ReferenceEqualityComparer.Instance);
            var report = cut.Instance.Report;

            await cut.InvokeAsync(() => source.Apply(TradeFields.Batch(changed: Amend(trades, 3, random))));
            cut.WaitForAssertion(() => Assert.NotSame(report, cut.Instance.Report));

            foreach (var row in Painted(cut))
            {
                var key = row.Instance.Row.Key;
                var changed = !previous.TryGetValue(key, out var old) || Painted(row.Instance.Row) != Painted(old);
                var rendered = !before.TryGetValue(row.Instance, out var count) || row.RenderCount > count;
                Assert.True(changed == rendered, $"redraw {redraw}: {key} {(changed ? "changed" : "did not change")} and {(rendered ? "rendered" : "did not render")}");
                if (components.TryGetValue(key, out var component))
                    Assert.True(ReferenceEquals(component, row.Instance), $"redraw {redraw}: the row component of {key} was built again");
                else
                    components[key] = row.Instance;
                if (changed)
                    changedRows++;
                else
                    keptRows++;
            }
        }

        // The run changed rows and kept rows alike, so neither half of the rule went untested.
        Assert.True(changedRows > 10, $"{changedRows} painted rows changed");
        Assert.True(keptRows > changedRows, $"{keptRows} painted rows kept against {changedRows} changed");
    }

    // ---- What a run of live redraws leaves alive (ADR-0160, ADR-0153; LV-22) ----

    /// <summary>One redraw's Window on screen, by weak reference only: the engine report its Report
    /// Version was laid out as, and that report's cube; the Window state ExPivot holds, and its Report
    /// Version's metadata; and the display rows.</summary>
    private sealed record Shown(WeakReference Report, WeakReference Cube, WeakReference State, WeakReference Metadata,
        WeakReference[] Rows);

    /// <summary>Reads the Window on screen, and the engine report <paramref name="source"/> laid its
    /// Report Version out as, and answers weak references to them. Read here, in a frame of its own:
    /// a local of the test, hoisted into its state machine, would keep what it read alive.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Shown TakeShown(IRenderedComponent<PivotComponent> cut, LocalPivotReportSource source)
    {
        var state = cut.Instance.Report!;
        var report = source.ReportOf(state.Metadata.Version)
            ?? throw new InvalidOperationException("The report source no longer holds the Report Version on screen.");
        return new(new(report), new(report.Cube), new(state), new(state.Metadata),
            [.. state.Rows.Select(static row => new WeakReference(row))]);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string ShownVersion(IRenderedComponent<PivotComponent> cut) => cut.Instance.Report!.Metadata.Version.Value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static LocalPivotReportSource AskedSource(IRenderedComponent<PivotComponent> cut)
        => (LocalPivotReportSource)cut.Instance.AskedSource!;

    /// <summary>A live redraw: three trades amended, and the next Report Version on screen.</summary>
    private static async Task RedrawAsync(IRenderedComponent<PivotComponent> cut, SnapshotPivotSource data, LiveTrade[] trades, Random random)
    {
        var before = ShownVersion(cut);
        await cut.InvokeAsync(() => data.Apply(TradeFields.Batch(changed: Amend(trades, 3, random))));
        cut.WaitForAssertion(() => Assert.NotEqual(before, ShownVersion(cut)));
    }

    /// <summary>Twelve live redraws, <paramref name="spacing"/> apart on the clock: the Window on
    /// screen first and after each.</summary>
    private async Task<List<Shown>> RunOfRedrawsAsync(IRenderedComponent<PivotComponent> cut, LocalPivotReportSource source,
        SnapshotPivotSource data, LiveTrade[] trades, TimeSpan spacing)
    {
        var random = new Random(20261008);
        var shown = new List<Shown> { TakeShown(cut, source) };
        for (var redraw = 0; redraw < 12; redraw++)
        {
            Clock.Advance(spacing);
            await RedrawAsync(cut, data, trades, random);
            shown.Add(TakeShown(cut, source));
        }
        return shown;
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static int[] Alive(IEnumerable<WeakReference> references)
        => [.. references.Select((reference, at) => (reference, at)).Where(p => p.reference.IsAlive).Select(p => p.at)];

    /// <summary>The rows of the Windows before <paramref name="newest"/> still alive that are none of
    /// <paramref name="among"/>' rows, as "Window: rows" — empty when there are none.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string[] AliveBeyond(List<Shown> shown, int newest, params int[] among)
    {
        var allowed = among.SelectMany(at => shown[at].Rows).Select(row => row.Target).Where(row => row is not null)
            .ToHashSet(ReferenceEqualityComparer.Instance);
        return [.. Enumerable.Range(0, newest)
            .Select(at => (at, rows: Alive(shown[at].Rows).Where(i => !allowed.Contains(shown[at].Rows[i].Target!)).ToArray()))
            .Where(p => p.rows.Length > 0)
            .Select(p => $"Window {p.at}: rows {string.Join(", ", p.rows)}")];
    }

    // /pivot-live's settings: a redraw at most every 250 ms, a Change Highlight of one second.
    private static void LiveSettings(ComponentParameterCollectionBuilder<PivotComponent> ps) => ps
        .Add(p => p.RedrawInterval, TimeSpan.FromMilliseconds(250))
        .Add(p => p.ChangeHighlightDuration, TimeSpan.FromSeconds(1));

    [Theory] // ADR-0160/0153 (LV-22): after a run of live redraws on /pivot-live's generator, no engine report, Report Version, Window or display row of an earlier redraw is alive but exactly what the report source keeps by design — the two newest Report Versions (versionsKept) with the Window last served under each, and the reports the Change Highlight compares: each published less than ChangeHighlightDuration before the newest, and the one before them
    [InlineData(1_000, new[] { 11, 12 })] // a highlight apart: the newest, and the one before it — its marks' baseline, and the version kept beside it
    [InlineData(250, new[] { 8, 9, 10, 11, 12 })] // at /pivot-live's RedrawInterval: the four published within the second a mark lasts, and the one before them
    public async Task ADR0160_ADR0153_LV22_after_live_redraws_only_what_the_report_source_keeps_is_alive(int spacingMs, int[] reportsKept)
    {
        var trades = Generate(2_000);
        var data = PivotSource.From(trades, TradeFields, Whole);
        var cut = RenderPivot(PnlByRegionAndDesk, ps => LiveSettings(ps.Add(p => p.Slicing, Whole)), source: data);
        var shown = await RunOfRedrawsAsync(cut, AskedSource(cut), data, trades, TimeSpan.FromMilliseconds(spacingMs));
        var newest = shown.Count - 1;
        // The Consumer renders the page again: Blazor's buffer of the render before the newest then
        // names nothing of an earlier Window, so what stays is what the report source keeps.
        cut.Render();

        Collect();

        // The engine's reports, and their cubes: the ones the Change Highlight compares — which
        // include the two newest Report Versions, which versioned operations read — and no other.
        Assert.Equal(reportsKept, Alive(shown.Select(s => s.Report)));
        Assert.Equal(reportsKept, Alive(shown.Select(s => s.Cube)));
        // The Report Versions held (versionsKept, 2 by default), and only the Window on screen.
        Assert.Equal([newest - 1, newest], Alive(shown.Select(s => s.Metadata)));
        Assert.Equal([newest], Alive(shown.Select(s => s.State)));
        // The display rows: the Window on screen, and every row of the Window last served under the
        // Report Version before it — a delta's baseline (ADR-0152) — and of earlier Windows only the
        // rows those two share with them (ADR-0153).
        Assert.True(shown[newest].Rows.All(static row => row.IsAlive) && shown[newest - 1].Rows.All(static row => row.IsAlive),
            "the Window on screen, and the one the Report Version before it was served, are held");
        Assert.Empty(AliveBeyond(shown, newest, newest, newest - 1));
    }

    [Fact] // ADR-0160 / LV-22, ADR-0153: the grid holds no display row of an earlier Window — over the wire, as /pivot-live's server pivot, where the rows on screen are ExPivot's own copies that nothing of the server's holds, no row of an earlier Window is alive but the ones Blazor keeps for the render before the newest, and none once the pivot renders again
    public async Task ADR0160_LV22_the_grid_holds_no_display_row_of_an_earlier_window()
    {
        var trades = Generate(2_000);
        var data = PivotSource.From(trades, TradeFields, Whole);
        await using var server = PivotReportSource.From(data, timeProvider: Clock, slicing: Whole);
        // Every Window crosses JSON, both ways.
        var remote = PivotReportSource.Fetch(server.Fields, server.Features, server.UpdateMode,
            async (request, ct) => PivotReportJson.Read<PivotReportUpdate>(PivotReportJson.Write(
                await server.WindowAsync(PivotReportJson.Read<PivotReportRequest>(PivotReportJson.Write(request)), ct))));
        server.Changed += change => remote.NotifyChanged(change.SourceVersion);
        var cut = RenderPivot(PnlByRegionAndDesk, LiveSettings, reportSource: remote);
        var shown = await RunOfRedrawsAsync(cut, server, data, trades, TimeSpan.FromSeconds(1));
        var newest = shown.Count - 1;

        Collect();

        // The server keeps its reports as the local source does; ExPivot keeps one Window, and one
        // Report Version's metadata: the ones on screen.
        Assert.Equal([newest - 1, newest], Alive(shown.Select(s => s.Report)));
        Assert.Equal([newest - 1, newest], Alive(shown.Select(s => s.Cube)));
        Assert.Equal([newest], Alive(shown.Select(s => s.Metadata)));
        Assert.Equal([newest], Alive(shown.Select(s => s.State)));
        Assert.True(shown[newest].Rows.All(static row => row.IsAlive), "the Window on screen is held");
        // Blazor keeps the render before the newest as the buffer it renders into next (ADR-0160):
        // the pivot's grid host names the Window it handed the grid then, and the grid the rows it
        // painted. No row of a Window before that one.
        Assert.Empty(AliveBeyond(shown, newest, newest, newest - 1));

        // The Consumer renders the page again, and the grid takes its parameters again.
        cut.Render();
        Collect();

        Assert.Empty(AliveBeyond(shown, newest, newest));
    }
}
