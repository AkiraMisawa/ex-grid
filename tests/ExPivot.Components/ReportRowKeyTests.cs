using System.Globalization;
using Bunit;
using ExGrid.Components;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// ExPivot's Row Key for its report grid (ADR-0140, PV-42): a report row is named by what it stands
/// for — its role, its Value Field and its Items, the pairing <c>ReportHistory</c> makes — so a live
/// redraw repaints the rows that changed in place, and builds no row component for a key it painted
/// before. The data is <c>/pivot-live</c>'s shape: keyed trades amended a few at a time and handed
/// to the bundled source as Change Batches, P&amp;L by region and desk across products. The clock is
/// the test's.
/// </summary>
public class ReportRowKeyTests : PivotTestContext
{
    /// <summary>A keyed trade, for Change Batches.</summary>
    public sealed record Trade(long Id, string Region, string Desk, string Product, decimal Pnl);

    private static readonly PivotFields<Trade> TradeFields = PivotFields.Of<Trade>()
        .Key("Id", t => t.Id)
        .Text("Region", t => t.Region)
        .Text("Desk", t => t.Desk)
        .Text("Product", t => t.Product)
        .Number("Pnl", t => t.Pnl);

    private static readonly string[] Regions = ["Americas", "Asia", "Europe"];
    private static readonly string[] Desks = ["Credit", "Rates"];
    private static readonly string[] Products = ["Bond", "Swap", "Option"];

    private static readonly PivotLayout PnlByRegionAndDesk = new()
    {
        Rows = [P("Region"), P("Desk")],
        Columns = [P("Product")],
        Values = [Sum("Pnl")],
    };

    // The report is built whole, without yielding: what is under test is the grid's rows.
    private static readonly PivotSlicing Whole = new() { Budget = TimeSpan.FromDays(1) };

    private static Trade[] Trades()
    {
        var trades = new List<Trade>();
        var id = 1L;
        foreach (var region in Regions)
        {
            foreach (var desk in Desks)
            {
                foreach (var product in Products)
                    trades.Add(new Trade(id, region, desk, product, id++ * 10m));
            }
        }
        return [.. trades];
    }

    /// <summary>Every painted report row component, by the key of the row it paints.</summary>
    private static Dictionary<PivotRowKey, ExGridRow<PivotReportRow>> PaintedByKey(IRenderedComponent<PivotComponent> cut)
        => cut.FindComponents<ExGridRow<PivotReportRow>>()
            .Select(row => row.Instance)
            .ToDictionary(row => row.Row.Key);

    // Not Assert.Same: a failure would print the components, and a row component's CellTextMetrics
    // cannot be printed (its Bold is another CellTextMetrics, so the record's ToString never ends).
    private static void SameComponent(ExGridRow<PivotReportRow> expected, ExGridRow<PivotReportRow> actual)
        => Assert.True(ReferenceEquals(expected, actual), $"the row component at {expected.RowIndex} was built again");

    [Fact] // ADR-0140 / PV-42: across live redraws, no row component is built for a key already painted, and the changed rows repaint in place
    public async Task Live_redraws_build_no_row_component_for_a_key_already_painted()
    {
        var trades = Trades();
        var source = PivotSource.From(trades, TradeFields, Whole);
        var cut = RenderPivot(PnlByRegionAndDesk, source: source);
        var painted = PaintedByKey(cut);
        Assert.True(painted.Count > 6, $"{painted.Count} rows painted");
        var built = new HashSet<ExGridRow<PivotReportRow>>(painted.Values, ReferenceEqualityComparer.Instance);
        var random = new Random(20261006);

        for (var redraw = 0; redraw < 12; redraw++)
        {
            // A few trades amended, as /pivot-live's generator amends them, after a quiet interval:
            // the first change after one is redrawn at once (PV-35).
            Clock.Advance(TimeSpan.FromSeconds(1));
            var report = cut.Instance.Report;
            var amended = new List<Trade>();
            foreach (var at in Enumerable.Range(0, trades.Length).OrderBy(_ => random.Next()).Take(3))
                amended.Add(trades[at] = trades[at] with { Pnl = trades[at].Pnl + random.Next(1, 50) });
            await cut.InvokeAsync(() => source.Apply(TradeFields.Batch(changed: amended)));
            cut.WaitForAssertion(() => Assert.NotSame(report, cut.Instance.Report));

            foreach (var (key, row) in PaintedByKey(cut))
            {
                if (painted.TryGetValue(key, out var before))
                    SameComponent(before, row);
                else
                    painted[key] = row;
                built.Add(row);
            }
        }

        // One component per key ever painted, and the values on screen are the newest.
        Assert.Equal(painted.Count, built.Count);
        Assert.EndsWith(" | " + trades.Sum(t => t.Pnl).ToString(CultureInfo.InvariantCulture), RowTexts(cut)[^1]);
    }

