using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The header click and the Sorts wiring (ADR-0012: clicking a column header sorts;
/// ADR-0001: the grid never sorts — the state travels out and a new Window travels in;
/// ADR-0011: a changed order drops the selection).
/// </summary>
public class SortWiringTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    private static Task ClickHeaderAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x)
        => cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, OffsetX = x, OffsetY = 10 });

    [Fact] // ADR-0012 / SR-1: one click raises OnSortChanged with ascending on that column
    public async Task A_header_click_raises_the_next_sorts_list()
    {
        IReadOnlyList<SortSpec>? raised = null;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns())
            .Add(g => g.OnSortChanged, (IReadOnlyList<SortSpec> s) => raised = s));

        await ClickHeaderAsync(cut, 150); // inside "Amount"

        Assert.Equal([new SortSpec("Amount", SortDirection.Ascending)], raised);
    }

    [Fact] // ADR-0012: the click cycles from the Sorts the Consumer passed back in
    public async Task The_cycle_reads_the_applied_sorts()
    {
        IReadOnlyList<SortSpec>? raised = null;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns())
            .Add(g => g.Sorts, (IReadOnlyList<SortSpec>)[new SortSpec("Amount", SortDirection.Ascending)])
            .Add(g => g.OnSortChanged, (IReadOnlyList<SortSpec> s) => raised = s));

        await ClickHeaderAsync(cut, 150);
        Assert.Equal([new SortSpec("Amount", SortDirection.Descending)], raised);

        cut.Render(ps => ps.Add(g => g.Sorts, (IReadOnlyList<SortSpec>)[new SortSpec("Amount", SortDirection.Descending)]));
        await ClickHeaderAsync(cut, 150);
        Assert.Empty(raised!);
    }

    [Fact] // ADR-0001 / SR-3: the grid never reorders — the painted order is the pushed order
    public async Task The_painted_order_does_not_move_until_a_new_window_arrives()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns())
            .Add(g => g.OnSortChanged, (IReadOnlyList<SortSpec> _) => { }));

        await ClickHeaderAsync(cut, 150);

        Assert.Equal(
            ["Alpha", "Beta", "Gamma"],
            cut.FindAll(".ex-row").Select(r => r.QuerySelector(".ex-cell")!.TextContent));
    }

    [Fact] // ADR-0012: with a Source the click goes to the source, which requeries itself
    public async Task With_a_source_the_click_sorts_the_source()
    {
        var source = GridSource.From(TestRows.Window());
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns()));

        await ClickHeaderAsync(cut, 150); // Amount ascending: -7, 0, 100.5

        Assert.Equal([new SortSpec("Amount", SortDirection.Ascending)], source.Sorts);
        Assert.Equal(
            ["Beta", "Gamma", "Alpha"],
            cut.FindAll(".ex-row").Select(r => r.QuerySelector(".ex-cell")!.TextContent));
    }

    [Fact] // ADR-0011 / SR-4: a sort that changes the visible order drops the selection
    public async Task A_sort_that_moves_the_order_drops_the_selection()
    {
        var source = GridSource.From(TestRows.Window());
        GridSelection? selection = null;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });
        Assert.False(selection!.IsEmpty);

        await ClickHeaderAsync(cut, 150);

        Assert.True(selection!.IsEmpty);
    }

    [Fact] // ADR-0011 / SR-4: a sort that leaves the sequence identical keeps the selection
    public async Task A_sort_that_leaves_the_order_alone_keeps_the_selection()
    {
        // Already in ascending Book order, so sorting by Book changes nothing.
        var source = GridSource.From(TestRows.Window());
        GridSelection? selection = null;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });

        await ClickHeaderAsync(cut, 50); // Book ascending — a no-op reorder

        Assert.False(selection!.IsEmpty);
    }

    [Fact] // SR-6: aria-sort reflects the applied sort; none elsewhere
    public async Task Aria_sort_follows_the_applied_sort()
    {
        var source = GridSource.From(TestRows.Window());
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns()));

        Assert.Equal(
            ["none", "none"],
            cut.FindAll(".ex-header-cell").Select(h => h.GetAttribute("aria-sort")));

        await ClickHeaderAsync(cut, 150);

        Assert.Equal(
            ["none", "ascending"],
            cut.FindAll(".ex-header-cell").Select(h => h.GetAttribute("aria-sort")));
    }

    [Fact] // ADR-0020: an Action Column is unsortable — the click does nothing
    public async Task A_click_on_an_action_column_header_is_ignored()
    {
        IReadOnlyList<SortSpec>? raised = null;
        GridColumn<TestRow>[] columns =
        [
            new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
            GridColumn<TestRow>.ActionColumn("Ops", [new GridAction("open", "Open")], width: Fixed100),
        ];
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, columns)
            .Add(g => g.OnSortChanged, (IReadOnlyList<SortSpec> s) => raised = s));

        await ClickHeaderAsync(cut, 150);

        Assert.Null(raised);
        Assert.Null(cut.FindAll(".ex-header-cell")[1].GetAttribute("aria-sort"));
    }

    [Fact] // A click past the last column lands on nothing, not on the last column again
    public async Task A_click_past_the_last_column_is_ignored()
    {
        IReadOnlyList<SortSpec>? raised = null;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns())
            .Add(g => g.OnSortChanged, (IReadOnlyList<SortSpec> s) => raised = s));

        await ClickHeaderAsync(cut, 450); // two 100px columns end at 200

        Assert.Null(raised);
    }

    [Fact] // ADR-0001: Sorts and OnSortChanged belong to the push form — a Source holds them itself
    public void Sorts_beside_a_source_is_refused_by_name()
    {
        var source = GridSource.From(TestRows.Window());

        var refusal = Assert.Throws<InvalidOperationException>(() => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.Sorts, (IReadOnlyList<SortSpec>)[])));

        Assert.Contains("Sorts", refusal.Message);
        Assert.Contains("Source", refusal.Message);
    }
}
