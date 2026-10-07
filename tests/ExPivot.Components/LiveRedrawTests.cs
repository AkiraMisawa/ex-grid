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
/// A live redraw made from the last (ADR-0161) on <c>/pivot-live</c>'s generator: P&amp;L by region
/// and desk across products, its trades amended a few at a time and handed to the bundled source as
/// Change Batches. The rows that render per redraw are exactly the painted rows whose painted text
/// changed, and no row component is built for a key painted before (PV-45, PV-42); after a run of
/// redraws no report but the one on screen is alive (LV-22's ExPivot half). The clock is the test's.
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

    private static List<IRenderedComponent<ExGridRow<PivotReportRow>>> Painted(IRenderedComponent<PivotComponent> cut)
        => [.. cut.FindComponents<ExGridRow<PivotReportRow>>().Where(row => !row.Instance.Placeholder)];

    /// <summary>A row as it paints: its labels, then each value cell's text.</summary>
    private static string Painted(PivotReport report, PivotReportRow row)
        => string.Join(" | ", row.Labels.Select(label => label.Text ?? ""))
            + " || " + string.Join(" | ", Enumerable.Range(0, report.ValueColumns.Count).Select(j => report.ValueAt(row, j)?.Text ?? ""));

    [Fact] // ADR-0161/0003 (PV-45, PV-42): on /pivot-live's generator, the rows that render per live redraw are exactly the painted rows whose painted text changed, and no row component is built for a key painted before
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
            var previous = cut.Instance.Report!;
            var before = Painted(cut).ToDictionary(row => (object)row.Instance, row => row.RenderCount, ReferenceEqualityComparer.Instance);

            await cut.InvokeAsync(() => source.Apply(TradeFields.Batch(changed: Amend(trades, 3, random))));
            cut.WaitForAssertion(() => Assert.NotSame(previous, cut.Instance.Report));

            var report = cut.Instance.Report!;
            foreach (var row in Painted(cut))
            {
                var key = row.Instance.Row.Key;
                var changed = Painted(report, row.Instance.Row) != Painted(previous, previous.RowFor(key)!);
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

    [Fact] // ADR-0161 (PV-44): a redraw from the last shares rows, and a source handed in afresh — a refresh, though its data be the same — shares none, nor does a layout the user changes
    public async Task A_new_source_or_a_new_layout_shares_no_row()
    {
        var trades = Generate(2_000);
        var source = PivotSource.From(trades, TradeFields, Whole);
        var cut = RenderPivot(PnlByRegionAndDesk, ps => ps.Add(p => p.Slicing, Whole), source: source);
        var first = cut.Instance.Report!;

        await cut.InvokeAsync(() => source.Apply(TradeFields.Batch(changed: Amend(trades, 3, new Random(5)))));
        cut.WaitForAssertion(() => Assert.NotSame(first, cut.Instance.Report));
        var redrawn = cut.Instance.Report!;
        Assert.True(redrawn.WasMadeFrom(first));
        Assert.Contains(redrawn.Rows, row => first.Rows.Contains(row));

        cut.Render(ps => ps.Add(p => p.Source, PivotSource.From(trades, TradeFields, Whole)));
        cut.WaitForAssertion(() => Assert.NotSame(redrawn, cut.Instance.Report));
        var refreshed = cut.Instance.Report!;
        Assert.DoesNotContain(refreshed.Rows, row => redrawn.Rows.Contains(row));

        cut.Render(ps => ps.Add(p => p.Layout, PnlByRegionAndDesk with { Form = PivotReportForm.Tabular }));
        cut.WaitForAssertion(() => Assert.NotSame(refreshed, cut.Instance.Report));
        Assert.DoesNotContain(cut.Instance.Report!.Rows, row => refreshed.Rows.Contains(row));
    }

    [Fact] // ADR-0160/0161 (LV-22): after a run of live redraws, no report but the one on screen is alive
    public async Task After_a_run_of_redraws_no_report_but_the_one_on_screen_is_alive()
    {
        var trades = Generate(2_000);
        var source = PivotSource.From(trades, TradeFields, Whole);
        var cut = RenderPivot(PnlByRegionAndDesk, ps => ps.Add(p => p.Slicing, Whole), source: source);
        var reports = new List<WeakReference<PivotReport>> { Weakly(cut) };
        var random = new Random(20261002);

        for (var redraw = 0; redraw < 12; redraw++)
        {
            Clock.Advance(TimeSpan.FromMilliseconds(300));
            var previous = reports[^1];
            await cut.InvokeAsync(() => source.Apply(TradeFields.Batch(changed: Amend(trades, 3, random))));
            cut.WaitForAssertion(() => Assert.False(previous.TryGetTarget(out var shown) && ReferenceEquals(shown, cut.Instance.Report)));
            reports.Add(Weakly(cut));
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.Equal([reports.Count - 1], reports.Select((report, at) => (report, at)).Where(p => p.report.TryGetTarget(out _)).Select(p => p.at));
    }

    // Read here, not in the test: a test's own frame may keep what it read alive until it returns.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<PivotReport> Weakly(IRenderedComponent<PivotComponent> cut) => new(cut.Instance.Report!);
}
