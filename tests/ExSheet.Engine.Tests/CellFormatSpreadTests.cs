using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// The Cell Formats a range shows, each once (ADR-0071, SH-45): what Format Cells reads to show a
/// part that differs across the Selection, each Border side the cell's own. Read from what is
/// recorded — cells, rows and columns — so that a range of whole columns is answered from its
/// levels, not a million cells.
/// </summary>
public class CellFormatSpreadTests
{
    private static readonly CellFill Yellow = CellFill.Solid(CellColour.FromRgb(0xFFFF00));
    private static readonly NumberFormat Percent = NumberFormat.Parse("0%");
    private static readonly BorderLine Thick = new(BorderLineStyle.Thick);

    private static CellFormat Bold => CellFormat.Default with { Font = new CellFont(Bold: true) };

    private static void Set(Sheet sheet, string range, CellFormatChange change) =>
        sheet.SetCellFormat([CellRange.Parse(range)], change);

    [Fact] // ADR-0071 / SH-45: a range that records nothing shows the default, once
    public void A_range_recording_nothing_shows_the_default()
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "12");

        Assert.Equal([CellFormat.Default], sheet.GetCellFormats(CellRange.Parse("A1:C3")));
    }

    [Fact] // ADR-0071 / SH-45: one cell answers its own Cell Format alone, its column's level under it
    public void One_cell_answers_its_own_cell_format_alone()
    {
        var sheet = NewSheet();
        Set(sheet, "A:A", new CellFormatChange { Fill = Yellow });
        Set(sheet, "A1", new CellFormatChange { Bold = true });

        Assert.Equal([Bold with { Fill = Yellow }], sheet.GetCellFormats(CellRange.Parse("A1")));
    }

    [Fact] // ADR-0071 / SH-45: a cell of its own beside one that records nothing shows both
    public void A_formatted_cell_beside_a_plain_one_shows_both()
    {
        var sheet = NewSheet();
        Set(sheet, "A1", new CellFormatChange { Bold = true, Fill = Yellow });

        var shown = sheet.GetCellFormats(CellRange.Parse("A1:A2"));

        Assert.Equal(2, shown.Count);
        Assert.Contains(Bold with { Fill = Yellow }, shown);
        Assert.Contains(CellFormat.Default, shown);
    }

    [Fact] // ADR-0071 / SH-45 / principle 5: whole columns are answered from their levels
    public void Whole_columns_are_answered_from_their_levels()
    {
        var sheet = NewSheet();
        Set(sheet, "B:B", new CellFormatChange { Bold = true });
        Set(sheet, "C:C", new CellFormatChange { Bold = true });

        Assert.Equal([Bold], sheet.GetCellFormats(CellRange.Parse("B:C")));
    }

    [Fact] // ADR-0071 / SH-45: a row's level over a column's shows where they cross, and each alone elsewhere
    public void A_row_level_over_a_column_level_shows_where_they_cross()
    {
        var sheet = NewSheet();
        Set(sheet, "B:B", new CellFormatChange { Bold = true });
        Set(sheet, "3:3", new CellFormatChange { NumberFormat = Percent });

        var shown = sheet.GetCellFormats(CellRange.Parse("A2:B3"));

        Assert.Equal(4, shown.Count);
        Assert.Contains(CellFormat.Default, shown);
        Assert.Contains(Bold, shown);
        Assert.Contains(CellFormat.Default with { NumberFormat = Percent }, shown);
        Assert.Contains(Bold with { NumberFormat = Percent }, shown);
    }

    [Fact] // ADR-0071 / SH-45: cells that each record the same thing over a level they cover leave the level unseen
    public void Cells_covering_a_level_leave_it_unseen()
    {
        var sheet = NewSheet();
        Set(sheet, "A:A", new CellFormatChange { Fill = Yellow });
        Set(sheet, "A1:A2", new CellFormatChange { Fill = CellFill.None, Bold = true });

        Assert.Equal([Bold], sheet.GetCellFormats(CellRange.Parse("A1:A2")));
    }

    [Fact] // ADR-0071 / SH-45, case 11-24: each cell's own sides are answered, not the edge as shown, since Excel's Format Cells shows a thick bottom over a plain cell as an edge that differs
    public void Each_cells_own_sides_are_answered_case_11_24()
    {
        var sheet = NewSheet();
        Set(sheet, "A1", new CellFormatChange { Borders = new BorderChange { Bottom = Thick } });

        Assert.Equal([CellFormat.Default], sheet.GetCellFormats(CellRange.Parse("A2")));
        Assert.Equal(new CellBorders(Top: Thick), sheet.GetBorders(CellAddress.Parse("A2")));
        Assert.Equal(2, sheet.GetCellFormats(CellRange.Parse("A1:A2")).Count);
    }
}
