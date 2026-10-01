using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// The Cell Formats a range shows, each once (ADR-0071, SH-45): what Format Cells reads to show a
/// part that differs across the Selection, each Border side the cell's own. And the lines drawn
/// along a range's outer edges, which Format Cells shows as drawn (the fourteenth Windows run, case
/// 13). Both are read from what is recorded — cells, rows and columns — so that a range of whole
/// columns is answered from its levels, not a million cells.
/// </summary>
public class CellFormatSpreadTests
{
    private static readonly CellFill Yellow = CellFill.Solid(CellColour.FromRgb(0xFFFF00));
    private static readonly NumberFormat Percent = NumberFormat.Parse("0%");
    private static readonly BorderLine Thick = new(BorderLineStyle.Thick);
    private static readonly BorderLine Thin = new(BorderLineStyle.Thin);
    private static readonly BorderLine ThinBlue = new(BorderLineStyle.Thin, CellColour.FromRgb(0x0000FF));

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

    // ---- The lines along a range's outer edges ----

    private static IReadOnlySet<BorderLine> Lines(params BorderLine[] lines) => new HashSet<BorderLine>(lines);

    private static void AssertLines(IReadOnlySet<BorderLine> expected, IReadOnlySet<BorderLine> actual, string because) =>
        Assert.True(expected.SetEquals(actual), $"{because}: expected {{{string.Join(", ", expected)}}}, got {{{string.Join(", ", actual)}}}");

    /// <summary>The lines <see cref="Sheet.GetBorders"/> answers along each outer edge, read cell by cell.</summary>
    private static RangeEdgeLines CellByCell(Sheet sheet, CellRange range)
    {
        var (first, last) = (range.First, range.Last);
        IEnumerable<CellAddress> Row(int row) => Enumerable.Range(first.Column, last.Column - first.Column + 1).Select(c => new CellAddress(row, c));
        IEnumerable<CellAddress> Column(int column) => Enumerable.Range(first.Row, last.Row - first.Row + 1).Select(r => new CellAddress(r, column));
        HashSet<BorderLine> Along(IEnumerable<CellAddress> cells, Func<CellBorders, BorderLine> side) => [.. cells.Select(c => side(sheet.GetBorders(c)))];
        return new RangeEdgeLines(Along(Row(first.Row), b => b.Top), Along(Row(last.Row), b => b.Bottom), Along(Column(first.Column), b => b.Left), Along(Column(last.Column), b => b.Right));
    }

    [Fact] // ADR-0071, case 14-13: A2 records nothing under A1's thick bottom, and its top edge is drawn thick, so that is the line along it
    public void A_neighbours_line_is_the_line_along_the_edge_case_14_13()
    {
        var sheet = NewSheet();
        Set(sheet, "A1", new CellFormatChange { Borders = new BorderChange { Bottom = Thick } });

        var a2 = sheet.GetEdgeLines(CellRange.Parse("A2"));

        Assert.Equal([CellFormat.Default], sheet.GetCellFormats(CellRange.Parse("A2")));
        AssertLines(Lines(Thick), a2.Top, "A2's top");
        AssertLines(Lines(BorderLine.None), a2.Bottom, "A2's bottom");
        AssertLines(Lines(BorderLine.None), a2.Left, "A2's left");
        AssertLines(Lines(BorderLine.None), a2.Right, "A2's right");
        AssertLines(Lines(Thick), sheet.GetEdgeLines(CellRange.Parse("A1")).Bottom, "A1's bottom");
    }

    [Fact] // ADR-0071, case 14-13 and the twelfth run: where both cells record a line on the edge, the upper cell's is the one drawn along it, and the left cell's
    public void Where_both_record_a_line_the_upper_or_left_cells_is_drawn()
    {
        var sheet = NewSheet();
        Set(sheet, "B2", new CellFormatChange { Borders = new BorderChange { Bottom = Thick, Right = Thick } });
        Set(sheet, "B4", new CellFormatChange { Borders = new BorderChange { Top = ThinBlue } });
        Set(sheet, "D2", new CellFormatChange { Borders = new BorderChange { Left = ThinBlue } });
        // Row 3 and column C deleted: B3 records its own thin blue top under B2's thick bottom, and C2 its left beside B2's right.
        sheet.DeleteRows(2);
        sheet.DeleteColumns(2);

        AssertLines(Lines(Thick), sheet.GetEdgeLines(CellRange.Parse("B3")).Top, "B3's top");
        AssertLines(Lines(Thick), sheet.GetEdgeLines(CellRange.Parse("C2")).Left, "C2's left");
    }

