using System.Globalization;
using Bunit;
using ExGrid.Cells;
using ExGrid.Components;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// ExSheet's Borders, drawn through ADR-0050 item 15 (ticket 49, ADR-0071): each cell's sides are
/// the edges as the engine shows them (<see cref="Sheet.GetBorders"/>), so where both cells record a
/// line the upper or left cell's is drawn (the twelfth Windows run); a line on a whole row or column
/// is drawn on cells that hold nothing; and a change repaints the rows either side of an edge it
/// moved, and no other. The pixels are layer 3's.
/// </summary>
public class SheetBorderTests : SheetTestContext
{
    private static readonly CellColour Red = CellColour.FromRgb(0xFF0000);
    private static readonly CellColour Blue = CellColour.FromRgb(0x0000FF);
    private static readonly BorderLine Thin = new(BorderLineStyle.Thin);
    private static readonly BorderLine Thick = new(BorderLineStyle.Thick);

    private static SheetDocument DocumentOf(Action<Sheet>? prepare = null, params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        prepare?.Invoke(sheet);
        return sheet.ToDocument();
    }

    private static void Format(Sheet sheet, string range, CellFormatChange change) =>
        sheet.SetCellFormat([CellRange.Parse(range)], change);

    private static Dictionary<int, (SheetRow Row, int Renders)> Rows(IRenderedComponent<ExSheet> cut) =>
        cut.FindComponents<ExGridRow<SheetRow>>().ToDictionary(r => r.Instance.RowIndex, r => (r.Instance.Row, r.RenderCount));

    private static string Classes(IRenderedComponent<ExSheet> cut, string address) => Cell(cut, address).ClassName ?? "";

    /// <summary>Asserts that exactly <paramref name="repainted"/> of the painted rows rendered again.</summary>
    private static void OnlyRepainted(Dictionary<int, (SheetRow Row, int Renders)> before, IRenderedComponent<ExSheet> cut, params int[] repainted)
    {
        foreach (var (index, (row, renders)) in Rows(cut))
        {
            if (repainted.Contains(index))
            {
                // A row the engine names is handed over as a new instance, which the grid paints as a
                // new row; any other row paints again in place.
                Assert.True(!ReferenceEquals(before[index].Row, row) || renders > before[index].Renders, $"row {index + 1} repaints");
                continue;
            }
            Assert.Same(before[index].Row, row);
            Assert.Equal(before[index].Renders, renders);
        }
    }

    [Theory] // ADR-0071, ticket 49: the engine's line styles are the core's, by name, and Automatic is Excel's black
    [InlineData(BorderLineStyle.None, BorderStyle.None)]
    [InlineData(BorderLineStyle.Hair, BorderStyle.Hair)]
    [InlineData(BorderLineStyle.Thin, BorderStyle.Thin)]
    [InlineData(BorderLineStyle.Medium, BorderStyle.Medium)]
    [InlineData(BorderLineStyle.Thick, BorderStyle.Thick)]
    [InlineData(BorderLineStyle.Double, BorderStyle.Double)]
    [InlineData(BorderLineStyle.Dotted, BorderStyle.Dotted)]
    [InlineData(BorderLineStyle.Dashed, BorderStyle.Dashed)]
    [InlineData(BorderLineStyle.DashDot, BorderStyle.DashDot)]
    [InlineData(BorderLineStyle.DashDotDot, BorderStyle.DashDotDot)]
    [InlineData(BorderLineStyle.MediumDashed, BorderStyle.MediumDashed)]
    [InlineData(BorderLineStyle.MediumDashDot, BorderStyle.MediumDashDot)]
    [InlineData(BorderLineStyle.MediumDashDotDot, BorderStyle.MediumDashDotDot)]
    [InlineData(BorderLineStyle.SlantedDashDot, BorderStyle.SlantedDashDot)]
    public void Each_line_style_is_the_cores(BorderLineStyle engine, BorderStyle core)
    {
        Assert.Equal(core, SheetAppearance.StyleOf(engine));
        var line = SheetAppearance.LineOf(new BorderLine(engine));
        Assert.Equal(core == BorderStyle.None ? Border.None : new Border(core, RgbColour.Black), line);
        Assert.Equal(Enum.GetValues<BorderLineStyle>().Length, Enum.GetValues<BorderStyle>().Length);
    }

