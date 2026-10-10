using Bunit;
using ExGrid;
using ExGrid.Components;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// A report that shrinks under the Window the grid was scrolled to (ADR-0151/0153). ExPivot asks
/// its report source for the Window the grid wants. When a layout, a collapse, a filter or newer
/// data lays out a report that ends before that Window does, the Window is clamped into the new
/// report, ending at its last row, as the grid clamps its own slice when its total shrinks, and
/// that Window is asked for and shown. So the grid is never handed rows past the report's row
/// count, and the user sees the new report's last rows. Later questions ask for the clamped
/// Window until the grid asks for another. The same holds for a report ExPivot computes over its
/// Pivot Source and for a server's report source over JSON. The clock is the test's.
/// </summary>
public sealed class ReportShrinkTests : PivotTestContext
{
    /// <summary>A keyed deal, for Change Batches.</summary>
    public sealed record Deal(long Id, string Region, string Product, decimal Amount, bool Online);

    private static readonly PivotFields<Deal> DealFields = PivotFields.Of<Deal>()
        .Key("Id", d => d.Id)
        .Text("Region", d => d.Region)
        .Text("Product", d => d.Product)
        .Number("Amount", d => d.Amount)
        .Boolean("Online", d => d.Online);

    // 998 deals, each in a Region of its own, every tenth one Apples. Under Online and Region they
    // make a report of 1,001 rows: FALSE, its 499 Regions, TRUE, its 499 Regions, and the Grand
    // Total. Row 500 is TRUE's.
    private static Deal[] Deals() => [.. Enumerable.Range(0, 998).Select(i =>
        new Deal(i, $"Region {i:D4}", i % 10 == 0 ? "Apples" : "Pears", i, i % 2 == 0))];

    private static readonly PivotLayout ByOnlineAndRegion = new()
    {
        Filters = [P("Product")],
        Rows = [P("Online"), P("Region")],
        Values = [Sum("Amount")],
    };

    private const double RowHeightPx = 20;

    // Built whole, without yielding: what is under test is the Window, not the slicing.
    private static readonly PivotSlicing Whole = new() { Budget = TimeSpan.FromDays(1) };

    private static readonly string Expanded = PivotToggleButton.GlyphFor(collapsed: false);
    private static readonly string Collapsed = PivotToggleButton.GlyphFor(collapsed: true);

    /// <summary>What shrinks the report.</summary>
    public enum Shrink
    {
        /// <summary>The Consumer hands in a layout of Online alone.</summary>
        Layout,

        /// <summary>The user collapses the Online field from the report's Context Menu.</summary>
        Collapse,

        /// <summary>The user hides Pears in the report filter band.</summary>
        Filter,

        /// <summary>A Change Batch removes every deal from the hundredth on.</summary>
        LiveRemoval,
    }

    /// <summary>Who computes the report.</summary>
    public enum Computed
    {
        /// <summary>ExPivot, over its Pivot Source (<c>Source</c>).</summary>
        Locally,

        /// <summary>A server's report source, asked over JSON through <c>PivotReportSource.Fetch</c>.</summary>
        OnAServer,
    }

    /// <summary>The rows each shrink leaves, as the report paints them, top to bottom.</summary>
    private static string[] RowsAfter(Shrink shrink, Deal[] deals) => shrink switch
    {
        Shrink.Layout => ["FALSE | 249001", "TRUE | 248502", "Grand Total | 497503"],
        Shrink.Collapse => [$"{Collapsed}FALSE | 249001", $"{Collapsed}TRUE | 248502", "Grand Total | 497503"],
        // Apples are the deals whose number ends in 0, all of them online.
        Shrink.Filter => ByOnlineAndRegionRows([.. deals.Where(d => d.Product == "Apples")]),
        Shrink.LiveRemoval => ByOnlineAndRegionRows([.. deals.Where(d => d.Id < 100)]),
        _ => throw new ArgumentOutOfRangeException(nameof(shrink)),
    };