    [Fact] // ADR-0071, case 14-13: an edge along which the lines differ answers each of them once
    public void Differing_lines_along_an_edge_are_each_answered_once()
    {
        var sheet = NewSheet();
        Set(sheet, "A1", new CellFormatChange { Borders = new BorderChange { Bottom = Thick } });
        Set(sheet, "C3", new CellFormatChange { Borders = new BorderChange { Top = Thin } });

        AssertLines(Lines(Thick, BorderLine.None), sheet.GetEdgeLines(CellRange.Parse("A2:B2")).Top, "A2:B2's top");
        AssertLines(Lines(BorderLine.None, Thin), sheet.GetEdgeLines(CellRange.Parse("B2:C2")).Bottom, "B2:C2's bottom");
    }

    [Fact] // ADR-0071, case 14-13 / principle 5: the left edge of a whole column is answered from the column beside it, and a cell that records its own side is read on its own
    public void A_whole_columns_edge_is_answered_from_the_levels()
    {
        var sheet = NewSheet();
        Set(sheet, "A:A", new CellFormatChange { Borders = new BorderChange { Right = Thick } });

        AssertLines(Lines(Thick), sheet.GetEdgeLines(CellRange.Parse("B:B")).Left, "B:B's left");

        Set(sheet, "A5", new CellFormatChange { Borders = new BorderChange { Right = BorderLine.None } });

        AssertLines(Lines(Thick, BorderLine.None), sheet.GetEdgeLines(CellRange.Parse("B:B")).Left, "B:B's left, A5 recording none");
        AssertLines(Lines(Thick), sheet.GetEdgeLines(CellRange.Parse("B1:B4")).Left, "B1:B4's left");
    }

    [Fact] // ADR-0071, case 14-13 / principle 5: where every place along the edge records a side of its own, the level beside it shows nowhere
    public void Cells_covering_a_level_along_the_edge_leave_it_unseen()
    {
        var sheet = NewSheet();
        Set(sheet, "A:A", new CellFormatChange { Borders = new BorderChange { Right = Thick } });
        Set(sheet, "A1:A2", new CellFormatChange { Borders = new BorderChange { Right = BorderLine.None } });

        AssertLines(Lines(BorderLine.None), sheet.GetEdgeLines(CellRange.Parse("B1:B2")).Left, "B1:B2's left");
    }

    [Fact] // ADR-0071, case 14-13: the lines along a range's edges are the ones drawn there, read cell by cell, over cells, rows, columns and the Sheet's own edges
    public void The_lines_along_the_edges_are_the_ones_drawn_cell_by_cell()
    {
        var sheet = NewSheet();
        Set(sheet, "B:B", new CellFormatChange { Borders = BorderChange.Outline(Thin) });
        Set(sheet, "3:3", new CellFormatChange { Fill = Yellow });
        Set(sheet, "5:5", new CellFormatChange { Borders = new BorderChange { Top = ThinBlue, Bottom = Thick } });
        Set(sheet, "C4:D6", new CellFormatChange { Borders = BorderChange.Outline(Thick) });
        Set(sheet, "A1", new CellFormatChange { Borders = new BorderChange { Top = Thick, Left = Thin } });
        Set(sheet, "E2", new CellFormatChange { Bold = true });
        Set(sheet, "XFD7", new CellFormatChange { Borders = new BorderChange { Right = Thick } });

        foreach (var address in new[] { "A1", "A1:C3", "B2:B6", "C3:E7", "D5", "B:B", "C:D", "5:5", "4:6", "XFC6:XFD8", "A1:XFD1" })
        {
            var range = CellRange.Parse(address);
            var expected = CellByCell(sheet, range);
            var actual = sheet.GetEdgeLines(range);
            AssertLines(expected.Top, actual.Top, $"{address}'s top");
            AssertLines(expected.Bottom, actual.Bottom, $"{address}'s bottom");
            AssertLines(expected.Left, actual.Left, $"{address}'s left");
            AssertLines(expected.Right, actual.Right, $"{address}'s right");
        }
    }
}
