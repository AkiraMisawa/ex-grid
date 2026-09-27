using System.Globalization;
using Bunit;
using ExGrid;
using ExGrid.Components;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// General fitted to its column, painted through ExGrid's painted text (SH-20, DC-35; ADR-0047
/// second and third rounds, ADR-0050 item 11): the grid hands the column's content width, ExSheet
/// converts it to characters with the grid's digit width and paints the engine's fitted text,
/// and the value's own text stays the accessible name and the copy.
/// </summary>
public class PaintedTextTests : SheetTestContext
{
    private static SheetDocument DocumentOf(params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    private static Task ResizeAsync(IRenderedComponent<Components.ExSheet> cut, string column, double widthPx)
    {
        var grid = Grid(cut);
        return grid.InvokeAsync(() => grid.Instance.OnColumnWidthChanged.InvokeAsync(new ColumnWidthChange(column, widthPx)));
    }

    [Fact] // ADR-0047 second round, SH-20, DC-35: =1/3 in a default column reads 0.333333, and its name is the unfitted Value
    public void One_third_reads_as_Excel_fits_it_in_a_default_column()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "=1/3"))));

        Assert.Equal("0.333333", CellText(cut, "A1"));
        Assert.Equal("0.333333333333333", Cell(cut, "A1").GetAttribute("aria-label"));
    }

    [Fact] // ADR-0047 second round: General goes scientific where the integer part does not fit
    public void A_long_integer_goes_scientific_in_a_default_column()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "=123456789*1"))));

        Assert.Equal("1.23E+08", CellText(cut, "A1"));
        Assert.Equal("123456789", Cell(cut, "A1").GetAttribute("aria-label"));
    }

    [Fact] // ADR-0050 item 11: a cell whose text the width does not shorten paints it, and names itself
    public void Text_and_short_numbers_paint_their_own_text()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "hello"), ("A2", "12.5"), ("A3", "TRUE"))));

        Assert.Equal("hello", CellText(cut, "A1"));
        Assert.Equal("12.5", CellText(cut, "A2"));
        Assert.Equal("TRUE", CellText(cut, "A3"));
        Assert.All(new[] { "A1", "A2", "A3" }, a => Assert.Null(Cell(cut, a).GetAttribute("aria-label")));
    }

    [Fact] // ADR-0016, ADR-0047: a number in a format other than General is never fitted; it is #### where it does not fit
    public void A_formatted_number_that_does_not_fit_is_hashed_not_fitted()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("A1"), "=1/3");
        sheet.SetFormat([CellAddress.Parse("A1")], NumberFormat.Parse("0.0000000000"));
        var cut = RenderSheet(ps => ps.Add(s => s.Document, sheet.ToDocument()));

        Assert.Matches("^#+$", CellText(cut, "A1"));
        Assert.Equal("0.3333333333", Cell(cut, "A1").GetAttribute("aria-label"));
    }

    [Fact] // ADR-0047 third round: a wider column shows more of the Value; the width is read from the grid's pixels
    public async Task A_wider_column_shows_more_digits()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "=1/3"))));
        var metrics = GridMetrics.Resolve(GridDensity.Compact).CellMetrics;

        await ResizeAsync(cut, "A", Math.Ceiling(11 * metrics.DigitWidthPx + 2 * metrics.CellHorizontalPaddingPx));

        Assert.Equal("0.333333333", CellText(cut, "A1"));
    }

    [Fact] // ADR-0047 third round, ADR-0005: the copy carries the Value unfitted
    public async Task A_copy_carries_the_unfitted_value()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "=1/3"))));
        await GoToAsync(cut, "A1");

        var payload = Grid(cut).Instance.BuildCopyPayload();

        Assert.Equal("0.333333333333333\r\n", payload.Text);
    }

    [Fact] // ADR-0050 item 11 / ADR-0003: the painted text is one held delegate, and an edit repaints only its row
    public async Task The_painted_text_is_held_and_an_edit_repaints_only_its_row()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "=1/3"), ("A3", "=2/3"))));
        var held = Grid(cut).Instance.PaintedText;
        Assert.NotNull(held);
        var before = cut.FindComponents<ExGridRow<SheetRow>>()
            .ToDictionary(r => r.Instance.RowIndex, r => (r.Instance.Row, r.RenderCount));

        await cut.Instance.DoAsync(SheetEdit.Enter(CellAddress.Parse("A3"), "=1/7"));

        Assert.Same(held, Grid(cut).Instance.PaintedText);
        Assert.Equal("0.142857", CellText(cut, "A3"));
        foreach (var row in cut.FindComponents<ExGridRow<SheetRow>>())
        {
            var (instance, renders) = before[row.Instance.RowIndex];
            if (row.Instance.RowIndex == 2) Assert.NotSame(instance, row.Instance.Row);
            else
            {
                Assert.Same(instance, row.Instance.Row);
                Assert.Equal(renders, row.RenderCount);
            }
        }
    }
}
