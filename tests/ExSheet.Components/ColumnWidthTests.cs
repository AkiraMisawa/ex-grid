using Bunit;
using ExGrid;
using ExGrid.Components;
using ExSheet.Components.Tests.Support;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// Column widths (ADR-0016, ADR-0047 second round, SH-20): a resize is recorded as the user's, and
/// a number or date typed into a column still at its default width widens it when it does not
/// fit, as Excel's does.
/// </summary>
public class ColumnWidthTests : SheetTestContext
{
    private static double WidthOf(Bunit.IRenderedComponent<Components.ExSheet> cut, int column) =>
        Grid(cut).Instance.Columns[column].Width.Width.FixedPx;

    private static Task ResizeAsync(Bunit.IRenderedComponent<Components.ExSheet> cut, string column, double widthPx)
    {
        var grid = Grid(cut);
        return grid.InvokeAsync(() => grid.Instance.OnColumnWidthChanged.InvokeAsync(new ColumnWidthChange(column, widthPx)));
    }

    [Fact] // ADR-0047 second round, SH-20: a number typed into a default-width column that does not fit widens it
    public async Task A_number_that_does_not_fit_widens_a_default_column()
    {
        var cut = RenderSheet();

        await EnterAsync(cut, "B1", "1234567890");

        Assert.True(WidthOf(cut, 1) > SheetColumns.DefaultWidthPx);
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        // Widened in the grid's own measure, so the grid does not hash it (ADR-0016).
        Assert.Equal("1234567890", CellText(cut, "B1"));
    }

    [Fact] // ADR-0047 second round, SH-20: a date typed as one widens the column to its whole text
    public async Task A_date_that_does_not_fit_widens_a_default_column()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, new global::ExSheet.Engine.Sheet(System.Globalization.CultureInfo.GetCultureInfo("ja-JP")).ToDocument()));

        await EnterAsync(cut, "A1", "2026/9/26");

        Assert.True(WidthOf(cut, 0) > SheetColumns.DefaultWidthPx);
        Assert.Equal("2026/09/26", CellText(cut, "A1"));
    }

    [Fact] // ADR-0047 second round: text, a Formula and a number that fits never widen a column
    public async Task Text_formulas_and_numbers_that_fit_do_not_widen()
    {
        var cut = RenderSheet();

        await EnterAsync(cut, "A1", "A heading far wider than its column");
        await EnterAsync(cut, "B1", "=1234567890*10");
        await EnterAsync(cut, "C1", "1234.5");

        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 1));
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 2));
    }

    [Fact] // ADR-0047 second round: a column widened by an entry is still at its default width, and widens again
    public async Task A_widened_column_widens_again()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1234567890");
        var first = WidthOf(cut, 0);

        await EnterAsync(cut, "A2", "12345678901");
        Assert.True(WidthOf(cut, 0) > first);

        // A shorter number never narrows it.
        var widest = WidthOf(cut, 0);
        await EnterAsync(cut, "A3", "123456789");
        Assert.Equal(widest, WidthOf(cut, 0));
    }

    [Fact] // ADR-0016 / ADR-0047: a resize is recorded as Fixed, and the column is the user's — an entry never widens it
    public async Task A_resized_column_is_the_users()
    {
        var cut = RenderSheet();

        await ResizeAsync(cut, "C", 60);

        Assert.Equal(60, WidthOf(cut, 2));
        await EnterAsync(cut, "C1", "1234567890");
        Assert.Equal(60, WidthOf(cut, 2));
        Assert.Matches("^#+$", CellText(cut, "C1"));
    }

    [Fact] // ADR-0016, ADR-0050 item 12, DC-36: the grips render because ExSheet takes the width change, and no column-menu button does
    public void The_resize_grips_are_offered_without_the_menu()
    {
        var cut = RenderSheet();

        Assert.True(Grid(cut).Instance.OnColumnWidthChanged.HasDelegate);
        Assert.True(Grid(cut).Instance.HideColumnMenu);
        Assert.NotEmpty(cut.FindAll(".ex-resize-grip"));
        Assert.Equal(cut.FindAll(".ex-header-cell").Count, cut.FindAll(".ex-resize-grip").Count);
        Assert.Empty(cut.FindAll(".ex-menu-button"));
    }

    [Fact] // ADR-0050 item 12, DC-36, ADR-0016: a double-click on a column's edge still sizes it to fit, as the user's width
    public async Task A_double_click_on_an_edge_sizes_the_column_to_fit()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "A heading far wider than its column");
        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));

        await cut.FindAll(".ex-resize-grip")[0].DoubleClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { Button = 0 });

        Assert.True(WidthOf(cut, 0) > SheetColumns.DefaultWidthPx);
        Assert.Equal("A heading far wider than its column", CellText(cut, "A1"));
    }

    [Fact] // ADR-0003: the column list is replaced only when a width changes, and an ordinary edit leaves it alone
    public async Task The_column_list_changes_only_with_a_width()
    {
        var cut = RenderSheet();
        var columns = Grid(cut).Instance.Columns;

        await EnterAsync(cut, "A1", "text");
        Assert.Same(columns, Grid(cut).Instance.Columns);

        await ResizeAsync(cut, "A", 120);
        Assert.NotSame(columns, Grid(cut).Instance.Columns);
        Assert.Same(columns[1], Grid(cut).Instance.Columns[1]);
    }

    [Fact] // ADR-0048: a different Sheet Document starts at the default widths
    public async Task Replacing_the_document_resets_the_widths()
    {
        var cut = RenderSheet();
        await ResizeAsync(cut, "A", 120);

        cut.Render(ps => ps.Add(s => s.Document, new global::ExSheet.Engine.Sheet(System.Globalization.CultureInfo.GetCultureInfo("en-US")).ToDocument()));

        Assert.Equal(SheetColumns.DefaultWidthPx, WidthOf(cut, 0));
    }
}