    [Fact] // ADR-0140 / PV-42: a redraw that changes the rows keeps the component of every key it painted before, and builds one only for a new key
    public async Task A_redraw_that_adds_an_item_keeps_every_painted_keys_component()
    {
        var trades = Trades();
        var source = PivotSource.From(trades, TradeFields, Whole);
        var cut = RenderPivot(PnlByRegionAndDesk, source: source);
        var before = PaintedByKey(cut);
        var version = Grid(cut).Instance.RowSequenceVersion;
        var report = cut.Instance.Report;

        // A new desk under Asia: rows below it move down, and its rows are new.
        await cut.InvokeAsync(() => source.Apply(TradeFields.Batch(added: [new Trade(999, "Asia", "FX", "Swap", 5m)])));
        cut.WaitForAssertion(() => Assert.NotSame(report, cut.Instance.Report));

        Assert.NotEqual(version, Grid(cut).Instance.RowSequenceVersion);
        var after = PaintedByKey(cut);
        var kept = 0;
        foreach (var (key, row) in after)
        {
            if (before.TryGetValue(key, out var previous))
            {
                SameComponent(previous, row);
                kept++;
            }
        }
        Assert.Equal(before.Count, kept);
        Assert.Contains(after.Keys, key => !before.ContainsKey(key));
    }

    [Fact] // ADR-0140 / PV-42: the report grid is handed a Row Key, and a report row's key equals the key of the row standing for the same thing in the next report
    public async Task The_report_grid_is_handed_a_value_equal_row_key()
    {
        var trades = Trades();
        var source = PivotSource.From(trades, TradeFields, Whole);
        var cut = RenderPivot(PnlByRegionAndDesk, source: source);
        var key = Grid(cut).Instance.RowKey;
        Assert.NotNull(key);
        var first = cut.Instance.Report!;

        await cut.InvokeAsync(() => source.Apply(TradeFields.Batch(changed: [trades[0] with { Pnl = 1m }])));
        cut.WaitForAssertion(() => Assert.NotSame(first, cut.Instance.Report));
        var second = cut.Instance.Report!;

        Assert.Equal(first.Rows.Count, second.Rows.Count);
        var shared = 0;
        for (var i = 0; i < first.Rows.Count; i++)
        {
            Assert.Equal(key(first.Rows[i]), key(second.Rows[i]));
            if (ReferenceEquals(first.Rows[i], second.Rows[i]))
                shared++;
        }
        // ADR-0161: the next report shares the rows whose painted text did not change, and makes the
        // others anew — one trade's region, desk and grand total.
        Assert.Equal(first.Rows.Count - 3, shared);
        Assert.Equal(first.Rows.Count, first.Rows.Select(row => key(row)).Distinct().Count());
    }

    [Fact] // ADR-0141 / LV-10: ExPivot vouches that its report holds no Row Key twice, so the grid does not walk a redrawn report whole
    public void The_report_grid_is_vouched_for()
    {
        var cut = RenderPivot(PnlByRegionAndDesk, source: PivotSource.From(Trades(), TradeFields, Whole));

        Assert.NotNull(Grid(cut).Instance.RowKey);
        Assert.True(Grid(cut).Instance.VouchesDistinctRows);
    }
}
