using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Columns a Consumer asks for (ADR-0057; DC-50, DC-1): given a list of columns, each with a
/// colour, the grid outlines each column's body across all its rows in that colour, in the
/// selection overlay — one element per column, cut to the painted rows as a whole-column range
/// is. An empty list outlines nothing, and without the list nothing changes. The grid here
/// declares nothing else, as the positions grid beside a Sheet declares nothing: no References
/// function, no edit, no selection. Rows of 20px in a 350 × 200 Viewport under a 20px header;
/// Book, Note and Amount are 100px each, and Note edits.
/// </summary>
public class OutlinedColumnTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(150);

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Note", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        IReadOnlyList<OutlinedColumn>? outlined,
        int rows = 50,
        int? total = null,
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? more = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(rows))
              .Add(g => g.TotalCount, total ?? rows)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350);
            if (outlined is not null)
                ps.Add(g => g.OutlinedColumns, outlined);
            more?.Invoke(ps);
        });

    private static OutlinedColumn Outlined(string column, int place) => new(column, new ReferenceColour(place));

    /// <summary>A rectangle's inline style without the hole a range holding the Focus carries.</summary>
    private static string Rectangle(string? style)
        => System.Text.RegularExpressions.Regex.Replace(style ?? "", @";\s*--ex-range-hole:[^;]*;?$", "");

    /// <summary>The Reference Outlines in one layer of the overlay, as class and style.</summary>
    private static List<(string? Class, string? Style)> Outlines(IRenderedComponent<ExGrid<TestRow>> cut, string layer = ".ex-selection")
        => [.. cut.FindAll($"{layer} .ex-reference-outline").Select(e => (e.GetAttribute("class"), e.GetAttribute("style")))];

    [Fact] // ADR-0057 / DC-50: each listed column is outlined over all its rows, in its colour, with nothing selected
    public void Each_listed_column_is_outlined_over_all_its_rows_in_its_colour()
    {
        var cut = RenderGrid([Outlined("Amount", 3), Outlined("Book", 1)], rows: 5);

        // Five rows of 20px, every one painted: the whole column's body, 100px tall.
        Assert.Equal(
        [
            ("ex-reference-outline ex-reference-3", "left: 200px; top: 0px; width: 100px; height: 100px"),
            ("ex-reference-outline ex-reference-1", "left: 0px; top: 0px; width: 100px; height: 100px"),
        ], Outlines(cut));
        Assert.Empty(Outlines(cut, ".ex-selection-pinned"));
        // Nothing was selected for them: they are the Consumer's, not the Selection's.
        Assert.Empty(cut.FindAll(".ex-range, .ex-focus"));
    }

    [Fact] // ADR-0057 / DC-50 / ADR-0008: one element per column, never one per cell
    public void A_column_is_one_element_whatever_it_covers()
    {
        var cut = RenderGrid([Outlined("Note", 2)]);

        Assert.Single(cut.FindAll(".ex-reference-outline"));
        Assert.Empty(cut.FindAll(".ex-row .ex-reference-outline, .ex-cell.ex-reference-outline"));
    }

    [Fact] // ADR-0057 / DC-50 / ADR-0053: cut to the painted rows exactly as a whole-column range is, rows outside the Window included, and never revealed
    public async Task An_outline_is_cut_to_the_painted_rows_as_a_whole_column_range_is()
    {
        // A thousand rows, fifty of them in the Window: all the rows are the column's, not the Window's.
        var cut = RenderGrid([Outlined("Note", 2)], rows: 50, total: 1000);
        await cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, ShiftKey = true, OffsetX = 150, OffsetY = 10 });
        var column = cut.Find(".ex-selection .ex-range");
        var scrolls = JSInterop.Invocations.Count(i => i.Identifier == "setScrollOffset");

        var outline = Assert.Single(Outlines(cut));
        Assert.Equal("ex-reference-outline ex-reference-2", outline.Class);
        // The same rectangle. A range holding the Focus also carries the hole where the Focus is
        // (--ex-range-hole, ADR-0008), which an outline, being no selection, never has.
        Assert.Equal(Rectangle(column.GetAttribute("style")), outline.Style);
        Assert.StartsWith("left: 100px; top: 0px; width: 100px; height: ", outline.Style);

        // Past the Window, the painted rows are Placeholders, and the outline is still over them.
        await ScrollToAsync(cut.Find(".ex-scroller"), 500 * 20);
        Clock.Advance(SettleDelay);
        cut.WaitForAssertion(() => Assert.Equal(
            Rectangle(cut.Find(".ex-selection .ex-range").GetAttribute("style")),
            Assert.Single(Outlines(cut)).Style));
        Assert.NotEqual(outline.Style, Assert.Single(Outlines(cut)).Style);
        Assert.Equal(scrolls, JSInterop.Invocations.Count(i => i.Identifier == "setScrollOffset"));
    }

    [Fact] // ADR-0057 / DC-50 / ADR-0004: a pinned column is outlined in the pinned layer, the rest in the scrollable one
    public void A_pinned_column_is_outlined_in_the_pinned_layer()
    {
        var cut = RenderGrid([Outlined("Book", 2), Outlined("Note", 4)], rows: 5, more: ps => ps.Add(g => g.PinnedColumnCount, 1));

        Assert.Equal([("ex-reference-outline ex-reference-2", "left: 0px; top: 0px; width: 100px; height: 100px")], Outlines(cut, ".ex-selection-pinned"));
        Assert.Equal([("ex-reference-outline ex-reference-4", "left: 100px; top: 0px; width: 100px; height: 100px")], Outlines(cut));
    }

    [Fact] // ADR-0057 / DC-50: the outline is the column's, by name, wherever the Consumer's order puts it
    public void The_outline_follows_its_column_when_the_order_changes()
    {
        IReadOnlyList<OutlinedColumn> outlined = [Outlined("Amount", 1)];
        var cut = RenderGrid(outlined, rows: 5);
        GridColumn<TestRow>[] columns = Columns();

        cut.Render(ps => ps.Add(g => g.Columns, [columns[2], columns[0], columns[1]]));

        Assert.Equal([("ex-reference-outline ex-reference-1", "left: 0px; top: 0px; width: 100px; height: 100px")], Outlines(cut));
    }

    [Fact] // ADR-0057 / DC-50: a column the grid does not show is outlined nowhere, as a Reference to cells it does not have is; the others keep the colour they were given
    public void A_column_the_grid_does_not_show_is_outlined_nowhere()
    {
        var cut = RenderGrid([Outlined("Hidden", 1), Outlined("Note", 2)], rows: 5);

        Assert.Equal([("ex-reference-outline ex-reference-2", "left: 100px; top: 0px; width: 100px; height: 100px")], Outlines(cut));
    }

    [Fact] // ADR-0057 / DC-50: a column listed twice — two Sheets reading it, say — is outlined twice, the later over the earlier (decided with the user, 2026-09-30)
    public void A_column_listed_twice_is_outlined_twice_in_order()
    {
        var cut = RenderGrid([Outlined("Note", 1), Outlined("Book", 3), Outlined("Note", 2)], rows: 5);

        Assert.Equal(
        [
            ("ex-reference-outline ex-reference-1", "left: 100px; top: 0px; width: 100px; height: 100px"),
            ("ex-reference-outline ex-reference-3", "left: 0px; top: 0px; width: 100px; height: 100px"),
            ("ex-reference-outline ex-reference-2", "left: 100px; top: 0px; width: 100px; height: 100px"),
        ], Outlines(cut));
    }

    [Fact] // ADR-0057 / DC-50: a column is named by a name
    public void An_outlined_column_needs_a_name()
    {
        Assert.Throws<ArgumentException>(() => new OutlinedColumn("", new ReferenceColour(1)));
        Assert.Throws<ArgumentNullException>(() => new OutlinedColumn(null!, new ReferenceColour(1)));
        Assert.Throws<ArgumentNullException>(() => RenderGrid([null!]));
    }

    [Fact] // ADR-0057 / DC-50: a grid with no rows has no body to outline
    public void A_grid_with_no_rows_outlines_nothing()
    {
        var cut = RenderGrid([Outlined("Note", 1)], rows: 0);

        Assert.Empty(cut.FindAll(".ex-reference-outline"));
    }

    [Fact] // ADR-0057 / DC-50 / DC-1: an empty list outlines nothing, and with no list nothing changes
    public void An_empty_list_or_none_changes_nothing()
    {
        var rows = TestRows.Many(50);
        var columns = Columns();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, columns)
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350));
        var without = cut.Markup;
        // No overlay at all while nothing is selected or hovered: no layer painted for nothing.
        Assert.Empty(cut.FindAll(".ex-selection, .ex-selection-pinned"));

        cut.Render(ps => ps.Add(g => g.OutlinedColumns, []));
        Assert.Equal(without, cut.Markup);

        cut.Render(ps => ps.Add(g => g.OutlinedColumns, [Outlined("Note", 1)]));
        Assert.Single(cut.FindAll(".ex-reference-outline"));

        cut.Render(ps => ps.Add(g => g.OutlinedColumns, null));
        Assert.Equal(without, cut.Markup);
    }

    [Fact] // ADR-0003 / ADR-0057: outlines are overlay paint, and no row renders for them
    public void Outlines_render_no_row()
    {
        var rows = TestRows.Many(50);
        var columns = Columns();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, columns)
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350));
        var before = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

        cut.Render(ps => ps.Add(g => g.OutlinedColumns, [Outlined("Note", 1)]));
        cut.Render(ps => ps.Add(g => g.OutlinedColumns, [Outlined("Note", 2), Outlined("Book", 3)]));
        cut.Render(ps => ps.Add(g => g.OutlinedColumns, []));

        Assert.Equal(before, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList());
    }

    [Fact] // ADR-0057 / DC-50 / DC-46: a column asked for and a range the edit's text names are outlined side by side
    public async Task Columns_asked_for_stand_beside_an_edits_outlines()
    {
        var cut = RenderGrid([Outlined("Amount", 5)], rows: 5, more: ps => ps
            .Add(g => g.ReferencesIn, text => text.StartsWith('=') && text.Length > 2
                ? [new EditorReference(1, 2, new SelectionRange(0, 0, 1, 1))]
                : []));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 150, OffsetY = 30 });
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("=", false, false, false, false, false));

        await cut.Find(".ex-viewport .ex-editor").InputAsync(new ChangeEventArgs { Value = "=A1" });

        Assert.Equal(
        [
            ("ex-reference-outline ex-reference-1", "left: 0px; top: 0px; width: 100px; height: 20px"),
            ("ex-reference-outline ex-reference-5", "left: 200px; top: 0px; width: 100px; height: 100px"),
        ], Outlines(cut));
    }
}
