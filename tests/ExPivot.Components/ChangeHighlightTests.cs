using Bunit;
using ExGrid;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// The report's Change Highlight (ADR-0067/0068): ExPivot answers the grid's <c>CellChangedAt</c>
/// by comparing the painted text of each value cell with the same cell — the same row Items, column
/// Items and Value Field — in the reports of the recent data versions. Only data marks a cell; every
/// cell of a row that appears is marked; a change the number format hides is not. The delegate stays stable; immutable display rows carry each change. The clock is the test's.
/// </summary>
public class ChangeHighlightTests : PivotTestContext
{
    private static readonly PivotLayout RegionAmount = new() { Rows = [P("Region")], Values = [Sum("Amount")] };

    private static readonly PivotLayout RegionProduct = new() { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] };

    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);

    private static Sale[] EastApples(decimal amount) => [Sales[0] with { Amount = amount }, .. Sales[1..]];

    private static Task PublishAsync(IRenderedComponent<PivotComponent> cut, LiveSource source, IReadOnlyList<Sale> records)
        => cut.InvokeAsync(() => source.Publish(records));

    /// <summary>The painted texts of the cells the grid marks, in the order it paints them.</summary>
    internal static string[] MarkedTexts(IRenderedComponent<PivotComponent> cut)
        => cut.FindAll(".ex-pivot-sheet > .ex-grid .ex-viewport .ex-row .ex-changed").Select(c => c.TextContent.Trim()).ToArray();

    /// <summary>What ExPivot answers the grid for a painted cell, by its row and column.</summary>
    private static DateTimeOffset? ChangedAt(IRenderedComponent<PivotComponent> cut, int row, int column)
    {
        var grid = Grid(cut).Instance;
        return grid.CellChangedAt?.Invoke(grid.Window[row], grid.Columns[column]);
    }

    // ---- What marks a cell ------------------------------------------------------------------

    [Fact] // ADR-0067/0068 (PV-36): a value cell whose painted text changed with the data is marked, at the time its answer arrived; the others are not
    public async Task A_value_whose_painted_text_changed_is_marked()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);
        Assert.Empty(MarkedTexts(cut));
        Assert.Empty(MarkedTexts(cut));
        Clock.Advance(TimeSpan.FromSeconds(3));
        var at = Clock.GetUtcNow();

        await PublishAsync(cut, source, EastApples(101));

        Assert.Equal(at, ChangedAt(cut, 0, 1));
        Assert.Null(ChangedAt(cut, 1, 1));
        Assert.Null(ChangedAt(cut, 2, 1));
        Assert.Null(ChangedAt(cut, 3, 1));
        Assert.Equal(at, ChangedAt(cut, 4, 1));
        Assert.Equal(["181", "286"], MarkedTexts(cut));
        // A label cell paints no value that could change, and is not asked.
        Assert.Null(ChangedAt(cut, 0, 0));
        Assert.Same(Clock, Grid(cut).Instance.Clock);
        Assert.Equal(TimeSpan.FromSeconds(1), Grid(cut).Instance.ChangeHighlightDuration);
    }

    [Fact] // ADR-0153/0068: the local report uses the clock currently handed to its component
    public async Task Replacing_the_clock_dates_new_changes_on_that_clock()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);
        var nextClock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(Clock.GetUtcNow().AddDays(1));
        cut.Render(ps => ps.Add(p => p.Clock, nextClock));
        nextClock.Advance(TimeSpan.FromSeconds(3));
        await PublishAsync(cut, source, EastApples(101));
        Assert.Equal(nextClock.GetUtcNow(), ChangedAt(cut, 0, 1));
        Assert.Equal(["181", "286"], MarkedTexts(cut));
    }

    [Fact] // ADR-0067 (PV-36): the comparison is of the painted text — a change the number format hides is not marked
    public async Task A_change_the_format_hides_is_not_marked()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount with { Values = [Sum("Amount") with { NumberFormat = "0" }] }, source: source);

        await PublishAsync(cut, source, EastApples(100.4m));

        Assert.Equal("East | 180", RowTexts(cut)[0]);
        Assert.Equal(180.4m, ((PivotDisplayValue)Grid(cut).Instance.Columns[1].Value(Grid(cut).Instance.Window[0])!).Exact);
        Assert.Empty(MarkedTexts(cut));
        Assert.All(Enumerable.Range(0, 5), row => Assert.Null(ChangedAt(cut, row, 1)));

        // A change it shows is.
        Clock.Advance(Interval);
        await PublishAsync(cut, source, EastApples(101m));
        Assert.Equal(["181", "286"], MarkedTexts(cut));
    }

    [Fact] // ADR-0067 (PV-36): every cell of a row that appears is marked, its empty cells too; a row that leaves simply goes
    public async Task Every_cell_of_a_row_that_appears_is_marked()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount with { Columns = [P("Online")] }, source: source);
        var at = Clock.GetUtcNow();

        await PublishAsync(cut, source, [.. Sales, new Sale("South", "Apples", 1m, 1, true)]);

        Assert.Equal(["East | 50 | 130 | 180", "North | 10 |  | 10", "South |  | 1 | 1", "West | 70 | 20 | 90", "(blank) |  | 5 | 5",
            "Grand Total | 130 | 156 | 286"], RowTexts(cut));
        Assert.Equal([null, null, null], Enumerable.Range(1, 3).Select(c => ChangedAt(cut, 0, c)));
        Assert.Equal([at, at, at], Enumerable.Range(1, 3).Select(c => ChangedAt(cut, 2, c)));
        Assert.Equal([null, at, at], Enumerable.Range(1, 3).Select(c => ChangedAt(cut, 5, c)));
        Assert.Equal(["", "1", "1", "156", "286"], MarkedTexts(cut));

        // Rows that leave — North, and South again — simply go; the totals they leave are marked.
        Clock.Advance(TimeSpan.FromSeconds(2));
        await PublishAsync(cut, source, Sales.Where(s => s.Region != "North").ToArray());
        Assert.Equal(["East | 50 | 130 | 180", "West | 70 | 20 | 90", "(blank) |  | 5 | 5", "Grand Total | 120 | 155 | 275"], RowTexts(cut));
        Assert.Equal(["120", "155", "275"], MarkedTexts(cut));
    }

    [Fact] // ADR-0067 (PV-36): a column that appears is marked as a row that appears is — its cells had no counterpart before
    public async Task Every_cell_of_a_column_that_appears_is_marked()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount with { Columns = [P("Product")] }, source: source);
        var at = Clock.GetUtcNow();

        await PublishAsync(cut, source, [.. Sales, new Sale("East", "Quinces", 1m, 1, true)]);

        Assert.Equal(["Row Labels", "Apples", "Pears", "Plums", "Quinces", "Grand Total"], HeaderTexts(cut));
        Assert.Equal([null, null, null, at, at], Enumerable.Range(1, 5).Select(c => ChangedAt(cut, 0, c)));
        Assert.Equal([null, null, null, at, null], Enumerable.Range(1, 5).Select(c => ChangedAt(cut, 1, c)));
    }

    [Fact] // ADR-0067/0011 (PV-36): rows a sort by value reorders with the data are compared by what they stand for, not where they stand — only changed text is marked
    public async Task Rows_reordered_by_the_data_are_compared_by_what_they_stand_for()
    {
        var source = new LiveSource();
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region") with { Sort = new PivotSort(PivotSortDirection.Descending, 0) }], Values = [Sum("Amount")] },
            source: source);
        Assert.Equal(["East | 180", "West | 90", "North | 10", "(blank) | 5", "Grand Total | 285"], RowTexts(cut));
        var version = Grid(cut).Instance.RowSequenceVersion;

        // West's Apples: 70 → 200, and West now leads.
        await PublishAsync(cut, source, [.. Sales[..3], Sales[3] with { Amount = 200m }, .. Sales[4..]]);

        Assert.Equal(["West | 220", "East | 180", "North | 10", "(blank) | 5", "Grand Total | 415"], RowTexts(cut));
        Assert.NotEqual(version, Grid(cut).Instance.RowSequenceVersion);
        Assert.Equal(["220", "415"], MarkedTexts(cut));
    }

    [Fact] // ADR-0068 (PV-36): a mark lasts from the version that changed the cell, while later versions change other cells
    public async Task A_mark_lasts_from_the_version_that_changed_the_cell()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);
        var first = Clock.GetUtcNow();
        await PublishAsync(cut, source, EastApples(101));
        Clock.Advance(TimeSpan.FromMilliseconds(300));
        var second = Clock.GetUtcNow();

        // West's Apples: 70 → 71, East unchanged since the first.
        Sale[] both = [.. EastApples(101)[..3], Sales[3] with { Amount = 71m }, .. Sales[4..]];
        await PublishAsync(cut, source, both);

        Assert.Equal(first, ChangedAt(cut, 0, 1));
        Assert.Equal(second, ChangedAt(cut, 2, 1));
        Assert.Equal(second, ChangedAt(cut, 4, 1));
        Assert.Equal(["181", "91", "287"], MarkedTexts(cut));

        // The first one's mark ends a second after it, the second one's a second after that.
        Clock.Advance(TimeSpan.FromMilliseconds(700));
        cut.WaitForAssertion(() => Assert.Equal(["91", "287"], MarkedTexts(cut)));
        Clock.Advance(TimeSpan.FromMilliseconds(300));
        cut.WaitForAssertion(() => Assert.Empty(MarkedTexts(cut)));
    }

    [Fact] // ADR-0068 (PV-36): the duration is the Consumer's — the mark is taken away once it has passed
    public async Task The_duration_is_honoured()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.ChangeHighlightDuration, TimeSpan.FromSeconds(2)), source: source);
        Assert.Equal(TimeSpan.FromSeconds(2), Grid(cut).Instance.ChangeHighlightDuration);

        await PublishAsync(cut, source, EastApples(101));
        Assert.Equal(["181", "286"], MarkedTexts(cut));

        Clock.Advance(TimeSpan.FromMilliseconds(1999));
        cut.WaitForAssertion(() => Assert.Equal(["181", "286"], MarkedTexts(cut)));
        Clock.Advance(TimeSpan.FromMilliseconds(1));
        cut.WaitForAssertion(() => Assert.Empty(MarkedTexts(cut)));
    }

    [Fact] // ADR-0068 (PV-36): a zero duration marks nothing, and hands the grid nothing to ask; a negative one is refused
    public async Task A_zero_duration_marks_nothing()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.ChangeHighlightDuration, TimeSpan.Zero), source: source);

        await PublishAsync(cut, source, EastApples(101));

        Assert.Equal("East | 181", RowTexts(cut)[0]);
        Assert.Empty(MarkedTexts(cut));
        Assert.Empty(MarkedTexts(cut));
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(
            () => cut.Render(ps => ps.Add(p => p.ChangeHighlightDuration, TimeSpan.FromSeconds(-1))));
        Assert.Equal(nameof(PivotComponent.ChangeHighlightDuration), refusal.ParamName);
    }

    // ---- Only data marks a cell -------------------------------------------------------------

    [Theory] // ADR-0067 (PV-36): a new layout, a sort, a collapse, a form, Show Values As or a format marks nothing — the history starts again
    [InlineData("layout")]
    [InlineData("sort")]
    [InlineData("collapse")]
    [InlineData("form")]
    [InlineData("show-values-as")]
    [InlineData("format")]
    public async Task Only_data_marks_a_cell(string gesture)
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionProduct, source: source);
        await PublishAsync(cut, source, EastApples(101));
        Assert.NotEmpty(MarkedTexts(cut));

        switch (gesture)
        {
            case "layout":
                await TickFieldAsync(cut, "Quantity", true);
                break;
            case "sort":
                await OpenMenuAsync(cut, "Rows", "Region");
                await RunMenuAsync(cut, "Sort Z to A");
                break;
            case "collapse":
                await cut.FindAll(".ex-pivot-toggle")[0].ClickAsync(new MouseEventArgs());
                break;
            case "form":
                await cut.Find(".ex-pivot-layout-button").ClickAsync(new MouseEventArgs());
                await RunMenuAsync(cut, "Show in Outline Form");
                break;
            default:
                await OpenMenuAsync(cut, "Values", "Sum of Amount");
                await RunMenuAsync(cut, "Value Field Settings…");
                if (gesture == "show-values-as")
                    await cut.FindAll(".ex-pivot-value-settings select")[1].ChangeAsync(new ChangeEventArgs { Value = "PercentOfGrandTotal" });
                else
                    await cut.FindAll(".ex-pivot-value-settings input[type=text]")[1].InputAsync(new ChangeEventArgs { Value = "#,##0.00" });
                await cut.Find(".ex-pivot-ok").ClickAsync(new MouseEventArgs());
                break;
        }

        cut.WaitForState(() => !cut.Instance.IsLoading);
        Assert.Empty(MarkedTexts(cut));
        Assert.Empty(MarkedTexts(cut));
    }

    [Fact] // ADR-0067 (PV-36): a layout change marks nothing even when a change of data arrives in the same redraw
    public async Task A_layout_change_with_new_data_in_the_same_redraw_marks_nothing()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);
        await PublishAsync(cut, source, EastApples(101));
        // Gathered, not yet asked for…
        await PublishAsync(cut, source, EastApples(102));
        Assert.Equal(2, source.Questions.Count);

        // …and answered by the user's question, which a new layout asks.
        await TickFieldAsync(cut, "Quantity", true);

        Assert.Equal("East | 182 | 18", RowTexts(cut)[0]);
        Assert.Empty(MarkedTexts(cut));
        Assert.Empty(MarkedTexts(cut));
        Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(3, source.Questions.Count);
    }

    [Fact] // ADR-0067/0066 (PV-36): new caps ask again for the layout on screen, and mark nothing, though the answer brings data the source never announced
    public async Task New_caps_mark_nothing()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);
        await PublishAsync(cut, source, EastApples(101));
        Assert.NotEmpty(MarkedTexts(cut));
        source.Replace(EastApples(102));

        cut.Render(ps => ps.Add(p => p.Caps, new PivotCaps { MaxLeaves = 1000 }));

        Assert.Equal(3, source.Questions.Count);
        Assert.Equal("East | 182", RowTexts(cut)[0]);
        Assert.Empty(MarkedTexts(cut));
        Assert.Empty(MarkedTexts(cut));
    }

    [Fact] // ADR-0067/0059 (PV-36): a new source is a refresh, and marks what changed; new words or a new culture mark nothing
    public async Task A_new_source_marks_and_new_words_do_not()
    {
        var cut = RenderPivot(RegionAmount);

        cut.Render(ps => ps.Add(p => p.DataSource, Bundled(EastApples(101))));
        Assert.Equal(["181", "286"], MarkedTexts(cut));

        cut.Render(ps => ps.Add(p => p.Label, PivotWords.Japanese));
        Assert.Empty(MarkedTexts(cut));
        Assert.Empty(MarkedTexts(cut));
    }

    // ---- ADR-0153: immutable rows carry changes through one stable delegate ------------------

    [Fact] // ADR-0153 (LV-30): row replacement carries change information; the delegate remains stable
    public async Task ADR0153_One_stable_delegate_reads_immutable_row_changes()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);
        Assert.Empty(MarkedTexts(cut));

        await PublishAsync(cut, source, EastApples(101));
        var first = Grid(cut).Instance.CellChangedAt;
        Assert.NotNull(first);

        // The pane, a menu, a column dragged wider, a selection: the same delegate.
        await cut.Find(".ex-pivot-search").InputAsync(new ChangeEventArgs { Value = "Reg" });
        await OpenMenuAsync(cut, "Rows", "Region");
        await cut.Find(".ex-pivot-popup").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        var name = Grid(cut).Instance.Columns[1].Name;
        await cut.InvokeAsync(() => Grid(cut).Instance.OnColumnWidthChanged.InvokeAsync(new ColumnWidthChange(name, 150)));
        await cut.InvokeAsync(() => Grid(cut).Instance.PlaceSelectionAsync(
            new ExGrid.Selection.SelectionRange(0, 1, 1, 1), new ExGrid.Selection.CellPosition(0, 1), Grid(cut).Instance.RowSequenceVersion));
        Assert.Same(first, Grid(cut).Instance.CellChangedAt);

        Clock.Advance(Interval);
        await PublishAsync(cut, source, EastApples(102));
        var second = Grid(cut).Instance.CellChangedAt;
        Assert.NotNull(second);
        Assert.Same(first, second);

        // A data version that changes nothing painted is a version all the same.
        Clock.Advance(Interval);
        await PublishAsync(cut, source, EastApples(102));
        Assert.Same(second, Grid(cut).Instance.CellChangedAt);
    }
    // ---- A server's clock -----------------------------------------------------------------------

    /// <summary>A keyed sale, for a server's Change Batches.</summary>
    public sealed record KeyedSale(long Id, string? Region, string Product, decimal Amount, int Quantity, bool Online);

    [Theory] // ADR-0068/0153: a report computed by a server whose clock is behind the browser's, or ahead of it, is marked from when ExPivot shows the change, for ChangeHighlightDuration on ExPivot's own clock
    [InlineData(-5)]
    [InlineData(0)]
    [InlineData(5)]
    public async Task A_servers_clock_changes_nothing_of_the_highlight(int skewSeconds)
    {
        var fields = PivotFields.Of<KeyedSale>().Key("Id", s => s.Id).Text("Region", s => s.Region).Text("Product", s => s.Product)
            .Number("Amount", s => s.Amount).Number("Quantity", s => s.Quantity).Boolean("Online", s => s.Online);
        var sales = Sales.Select((s, i) => new KeyedSale(i, s.Region, s.Product, s.Amount, s.Quantity, s.Online)).ToArray();
        var data = PivotSource.From(sales, fields);
        var serverClock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(Clock.GetUtcNow() + TimeSpan.FromSeconds(skewSeconds));
        await using var server = PivotReportSource.From(data, timeProvider: serverClock);
        // The server's answers cross JSON, as over HTTP: nothing but what the protocol carries.
        static T Wire<T>(T value) => PivotReportJson.Read<T>(PivotReportJson.Write(value));
        var remote = PivotReportSource.Fetch(server.Fields, server.Features, server.UpdateMode,
            async (request, ct) => Wire(await server.WindowAsync(Wire(request), ct)),
            items: server.RawItemsAsync, reportItems: async (query, ct) => Wire(await server.ItemsAsync(Wire(query), ct)),
            copy: server.CopyAsync, summary: server.SummaryAsync, details: server.DetailsAsync);
        data.Changed += change => remote.NotifyChanged(change.SourceVersion);
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Render<PivotComponent>(ps => ps
            .Add(p => p.Source, remote)
            .Add(p => p.Layout, RegionAmount)
            .Add(p => p.Culture, System.Globalization.CultureInfo.GetCultureInfo("en-US"))
            .Add(p => p.ViewportHeight, (ViewportSize)400)
            .Add(p => p.ViewportWidth, (ViewportSize)700));
        cut.WaitForAssertion(() => Assert.Equal("East | 180", RowTexts(cut)[0]));
        void Advance(TimeSpan by)
        {
            serverClock.Advance(by);
            Clock.Advance(by);
        }

        Advance(TimeSpan.FromSeconds(3));
        var shownAt = Clock.GetUtcNow();
        await cut.InvokeAsync(() => data.Apply(fields.Batch(changed: [sales[0] with { Amount = 101m }])));

        cut.WaitForAssertion(() => Assert.Equal("East | 181", RowTexts(cut)[0]));
        Assert.Equal(shownAt, ChangedAt(cut, 0, 1));
        Assert.Equal(shownAt, ChangedAt(cut, 4, 1));
        Assert.Equal(["181", "286"], MarkedTexts(cut));
        // Shown for ChangeHighlightDuration on ExPivot's clock: a moment before it ends, and then not.
        Advance(TimeSpan.FromMilliseconds(999));
        Assert.Equal(["181", "286"], MarkedTexts(cut));
        Advance(TimeSpan.FromMilliseconds(1));
        cut.WaitForAssertion(() => Assert.Empty(MarkedTexts(cut)));
    }
}