    /// <summary>The rows of <see cref="ByOnlineAndRegion"/> over <paramref name="deals"/>, as the
    /// report paints them: each Online Item's group row with its subtotal, its Regions in label
    /// order, and the Grand Total.</summary>
    private static string[] ByOnlineAndRegionRows(IReadOnlyCollection<Deal> deals) =>
    [
        .. deals.GroupBy(d => d.Online).OrderBy(group => group.Key).SelectMany(group => (IEnumerable<string>)
        [
            $"{Expanded}{(group.Key ? "TRUE" : "FALSE")} | {Text(group.Sum(d => d.Amount))}",
            .. group.OrderBy(d => d.Region, StringComparer.Ordinal).Select(d => $"{d.Region} | {Text(d.Amount)}"),
        ]),
        $"Grand Total | {Text(deals.Sum(d => d.Amount))}",
    ];

    private static string Text(decimal amount) => amount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A server's report source over the data, asked over JSON as a transport would ask
    /// it, its notices passed on, and every Window it is asked for recorded — each answered once
    /// <paramref name="answerWhen"/> says, when given.</summary>
    private static (FetchingPivotReportSource Remote, LocalPivotReportSource Server) OverJson(
        SnapshotPivotSource data, TimeProvider clock, List<PivotReportRequest> asked,
        Func<PivotReportRequest, Task>? answerWhen = null)
    {
        var server = PivotReportSource.From(data, timeProvider: clock, slicing: Whole);
        static T Wire<T>(T value) => PivotReportJson.Read<T>(PivotReportJson.Write(value));
        var remote = PivotReportSource.Fetch(server.Fields, server.Features, server.UpdateMode,
            async (request, ct) =>
            {
                lock (asked)
                    asked.Add(request);
                if (answerWhen is not null)
                    await answerWhen(request).WaitAsync(ct);
                return Wire(await server.WindowAsync(Wire(request), ct));
            },
            items: server.RawItemsAsync,
            reportItems: async (query, ct) => Wire(await server.ItemsAsync(Wire(query), ct)));
        data.Changed += change => remote.NotifyChanged(change.SourceVersion);
        return (remote, server);
    }

    private IRenderedComponent<PivotComponent> RenderOver(SnapshotPivotSource data, Computed computed,
        List<PivotReportRequest> asked, out LocalPivotReportSource? server)
    {
        Action<ComponentParameterCollectionBuilder<PivotComponent>> parameters = ps => ps
            .Add(p => p.RowHeight, RowHeightPx)
            .Add(p => p.ShowFieldList, false)
            .Add(p => p.Slicing, Whole);
        if (computed == Computed.Locally)
        {
            server = null;
            return RenderPivot(ByOnlineAndRegion, parameters, source: data);
        }
        var (remote, local) = OverJson(data, Clock, asked);
        server = local;
        return RenderPivot(ByOnlineAndRegion, parameters, reportSource: remote);
    }

