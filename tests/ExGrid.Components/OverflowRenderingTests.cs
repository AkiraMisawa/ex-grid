using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The ADR-0016 wiring: columns carry C#-resolved pixel widths, and a Number or Date
/// that does not fit is painted as ####, never as a plausible-looking shorter number.
/// </summary>
public class OverflowRenderingTests : GridTestContext
{
    private static IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        BunitContext ctx, TestRow[] rows, GridColumn<TestRow>[] columns)
        => ctx.Render<ExGrid<TestRow>>(ps => ps.Add(g => g.Window, rows).Add(g => g.Columns, columns));

    [Fact] // ADR-0016: a Number that does not fit becomes ####, not an ellipsis
    public void An_overflowing_number_cell_paints_hashes()
    {
        GridColumn<TestRow>[] columns =
        [
            new("Amount", ColumnType.Number, r => r.Amount,
                width: new ColumnWidthSpec(ColumnWidth.Fixed(60))),
        ];
        TestRow[] rows = [new() { Amount = 123456789.25m }];

        var cell = RenderGrid(this, rows, columns).Find(".ex-cell");

        Assert.NotEmpty(cell.TextContent);
        Assert.DoesNotContain(cell.TextContent, c => c != '#');
    }

    [Fact] // ADR-0016: Text never hashes — the full value stays in the markup (the cut is CSS)
    public void An_overflowing_text_cell_keeps_its_full_value()
    {
        GridColumn<TestRow>[] columns =
        [
            new("Book", ColumnType.Text, r => r.Book,
                width: new ColumnWidthSpec(ColumnWidth.Fixed(60))),
        ];
        TestRow[] rows = [new() { Book = "A very long description that cannot fit" }];

        var cell = RenderGrid(this, rows, columns).Find(".ex-cell");

        Assert.Equal("A very long description that cannot fit", cell.TextContent);
    }

    [Fact] // ADR-0016: the resolved width reaches every cell and header cell as an inline style
    public void Cells_and_header_cells_carry_the_resolved_width()
    {
        GridColumn<TestRow>[] columns =
        [
            new("Amount", ColumnType.Number, r => r.Amount,
                width: new ColumnWidthSpec(ColumnWidth.Fixed(120))),
        ];

        var cut = RenderGrid(this, TestRows.Window(), columns);

        Assert.Contains("width: 120px", cut.Find(".ex-header-cell").GetAttribute("style"));
        Assert.All(cut.FindAll(".ex-cell"),
            cell => Assert.Contains("width: 120px", cell.GetAttribute("style")));
    }

    [Fact] // ADR-0016: an Auto width only ever grows — a screen of short values does not narrow it
    public void An_auto_width_grows_and_never_narrows()
    {
        GridColumn<TestRow>[] columns = [new("Book", ColumnType.Text, r => r.Book)];
        TestRow[] shortRows = [new() { Book = "AB" }];
        var cut = RenderGrid(this, shortRows, columns);
        var initial = CellWidth(cut);

        TestRow[] longRows = [new() { Book = "A much longer book name" }];
        cut.Render(ps => ps.Add(g => g.Window, longRows).Add(g => g.Columns, columns));
        var grown = CellWidth(cut);

        TestRow[] shortAgain = [new() { Book = "C" }];
        cut.Render(ps => ps.Add(g => g.Window, shortAgain).Add(g => g.Columns, columns));

        Assert.True(grown > initial);
        Assert.Equal(grown, CellWidth(cut));
    }

    private static double CellWidth(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        var style = cut.Find(".ex-cell").GetAttribute("style")!;
        var match = System.Text.RegularExpressions.Regex.Match(style, @"width: ([0-9.]+)px");
        return double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
