using System.Globalization;
using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The bundled sources' live data as the grid shows it (ADR-0141): <c>GridSource.From</c> with a
/// Row Key and <c>GridSource.Fetch</c> told that the data moved on, bound to an ExGrid whose clock
/// and the sources' are the test's fake one. Gathering reaches the screen at the interval's end
/// (LV-6), the Selection is kept when the sequence did not move and dropped when it did (LV-7, LV-8),
/// and the source's Change Highlight marks the cells that changed, asked again only by the rows with
/// new instances (LV-9).
/// </summary>
public class LiveSourceGridTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static readonly GridColumn<TestRow>[] Columns =
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100,
            format: value => ((decimal)value).ToString("0.0", CultureInfo.InvariantCulture)),
    ];

    private static TestRow With(TestRow row, decimal amount) => new() { Book = row.Book, Amount = amount, AsOf = row.AsOf, Active = row.Active };

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        IGridSource<TestRow> source, CellChangeOf<TestRow>? changedAt = null, Action<GridSelection>? selected = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Source, source)
              .Add(g => g.Columns, Columns)
              .Add(g => g.RowHeight, 20)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.CellChangedAt, changedAt);
            if (selected is not null)
                ps.Add(g => g.SelectionChanged, selected);
        });

    private static string AmountText(IRenderedComponent<ExGrid<TestRow>> cut, string book)
        => cut.FindComponents<ExGridRow<TestRow>>().Single(r => r.Instance.Row.Book == book)
            .FindAll(".ex-cell")[1].TextContent;

    private static List<(string Book, string Column)> MarkedCells(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindComponents<ExGridRow<TestRow>>()
            .SelectMany(row => row.FindAll(".ex-changed").Select(cell => (
                row.Instance.Row.Book,
                row.Instance.Columns[int.Parse(cell.GetAttribute("aria-colindex")!, CultureInfo.InvariantCulture) - 1].Name)))
            .OrderBy(cell => cell.Book, StringComparer.Ordinal)];

    private static void SelectFirstCell(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-viewport").MouseDown(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });

    // ---- GridSource.From with a Row Key ---------------------------------------------------------

    [Fact] // ADR-0141 / LV-6: the first change reaches the grid at once; the changes within the interval at its end, as one
    public void Gathered_changes_reach_the_grid_at_the_intervals_end()
    {
        var rows = TestRows.Window();
        var source = GridSource.From(rows, r => r.Book, Clock);
        var cut = RenderGrid(source);

        source.Apply(new(changed: [With(rows[0], 1)]));
        Assert.Equal("1.0", AmountText(cut, "Alpha"));

        Clock.Advance(TimeSpan.FromMilliseconds(100));
        source.Apply(new(changed: [With(rows[1], 2)]));
        source.Apply(new(changed: [With(rows[2], 3)]));
        Assert.Equal("-7.0", AmountText(cut, "Beta"));
        Assert.Equal("0.0", AmountText(cut, "Gamma"));

        Clock.Advance(TimeSpan.FromMilliseconds(150));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal("2.0", AmountText(cut, "Beta"));
            Assert.Equal("3.0", AmountText(cut, "Gamma"));
        });
    }

    [Fact] // ADR-0141 / LV-7: a batch that moves no row keeps the Selection; one that moves a row drops it
    public void The_selection_is_kept_when_nothing_moved_and_dropped_when_a_row_did()
    {
        var rows = TestRows.Window();
        var source = GridSource.From(rows, r => r.Book, Clock);
        source.GatherInterval = TimeSpan.Zero;
        GridSelection? selection = null;
        var cut = RenderGrid(source, selected: s => selection = s);
        source.OnSortChanged([new SortSpec("Amount", SortDirection.Ascending)]);
        // Beta (-7), Gamma (0), Alpha (100.5).
        SelectFirstCell(cut);
        Assert.Equal(1, selection!.CellCount);

        source.Apply(new(changed: [With(rows[2], 50)]));
        // No SelectionChanged was raised: the Selection stands.
        Assert.Equal(1, selection.CellCount);

        source.Apply(new(changed: [With(rows[1], 500)]));
        Assert.True(selection.IsEmpty);
        Assert.Empty(cut.FindAll(".ex-range"));
    }

    [Fact] // ADR-0141 / LV-9: the source's Change Highlight marks the changed cell, and only the changed row asks again
    public void The_sources_change_highlight_marks_the_changed_cell()
    {
        var rows = TestRows.Window();
        var source = GridSource.From(rows, r => r.Book, Clock);
        source.GatherInterval = TimeSpan.Zero;
        var asked = new List<string>();
        var sourceChangedAt = source.CellChangedAt!;
        // One counting delegate, held, over the source's one delegate.
        CellChangeOf<TestRow> counting = (row, column) =>
        {
            asked.Add(row.Book);
            return sourceChangedAt(row, column);
        };
        var cut = RenderGrid(source, counting);
        asked.Clear();

        source.Apply(new(changed: [With(rows[1], 12)]));

        cut.WaitForAssertion(() => Assert.Equal([("Beta", "Amount")], MarkedCells(cut)));
        Assert.Equal(["Beta", "Beta"], asked);

        // A change the format hides is not marked, and a sort marks nothing.
        asked.Clear();
        source.Apply(new(changed: [With(rows[2], 0.01m)]));
        source.OnSortChanged([new SortSpec("Book", SortDirection.Descending)]);
        Assert.Equal([("Beta", "Amount")], MarkedCells(cut));

        // The mark goes when the grid's duration has passed.
        Clock.Advance(TimeSpan.FromSeconds(1));
        cut.WaitForAssertion(() => Assert.Empty(MarkedCells(cut)));
    }

    [Fact] // ADR-0141 / LV-9: every cell of a row added is marked
    public void Every_cell_of_an_added_row_is_marked()
    {
        var source = GridSource.From(TestRows.Window(), r => r.Book, Clock);
        source.GatherInterval = TimeSpan.Zero;
        var cut = RenderGrid(source, source.CellChangedAt);

        source.Apply(new(added: [new TestRow { Book = "Delta", Amount = 4 }]));

        cut.WaitForAssertion(() => Assert.Equal([("Delta", "Book"), ("Delta", "Amount")], MarkedCells(cut)));
    }

    [Fact] // ADR-0141 / LV-9, ADR-0067: a filter or a change of columns changes what is painted, and marks nothing
    public void A_filter_or_a_column_change_marks_nothing()
    {
        var rows = TestRows.Window();
        var source = GridSource.From(rows, r => r.Book, Clock);
        source.GatherInterval = TimeSpan.Zero;
        var cut = RenderGrid(source, source.CellChangedAt);

        source.OnFilterChanged(new GridFilter(new Dictionary<string, FilterSpec>
        {
            ["Book"] = new([new FilterClause(FilterOperator.Equals, "Beta")]),
        }));
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<ExGridRow<TestRow>>()));
        Assert.Empty(MarkedCells(cut));

        source.OnFilterChanged(null);
        cut.Render(ps => ps.Add(g => g.Columns, [Columns[1], Columns[0]]));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindComponents<ExGridRow<TestRow>>().Count));
        Assert.Empty(MarkedCells(cut));
    }

    // ---- GridSource.Fetch told that the data moved on -----------------------------------------------

    /// <summary>A server holding its rows, answering every question at once, as new instances.</summary>
    private sealed class Server
    {
        public List<TestRow> Rows { get; } = [.. TestRows.Many(300)];

        public string Token { get; set; } = "order-0";

        public ValueTask<GridPage<TestRow>> Fetch(GridQuery query, CancellationToken cancellation)
        {
            var start = Math.Min(query.Range.Start, Rows.Count);
            var count = Math.Min(query.Range.Count, Rows.Count - start);
            var rows = Rows.Skip(start).Take(count).Select(r => With(r, r.Amount)).ToArray();
            return ValueTask.FromResult(new GridPage<TestRow>(rows, start, Rows.Count) { OrderToken = Token });
        }
    }

    [Fact] // ADR-0141 / LV-8: told the data moved on, the source reads its Window again and the grid marks what changed
    public void A_notice_repaints_the_window_and_marks_what_changed()
    {
        var server = new Server();
        var source = GridSource.Fetch<TestRow>(server.Fetch, rowKey: r => r.Book, clock: Clock);
        var cut = RenderGrid(source, source.CellChangedAt);
        cut.WaitForAssertion(() => Assert.Equal("3.0", AmountText(cut, "Row 000003")));

        server.Rows[3] = With(server.Rows[3], 33);
        source.NotifyChanged();

        cut.WaitForAssertion(() => Assert.Equal("33.0", AmountText(cut, "Row 000003")));
        Assert.Equal([("Row 000003", "Amount")], MarkedCells(cut));
    }

    [Fact] // ADR-0141 / LV-8: a row cancelled after the Window, with a Selection reaching past it, drops the Selection
    public async Task A_row_cancelled_after_the_window_drops_a_selection_reaching_past_it()
    {
        var server = new Server();
        var source = GridSource.Fetch<TestRow>(server.Fetch, rowKey: r => r.Book, clock: Clock);
        GridSelection? selection = null;
        var cut = RenderGrid(source, source.CellChangedAt, s => selection = s);
        cut.WaitForAssertion(() => Assert.Equal(300, cut.Instance.Source!.TotalCount));
        SelectFirstCell(cut);
        // Every row, past the Window (ADR-0011's Ctrl+A).
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("a", true, false, false, false, false));
        Assert.Equal(600, selection!.CellCount);
        var painted = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.Instance.Row.Book).ToArray();

        // One row cancelled far past the Window, and one booked: the rows on screen are the same rows,
        // and only the server's order token says the order moved.
        server.Rows.RemoveAt(250);
        server.Rows.Add(new TestRow { Book = "Row 999999", Amount = 1 });
        server.Token = "order-1";
        source.NotifyChanged();

        cut.WaitForAssertion(() => Assert.True(selection.IsEmpty));
        Assert.Empty(cut.FindAll(".ex-range"));
        Assert.Equal(painted, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.Instance.Row.Book).ToArray());
    }

    [Fact] // ADR-0141 / LV-8: the same order token keeps the Selection through a change of values
    public void The_same_token_keeps_the_selection_through_a_change_of_values()
    {
        var server = new Server();
        var source = GridSource.Fetch<TestRow>(server.Fetch, rowKey: r => r.Book, clock: Clock);
        GridSelection? selection = null;
        var cut = RenderGrid(source, source.CellChangedAt, s => selection = s);
        cut.WaitForAssertion(() => Assert.Equal(300, cut.Instance.Source!.TotalCount));
        SelectFirstCell(cut);

        server.Rows[0] = With(server.Rows[0], 77);
        source.NotifyChanged();

        cut.WaitForAssertion(() => Assert.Equal("77.0", AmountText(cut, "Row 000000")));
        Assert.Equal(1, selection!.CellCount);
    }

    // A row put into the server's rows at the Window's third place, as a new booking would land under a
    // server's order, and the row the Window's last place shows after it.
    private static TestRow Booked(Server server)
    {
        var booked = new TestRow { Book = "Row 000002b", Amount = 5 };
        server.Rows.Insert(3, booked);
        return booked;
    }

    [Fact] // ADR-0141 D6 / LV-9: a key the Consumer names as added is marked whole; a key that only slid into the Window is not
    public void A_named_added_key_is_marked_whole_and_one_that_slid_in_is_not()
    {
        var server = new Server();
        var source = GridSource.Fetch<TestRow>(server.Fetch, rowKey: r => r.Book, clock: Clock);
        var changedAt = source.CellChangedAt!;
        var cut = RenderGrid(source, changedAt);
        cut.WaitForAssertion(() => Assert.Equal("3.0", AmountText(cut, "Row 000003")));
        var size = source.Window.Count;
        var before = source.Window.Select(r => r.Book).ToHashSet(StringComparer.Ordinal);
        // Two rows cancelled at the front and one booked among the first: one row past the Window's
        // end slides into it.
        server.Rows.RemoveAt(0);
        server.Rows.RemoveAt(0);
        var booked = new TestRow { Book = "Row 000002b", Amount = 5 };
        server.Rows.Insert(1, booked);

        source.NotifyChanged([booked.Book]);

        cut.WaitForAssertion(() => Assert.Equal("5.0", AmountText(cut, booked.Book)));
        Assert.Equal(size, source.Window.Count);
        var slid = Assert.Single(source.Window, r => r.Book != booked.Book && !before.Contains(r.Book));
        var inWindow = source.Window.Single(r => r.Book == booked.Book);
        Assert.All(Columns, column => Assert.NotNull(changedAt(inWindow, column)));
        Assert.All(Columns, column => Assert.Null(changedAt(slid, column)));
    }

    [Fact] // ADR-0141 D6 / LV-9: with no keys named, a new key between two rows painted before is guessed to be added, and marked whole
    public void With_no_keys_named_a_key_between_painted_rows_is_guessed_added()
    {
        var server = new Server();
        var source = GridSource.Fetch<TestRow>(server.Fetch, rowKey: r => r.Book, clock: Clock);
        var cut = RenderGrid(source, source.CellChangedAt);
        cut.WaitForAssertion(() => Assert.Equal("3.0", AmountText(cut, "Row 000003")));
        var booked = Booked(server);

        source.NotifyChanged();

        cut.WaitForAssertion(() => Assert.Equal("5.0", AmountText(cut, booked.Book)));
        Assert.Equal(2, MarkedCells(cut).Count(c => c.Book == booked.Book));
    }
}