    [Fact] // ADR-0050 item 15, DC-59: a cell's line is declared on both cells of its edge, and the thick one's share reaches into the cell below
    public void A_line_is_declared_on_both_cells_of_its_edge()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(
            sheet => Format(sheet, "B2", new CellFormatChange { Borders = new BorderChange { Bottom = Thick, Right = new BorderLine(BorderLineStyle.Thin, Red) } }))));

        // B2 draws the gridline's pixel and the one above it; B3 the pixel a thick line reaches past it.
        Assert.Contains("ex-lb-thick-000000", Classes(cut, "B2"));
        Assert.Contains("ex-lt-thick-000000", Classes(cut, "B3"));
        // A thin line lies on the gridline alone, which B2 holds: C2 draws nothing of it.
        Assert.Contains("ex-lr-thin-ff0000", Classes(cut, "B2"));
        Assert.DoesNotContain("ex-ll-", Classes(cut, "C2"));
        Assert.DoesNotContain("ex-l", Classes(cut, "D4").Replace("ex-cell", "", StringComparison.Ordinal));
    }

    [Fact] // ADR-0071, ticket 49 (the twelfth Windows run, case 1): where both cells record a line on an edge, the left cell's is drawn and the other's not at all
    public void Where_both_cells_record_a_line_the_left_cells_is_drawn()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(sheet =>
        {
            Format(sheet, "C2", new CellFormatChange { Borders = new BorderChange { Left = new BorderLine(BorderLineStyle.Thin, Blue) } });
            sheet.Enter(CellAddress.Parse("E5"), "1");
            Format(sheet, "E5", new CellFormatChange { Borders = new BorderChange { Right = new BorderLine(BorderLineStyle.Thick, Red) } });
            sheet.Do(SheetEdit.Paste(sheet.Copy(CellRange.Parse("E5")).Block!, CellAddress.Parse("B2")));
        })));

        Assert.Contains("ex-lr-thick-ff0000", Classes(cut, "B2"));
        Assert.Contains("ex-ll-thick-ff0000", Classes(cut, "C2"));
        Assert.DoesNotContain("0000ff", Classes(cut, "B2") + Classes(cut, "C2"));
    }

    [Fact] // ADR-0071, ticket 49: ExSheet answers the core's "which line" by the engine's rule, the upper or left cell's
    public void The_line_an_edge_draws_is_the_upper_or_left_cells()
    {
        var cut = RenderSheet();
        var edge = Grid(cut).Instance.EdgeBorder;

        Assert.NotNull(edge);
        var upper = new Border(BorderStyle.Thick, RgbColour.FromRgb(0xFF0000));
        var lower = new Border(BorderStyle.Thin, RgbColour.FromRgb(0x0000FF));
        Assert.Equal(upper, edge(upper, lower));
    }

    [Fact] // ADR-0071, ticket 49: a line on a whole row is drawn on cells that hold nothing
    public async Task A_line_on_a_whole_row_is_drawn_on_cells_that_hold_nothing()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "4:4");

        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { Borders = new BorderChange { Top = Thin, Bottom = Thin } }));

        // Row 4's top is the bottom of row 3, which holds that gridline.
        Assert.Contains("ex-lb-thin-000000", Classes(cut, "D3"));
        Assert.Contains("ex-lb-thin-000000", Classes(cut, "D4"));
        Assert.DoesNotContain("ex-lb-", Classes(cut, "D5"));
    }

    [Fact] // ADR-0071, SH-4, DC-58, ticket 49: a thick line on a cell repaints its row and the row past each edge it moved, and no other
    public async Task A_line_on_a_cell_repaints_the_rows_either_side_of_its_edge_alone()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(null, ("A1", "1"), ("B4", "x"))));
        await GoToAsync(cut, "B4");
        var before = Rows(cut);

        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { Borders = new BorderChange { Bottom = Thick } }));

        Assert.Contains("ex-lt-thick-000000", Classes(cut, "B5"));
        OnlyRepainted(before, cut, 3, 4);
    }

    [Fact] // ADR-0071, ticket 49: a thick line on a whole row repaints the row, the row above, which holds its top, and the row below, which its bottom reaches into, and no other
    public async Task A_line_on_a_whole_row_repaints_one_row_further_each_side()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(null, ("A1", "1"), ("C8", "x"))));
        await GoToAsync(cut, "5:5");
        var before = Rows(cut);

        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { Borders = BorderChange.Outline(Thick) }));

        Assert.Contains("ex-lb-thick-000000", Classes(cut, "F4"));
        Assert.Contains("ex-lt-thick-000000", Classes(cut, "F6"));
        OnlyRepainted(before, cut, 3, 4, 5);
    }

    [Fact] // ADR-0071, ticket 49: a line on whole columns is drawn on every painted row, and undoing it takes it off every one
    public async Task A_line_on_whole_columns_is_drawn_on_every_painted_row()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "C:D");

        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { Borders = BorderChange.Outline(Thin) }));

        var rows = cut.FindAll(".ex-row").Select(r => r.GetAttribute("aria-rowindex")!).ToList();
        foreach (var row in rows)
        {
            Assert.Contains("ex-lr-thin-000000", Classes(cut, $"B{row}"));
            Assert.Contains("ex-lr-thin-000000", Classes(cut, $"D{row}"));
        }

        Assert.True(await cut.Instance.UndoAsync());
        Assert.Empty(cut.FindAll(".ex-lined"));
    }

    [Fact] // ADR-0071, ticket 49: deleting the row under a line takes it off the row above, which holds nothing and which the engine does not name
    public async Task Deleting_a_row_takes_its_line_off_the_row_above()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(
            sheet => Format(sheet, "A2", new CellFormatChange { Borders = new BorderChange { Top = Thin } }))));
        Assert.Contains("ex-lb-thin-000000", Classes(cut, "A1"));

        await cut.Instance.DoAsync(SheetEdit.DeleteRows(1));

        Assert.DoesNotContain("ex-lb-", Classes(cut, "A1"));
        Assert.Empty(cut.FindAll(".ex-lined"));

        Assert.True(await cut.Instance.UndoAsync());
        Assert.Contains("ex-lb-thin-000000", Classes(cut, "A1"));
    }

    [Fact] // ADR-0071, ticket 49: inserting a row under a line moves the line with its cell, and the row above shows none
    public async Task Inserting_a_row_moves_the_line_with_its_cell()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(
            sheet => Format(sheet, "A2", new CellFormatChange { Borders = new BorderChange { Top = Thin } }))));

        await cut.Instance.DoAsync(SheetEdit.InsertRows(1));

        // A2's top moved down with A2, which is A3 now: the line is under row 2.
        Assert.DoesNotContain("ex-lb-", Classes(cut, "A1"));
        Assert.Contains("ex-lb-thin-000000", Classes(cut, "A2"));
    }
}