    /// <summary>Scrolls the report down to row 500, as a user does, and waits for the grid to
    /// paint it: TRUE's group row at the top.</summary>
    private async Task ScrolledTo500Async(IRenderedComponent<PivotComponent> cut)
    {
        cut.WaitForAssertion(() => Assert.Equal(1001, Grid(cut).Instance.TotalCount));
        await ScrollReportAsync(cut, 500 * RowHeightPx);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(500, Grid(cut).Instance.WindowStart);
            Assert.Equal($"{Expanded}TRUE | 248502", RowTexts(cut)[0]);
        });
    }

    private async Task ShrinkAsync(IRenderedComponent<PivotComponent> cut, Shrink shrink, SnapshotPivotSource data, Deal[] deals)
    {
        switch (shrink)
        {
            case Shrink.Layout:
                cut.Render(ps => ps.Add(p => p.Layout, ByOnlineAndRegion with { Rows = [P("Online")] }));
                break;
            case Shrink.Collapse:
                // On TRUE's group row, at the top of the Window: Collapse Entire Field.
                var collapse = ContextCommands(cut, 0, Grid(cut).Instance.Columns[0].Name)
                    .Single(command => command.Id == PivotCommandIds.CollapseField);
                await cut.InvokeAsync(collapse.Invoke);
                break;
            case Shrink.Filter:
                await cut.InvokeAsync(() => cut.Find(".ex-pivot-toolbar .ex-pivot-filter-button").ClickAsync(new MouseEventArgs()));
                cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".ex-pivot-item"), i => i.TextContent.Trim() == "Pears"));
                await cut.InvokeAsync(() => cut.FindAll(".ex-pivot-item").Single(i => i.TextContent.Trim() == "Pears")
                    .QuerySelector("input")!.ChangeAsync(new ChangeEventArgs { Value = false }));
                await cut.InvokeAsync(() => cut.Find(".ex-pivot-ok").ClickAsync(new MouseEventArgs()));
                break;
            case Shrink.LiveRemoval:
                await cut.InvokeAsync(() => data.Apply(DealFields.Batch(
                    removedKeys: deals.Where(d => d.Id >= 100).Select(d => (object)d.Id))));
                break;
        }
    }

    [Theory] // ADR-0151/0153: a report that shrinks below the Window scrolled to is shown at its last rows, the Window clamped into it — whatever shrinks it, and whoever computes it
    [InlineData(Shrink.Layout, Computed.Locally)]
    [InlineData(Shrink.Layout, Computed.OnAServer)]
    [InlineData(Shrink.Collapse, Computed.Locally)]
    [InlineData(Shrink.Collapse, Computed.OnAServer)]
    [InlineData(Shrink.Filter, Computed.Locally)]
    [InlineData(Shrink.Filter, Computed.OnAServer)]
    [InlineData(Shrink.LiveRemoval, Computed.Locally)]
    [InlineData(Shrink.LiveRemoval, Computed.OnAServer)]
    public async Task ADR0151_ADR0153_A_report_that_shrinks_below_the_Window_scrolled_to_shows_its_last_rows(Shrink shrink, Computed computed)
    {
        var deals = Deals();
        var data = PivotSource.From(deals, DealFields, Whole);
        var asked = new List<PivotReportRequest>();
        var cut = RenderOver(data, computed, asked, out var server);
        try
        {
            await ScrolledTo500Async(cut);

            await ShrinkAsync(cut, shrink, data, deals);

            var expected = RowsAfter(shrink, deals);
            cut.WaitForAssertion(() => Assert.Equal(expected.Length, Grid(cut).Instance.TotalCount));
            // The grid is handed a Window inside the new report: it starts within the row count and
            // claims no row past it.
            var grid = Grid(cut).Instance;
            Assert.InRange(grid.WindowStart, 0, expected.Length - 1);
            Assert.True(grid.WindowStart + grid.Window.Count <= expected.Length,
                $"the Window claims rows {grid.WindowStart}-{grid.WindowStart + grid.Window.Count - 1} of {expected.Length}");
            // And the user sees rows of the new report: its last ones, the grid's slice clamped to
            // the end as the Window is, none of them a Placeholder.
            cut.WaitForAssertion(() =>
            {
                var painted = RowTexts(cut);
                Assert.NotEmpty(painted);
                Assert.Equal(expected[^painted.Length..], painted);
            });
            Assert.False(Renderer.UnhandledException.IsCompleted, "an exception reached the renderer");
            Assert.False(cut.Instance.IsStale);
            Assert.Null(cut.Instance.LastError);
        }
        finally
        {
            if (server is not null)
                await server.DisposeAsync();
        }
    }

    [Fact] // ADR-0151/0066: a report that now ends inside the Window is asked for again, clamped, in the same question; the report on screen stays until that lands, never handed to the grid short of rows
    public async Task ADR0151_A_report_that_ends_inside_the_Window_stays_on_screen_until_its_clamped_Window_lands()
    {
        var deals = Deals();
        var data = PivotSource.From(deals, DealFields, Whole);
        var asked = new List<PivotReportRequest>();
        TaskCompletionSource? hold = null;
        // Once the test holds, a Window anywhere but row 500 waits to be answered.
        var (remote, server) = OverJson(data, Clock, asked,
            request => hold is { } held && request.Window.Start != 500 ? held.Task : Task.CompletedTask);
        await using var _ = server;
        var cut = RenderPivot(ByOnlineAndRegion, ps => ps.Add(p => p.RowHeight, RowHeightPx).Add(p => p.ShowFieldList, false),
            reportSource: remote);
        await ScrolledTo500Async(cut);
        var height = Grid(cut).Instance.Window.Count;

        // Every deal from the 500th on goes: 503 rows, which end inside the Window at row 500.
        hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var left = deals.Where(d => d.Id < 500).ToArray();
        await cut.InvokeAsync(() => data.Apply(DealFields.Batch(removedKeys: deals.Where(d => d.Id >= 500).Select(d => (object)d.Id))));

        // The question asks where the grid stands, then for the Window clamped into the new report,
        // which is held: until it lands, the report on screen stays as it was.
        var clamped = new PivotReportWindow(503 - height, height);
        cut.WaitForAssertion(() => Assert.Equal(clamped, asked[^1].Window));
        Assert.Equal(new PivotReportWindow(500, height), asked[^2].Window);
        Assert.Equal(1001, Grid(cut).Instance.TotalCount);
        Assert.Equal(500, Grid(cut).Instance.WindowStart);
        Assert.Equal($"{Expanded}TRUE | 248502", RowTexts(cut)[0]);

        hold.SetResult();
        cut.WaitForAssertion(() => Assert.Equal(503, Grid(cut).Instance.TotalCount));
        Assert.Equal(clamped.Start, Grid(cut).Instance.WindowStart);
        var expected = ByOnlineAndRegionRows(left);
        Assert.Equal(503, expected.Length);
        cut.WaitForAssertion(() =>
        {
            var painted = RowTexts(cut);
            Assert.Equal(height, painted.Length);
            Assert.Equal(expected[^height..], painted);
        });
        Assert.False(Renderer.UnhandledException.IsCompleted, "an exception reached the renderer");
    }

    [Fact] // ADR-0151: after a shrink, later questions ask for the Window clamped into the report, until the grid asks for another — and the report scrolls on
    public async Task ADR0151_After_a_shrink_later_questions_ask_for_the_clamped_Window_until_the_grid_asks_for_another()
    {
        var deals = Deals();
        var data = PivotSource.From(deals, DealFields, Whole);
        var asked = new List<PivotReportRequest>();
        var cut = RenderOver(data, Computed.OnAServer, asked, out var server);
        try
        {
            await ScrolledTo500Async(cut);
            var height = Grid(cut).Instance.Window.Count;
            Assert.Equal(new PivotReportWindow(500, height), asked[^1].Window);

            // The Consumer hides Pears: 102 rows, which end before row 500.
            var applesOnly = ByOnlineAndRegion with { Filters = [P("Product") with { HiddenItems = [PivotItemKey.Text("Pears")] }] };
            cut.Render(ps => ps.Add(p => p.Layout, applesOnly));
            var expected = RowsAfter(Shrink.Filter, deals);
            cut.WaitForAssertion(() => Assert.Equal(expected.Length, Grid(cut).Instance.TotalCount));

            // Asked where the grid stood, then clamped into the report, ending at its last row.
            var clamped = new PivotReportWindow(expected.Length - height, height);
            Assert.Equal(new PivotReportWindow(500, height), asked[^2].Window);
            Assert.Equal(clamped, asked[^1].Window);
            Assert.Equal(clamped.Start, Grid(cut).Instance.WindowStart);
            cut.WaitForAssertion(() => Assert.Equal(expected[^RowTexts(cut).Length..], RowTexts(cut)));

            // The next question — the source says its data moved on — asks for the clamped Window,
            // not the one the report no longer reaches.
            var before = asked.Count;
            await cut.InvokeAsync(() => data.Apply(DealFields.Batch(changed: [deals[990] with { Amount = 991m }])));
            cut.WaitForAssertion(() => Assert.Equal("Grand Total | 49501", RowTexts(cut)[^1]));
            Assert.True(asked.Count > before);
            Assert.All(asked.Skip(before), request => Assert.Equal(clamped, request.Window));

            // Scrolled back to the top, the grid asks for the rows it shows there, and that is
            // what is asked: the report scrolls on.
            await ScrollReportAsync(cut, 0);
            cut.WaitForAssertion(() =>
            {
                Assert.Equal(0, Grid(cut).Instance.WindowStart);
                var painted = RowTexts(cut);
                Assert.Equal([$"{Expanded}TRUE | 49501", .. expected[1..painted.Length]], painted);
            });
            Assert.Equal(new PivotReportWindow(0, height), asked[^1].Window);
            Assert.False(Renderer.UnhandledException.IsCompleted, "an exception reached the renderer");
        }
        finally
        {
            await server!.DisposeAsync();
        }
    }

    [Fact] // ADR-0151: a report that shrinks again while the clamped Window is asked for is clamped again, and shown at its last rows
    public async Task ADR0151_A_report_that_shrinks_again_while_the_clamped_Window_is_asked_is_clamped_again()
    {
        var asked = new List<PivotReportRequest>();
        var byRegion = new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] };
        var byProduct = new PivotLayout { Rows = [P("Product")], Values = [Sum("Amount")] };
        // A server whose report has 1,001 rows by Region. By Product its data shrinks under the
        // question: 300 rows the first time it is asked, 100 every time after.
        var askedByProduct = 0;
        var source = PivotReportSource.Fetch(Fields, PivotSourceFeatures.All, PivotReportUpdateMode.Incremental,
            (request, _) =>
            {
                asked.Add(request);
                var field = request.Layout.Rows[0].Field;
                var total = field == "Region" ? 1001 : askedByProduct++ == 0 ? 300 : 100;
                var metadata = new PivotReportMetadata(new($"report-{field}-{total}"), $"source-{total}", $"order-{field}-{total}",
                    request.Layout, request.Settings, total, [new("label", "Label", field)],
                    [new("value", "Sum of Amount", PivotColumnRole.Item, 0, [])], [], 0, ["Sum of Amount"]);
                var count = Math.Min(request.Window.Count, Math.Max(0, total - request.Window.Start));
                var rows = Enumerable.Range(request.Window.Start, count).Select(i =>
                    new PivotDisplayRow(new(PivotRowRole.Item, -1, [PivotItemKey.Text($"R {i}")]),
                        PivotRowRole.Item, -1, true, [new($"R {i}")], [new(i, i, null, i.ToString(System.Globalization.CultureInfo.InvariantCulture))],
                        [new(field, PivotItemKey.Text($"R {i}"))])).ToArray();
                return ValueTask.FromResult(PivotReportUpdate.Complete(request, metadata, rows));
            }, items: (query, _) => ValueTask.FromResult(new PivotItemPage(query.SourceVersion, [], 0)));
        var cut = RenderPivot(byRegion, ps => ps.Add(p => p.RowHeight, RowHeightPx).Add(p => p.ShowFieldList, false),
            reportSource: source);
        cut.WaitForAssertion(() => Assert.Equal(1001, Grid(cut).Instance.TotalCount));
        await ScrollReportAsync(cut, 500 * RowHeightPx);
        cut.WaitForAssertion(() => Assert.Equal("R 500 | 500", RowTexts(cut)[0]));
        var height = Grid(cut).Instance.Window.Count;

        cut.Render(ps => ps.Add(p => p.Layout, byProduct));

        cut.WaitForAssertion(() => Assert.Equal(100, Grid(cut).Instance.TotalCount));
        // Asked where the grid stood; clamped into the 300 rows that answered; clamped again into
        // the 100 that answered that.
        Assert.Equal(
            [new PivotReportWindow(500, height), new PivotReportWindow(300 - height, height), new PivotReportWindow(100 - height, height)],
            asked.Where(request => request.Layout.Rows[0].Field == "Product").Select(request => request.Window));
        Assert.Equal(100 - height, Grid(cut).Instance.WindowStart);
        Assert.Equal(height, Grid(cut).Instance.Window.Count);
        cut.WaitForAssertion(() =>
        {
            var painted = RowTexts(cut);
            Assert.NotEmpty(painted);
            Assert.Equal(Enumerable.Range(100 - painted.Length, painted.Length).Select(i => $"R {i} | {i}"), painted);
        });
        Assert.False(Renderer.UnhandledException.IsCompleted, "an exception reached the renderer");
    }
}
