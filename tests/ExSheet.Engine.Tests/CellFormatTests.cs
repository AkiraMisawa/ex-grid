using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0063 (SH-38, SH-44): a Cell Format holds Font, Fill and Borders beside the Number Format and
/// the Alignment, recorded at cell, row and column level, and set by a change that names only the
/// parts it sets, its Borders relative to each range. The line between two cells is one line, as
/// the eleventh Windows run found Excel's to be (cases 7, 13 and 15).
/// </summary>
public class CellFormatTests
{
    private static readonly CellColour Red = CellColour.FromRgb(0xFF0000);
    private static readonly CellFill Yellow = CellFill.Solid(CellColour.FromRgb(0xFFFF00));
    private static readonly CellFill Blue = CellFill.Solid(CellColour.FromRgb(0x0000FF));
    private static readonly BorderLine Thin = new(BorderLineStyle.Thin);
    private static readonly BorderLine ThickRed = new(BorderLineStyle.Thick, Red);

    private static CellAddress At(string address) => CellAddress.Parse(address);

    private static SheetStep Set(Sheet sheet, CellFormatChange change, params string[] ranges) =>
        sheet.Do(SheetEdit.SetCellFormat([.. ranges.Select(CellRange.Parse)], change));

    [Fact] // ADR-0063 (SH-38): a cell no level formats shows the default of every part
    public void A_cell_nothing_formats_shows_the_defaults()
    {
        var sheet = NewSheet();

        Assert.Equal(CellFormat.Default, sheet.GetCellFormat(At("C3")));
        Assert.Equal(CellFont.Default, sheet.GetFont(At("C3")));
        Assert.True(sheet.GetFont(At("C3")).Colour.IsAutomatic);
        Assert.True(sheet.GetFill(At("C3")).IsNone);
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("C3")));
    }

    [Fact] // ADR-0063 (SH-38): Font, Fill and Borders are recorded at three levels, cell over row over column, null inheriting
    public void Cell_over_row_over_column()
    {
        var sheet = NewSheet();

        Set(sheet, new CellFormatChange { Fill = Yellow, Italic = true, Borders = new BorderChange { Left = Thin, Right = Thin } }, "B:B");
        Set(sheet, new CellFormatChange { Fill = Blue, Bold = true }, "3:3");
        Set(sheet, new CellFormatChange { Fill = CellFill.None }, "B3");

        Assert.Equal(Yellow, sheet.GetFill(At("B7")));
        Assert.Equal(Blue, sheet.GetFill(At("D3")));
        Assert.True(sheet.GetFill(At("B3")).IsNone);
        Assert.True(sheet.GetFill(At("A1")).IsNone);
        Assert.Equal(new CellFont(Italic: true), sheet.GetFont(At("B7")));
        Assert.Equal(new CellFont(Bold: true), sheet.GetFont(At("D3")));
        // B3 showed column B's italic when row 3 was made bold, so it keeps it.
        Assert.Equal(new CellFont(Bold: true, Italic: true), sheet.GetFont(At("B3")));
        Assert.Equal(new CellBorders(Left: Thin, Right: Thin), sheet.GetBorders(At("B3")));
        // The line it shares with B3, recorded on column C (case 7).
        Assert.Equal(new CellBorders(Left: Thin), sheet.GetBorders(At("C3")));

        var document = sheet.ToDocument();
        var column = Assert.Single(document.Columns, c => c.First == 1);
        Assert.Equal((1, 1, Yellow, new CellFont(Italic: true)), (column.First, column.Last, column.Fill, column.Font));
        var row = Assert.Single(document.Rows);
        Assert.Equal((2, Blue, new CellFont(Bold: true)), (row.First, row.Fill, row.Font));
        var b3 = Assert.Single(document.Cells);
        Assert.Equal((CellFill.None, new CellFont(Bold: true, Italic: true)), (b3.Fill, b3.Font));
        Assert.Null(b3.Borders);
        Assert.Equal(new CellFormat(NumberFormat.General, HorizontalAlignment.General, new CellFont(Bold: true, Italic: true), CellFill.None, new CellBorders(Left: Thin, Right: Thin)), sheet.GetCellFormat(At("B3")));
    }

    [Fact] // ADR-0063 (SH-44): a change sets only the parts it names, each Font emphasis on its own; every other part stays
    public void A_change_sets_only_what_it_names()
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "0.5");
        sheet.SetNumberFormat(At("B2"), NumberFormat.Parse("0%"));
        Set(sheet, new CellFormatChange { Italic = true, FontColour = Red, Fill = Yellow, Borders = BorderChange.Outline(Thin) }, "B2");

        Set(sheet, new CellFormatChange { Bold = true }, "B2");

        var shown = sheet.GetCellFormat(At("B2"));
        Assert.Equal(new CellFont(Red, Bold: true, Italic: true), shown.Font);
        Assert.Equal(Yellow, shown.Fill);
        Assert.Equal(new CellBorders(Thin, Thin, Thin, Thin), shown.Borders);
        Assert.Equal("0%", shown.NumberFormat.Code);
        Assert.Equal("50%", sheet.GetDisplay(At("B2")).Text);

        Set(sheet, new CellFormatChange { Italic = false, FontColour = CellColour.Automatic, Underline = true, Strikethrough = true }, "B2");

        Assert.Equal(new CellFont(Bold: true, Underline: true, Strikethrough: true), sheet.GetFont(At("B2")));
        Assert.Equal(Yellow, sheet.GetFill(At("B2")));
    }

    [Fact] // ADR-0063 (SH-44): a change that sets Font, Fill or Borders changes no Value, and names the rows it repaints
    public void A_change_names_its_rows_and_changes_no_value()
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "7");

        var step = Set(sheet, new CellFormatChange { Fill = Yellow }, "B2:B3", "D9");

        Assert.Equal([1, 2, 8], step.Change.Rows);
        Assert.Empty(step.Change.ValueChanges);
        Assert.Equal(7, sheet.Number("B2"));
    }

    [Fact] // ADR-0063 (SH-38, SH-44): bold on a whole column keeps each cell's own emphasis, and a row's Font with it
    public void Bold_on_a_whole_column_keeps_each_cells_own_emphasis()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Italic = true }, "B5");
        Set(sheet, new CellFormatChange { Underline = true }, "7:7");

        Set(sheet, new CellFormatChange { Bold = true }, "B:B");

        Assert.Equal(new CellFont(Bold: true, Italic: true), sheet.GetFont(At("B5")));
        Assert.Equal(new CellFont(Bold: true, Underline: true), sheet.GetFont(At("B7")));
        Assert.Equal(new CellFont(Underline: true), sheet.GetFont(At("C7")));
        Assert.Equal(new CellFont(Bold: true), sheet.GetFont(At("B1048576")));
        Assert.Equal(new CellFont(Bold: true), Assert.Single(sheet.ToDocument().Columns).Font);
    }

    [Fact] // ADR-0063 (SH-38, SH-44): bold on whole rows keeps the Font a column gives a cell, and taking it away gives the column's back
    public void Bold_on_whole_rows_keeps_a_columns_font()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Italic = true, FontColour = Red }, "C:C");

        Set(sheet, new CellFormatChange { Bold = true }, "3:4");

        Assert.Equal(new CellFont(Red, Bold: true, Italic: true), sheet.GetFont(At("C3")));
        Assert.Equal(new CellFont(Red, Bold: true, Italic: true), sheet.GetFont(At("C4")));
        Assert.Equal(new CellFont(Bold: true), sheet.GetFont(At("D3")));
        Assert.Equal(new CellFont(Red, Italic: true), sheet.GetFont(At("C5")));

        Set(sheet, new CellFormatChange { Bold = false }, "3:4");

        Assert.Equal(new CellFont(Red, Italic: true), sheet.GetFont(At("C3")));
        Assert.Equal(CellFont.Default, sheet.GetFont(At("D3")));
        var document = sheet.ToDocument();
        Assert.Empty(document.Rows);
        Assert.Empty(document.Cells);
    }

    [Fact] // ADR-0063 (SH-38): no Fill on whole rows is recorded where a column's Fill would otherwise show through
    public void No_fill_on_whole_rows_hides_a_columns_fill()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Fill = Yellow }, "C:C");

        Set(sheet, new CellFormatChange { Fill = CellFill.None }, "3:3");

        Assert.True(sheet.GetFill(At("C3")).IsNone);
        Assert.Equal(Yellow, sheet.GetFill(At("C4")));
        Assert.Equal(CellFill.None, Assert.Single(sheet.ToDocument().Rows).Fill);
    }

    [Fact] // ADR-0063 (SH-38): over the whole Sheet a row's own Font is patched, not lost, and its Fill gives way
    public void The_whole_sheet_patches_a_rows_font()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Italic = true }, "3:3");
        Set(sheet, new CellFormatChange { Fill = Yellow }, "5:5");

        Set(sheet, new CellFormatChange { Bold = true, Fill = Blue }, "A:XFD");

        Assert.Equal(new CellFont(Bold: true, Italic: true), sheet.GetFont(At("C3")));
        Assert.Equal(new CellFont(Bold: true), sheet.GetFont(At("C4")));
        Assert.Equal(Blue, sheet.GetFill(At("C5")));
        var document = sheet.ToDocument();
        var columns = Assert.Single(document.Columns);
        Assert.Equal((0, Sheet.ColumnCount - 1, Blue), (columns.First, columns.Last, columns.Fill));
        var row = Assert.Single(document.Rows);
        Assert.Equal((2, new CellFont(Bold: true, Italic: true), (CellFill?)null), (row.First, row.Font, row.Fill));
    }

    [Fact] // ADR-0063 (SH-44): borders apply to each range, so several ranges get their own outlines
    public void Each_range_gets_its_own_outline()
    {
        var sheet = NewSheet();

        var step = Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) }, "B2:C3", "E5:F6");

        Assert.Equal(new CellBorders(Top: Thin, Left: Thin), sheet.GetBorders(At("B2")));
        Assert.Equal(new CellBorders(Top: Thin, Right: Thin), sheet.GetBorders(At("C2")));
        Assert.Equal(new CellBorders(Bottom: Thin, Left: Thin), sheet.GetBorders(At("B3")));
        Assert.Equal(new CellBorders(Bottom: Thin, Right: Thin), sheet.GetBorders(At("C3")));
        Assert.Equal(new CellBorders(Top: Thin, Left: Thin), sheet.GetBorders(At("E5")));
        Assert.Equal(new CellBorders(Top: Thin, Right: Thin), sheet.GetBorders(At("F5")));
        Assert.Equal(new CellBorders(Bottom: Thin, Left: Thin), sheet.GetBorders(At("E6")));
        Assert.Equal(new CellBorders(Bottom: Thin, Right: Thin), sheet.GetBorders(At("F6")));
        // D4 touches both ranges only at their corners, so no edge of either is one of its sides.
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("D4")));
        Assert.Equal(new CellBorders(Left: Thin), sheet.GetBorders(At("D3")));
        Assert.Equal(new CellBorders(Right: Thin), sheet.GetBorders(At("D5")));

        step.Undo();

        Assert.Empty(sheet.ToDocument().Cells);
    }

    [Fact] // ADR-0063 (SH-44): a single cell's outline is its four sides
    public void A_cells_outline_is_its_four_sides()
    {
        var sheet = NewSheet();

        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(ThickRed) }, "C3");

        Assert.Equal(new CellBorders(ThickRed, ThickRed, ThickRed, ThickRed), sheet.GetBorders(At("C3")));
    }

    [Fact] // ADR-0063 (SH-44): Inside sets every edge between the range's cells, and none of its outer edges
    public void Inside_sets_the_inner_edges()
    {
        var sheet = NewSheet();

        Set(sheet, new CellFormatChange { Borders = BorderChange.Inside(ThickRed) }, "B2:C3");

        Assert.Equal(new CellBorders(Bottom: ThickRed, Right: ThickRed), sheet.GetBorders(At("B2")));
        Assert.Equal(new CellBorders(Bottom: ThickRed, Left: ThickRed), sheet.GetBorders(At("C2")));
        Assert.Equal(new CellBorders(Top: ThickRed, Right: ThickRed), sheet.GetBorders(At("B3")));
        Assert.Equal(new CellBorders(Top: ThickRed, Left: ThickRed), sheet.GetBorders(At("C3")));
    }

    [Fact] // ADR-0063 (SH-44): each edge is set on its own, an edge the change does not name stays, and inside horizontal and vertical are separate
    public void Each_edge_is_set_on_its_own()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) }, "B2:C3");

        Set(sheet, new CellFormatChange { Borders = new BorderChange { Top = ThickRed, InsideVertical = Thin } }, "B2:C3");

        Assert.Equal(new CellBorders(Top: ThickRed, Left: Thin, Right: Thin), sheet.GetBorders(At("B2")));
        Assert.Equal(new CellBorders(Top: ThickRed, Left: Thin, Right: Thin), sheet.GetBorders(At("C2")));
        Assert.Equal(new CellBorders(Bottom: Thin, Left: Thin, Right: Thin), sheet.GetBorders(At("B3")));

        Set(sheet, new CellFormatChange { Borders = new BorderChange { InsideHorizontal = Thin } }, "D5:E5");

        Assert.Equal(CellBorders.None, sheet.GetBorders(At("D5")));
    }

    [Fact] // ADR-0063 (SH-44), the eleventh Windows run, case 7: the line between two cells is one line; setting it from either cell sets both sides, the later setting wins, and undo puts back both
    public void The_line_between_two_cells_is_one_line_case_7()
    {
        var sheet = NewSheet();
        var thinBlue = new BorderLine(BorderLineStyle.Thin, CellColour.FromRgb(0x0000FF));

        Set(sheet, new CellFormatChange { Borders = new BorderChange { Right = ThickRed } }, "B2");

        Assert.Equal(new CellBorders(Right: ThickRed), sheet.GetBorders(At("B2")));
        Assert.Equal(new CellBorders(Left: ThickRed), sheet.GetBorders(At("C2")));

        var step = Set(sheet, new CellFormatChange { Borders = new BorderChange { Left = thinBlue } }, "C2");

        Assert.Equal(new CellBorders(Left: thinBlue), sheet.GetBorders(At("C2")));
        Assert.Equal(new CellBorders(Right: thinBlue), sheet.GetBorders(At("B2")));

        step.Undo();

        Assert.Equal(new CellBorders(Right: ThickRed), sheet.GetBorders(At("B2")));
        Assert.Equal(new CellBorders(Left: ThickRed), sheet.GetBorders(At("C2")));
    }

    [Fact] // ADR-0063 (SH-38, SH-44), the eleventh Windows run, case 7: on whole columns and whole rows too, a side sets the neighbour's, the later setting wins from either side, and undo puts back both
    public void The_line_between_two_columns_or_two_rows_is_one_line_case_7()
    {
        var sheet = NewSheet();
        sheet.Enter("A5", "1");

        Set(sheet, new CellFormatChange { Borders = new BorderChange { Left = ThickRed } }, "B:B");

        Assert.Equal(new CellBorders(Right: ThickRed), Assert.Single(sheet.ToDocument().Columns, c => c.First == 0).Borders);
        Assert.Equal(new CellBorders(Right: ThickRed), sheet.GetBorders(At("A1048576")));
        // A5, which holds an Entry, reads the same.
        Assert.Equal(new CellBorders(Right: ThickRed), sheet.GetBorders(At("A5")));

        var step = Set(sheet, new CellFormatChange { Borders = new BorderChange { Right = Thin } }, "A:A");

        Assert.Equal(new CellBorders(Left: Thin), sheet.GetBorders(At("B7")));
        Assert.Equal(new CellBorders(Right: Thin), sheet.GetBorders(At("A5")));

        step.Undo();

        Assert.Equal(new CellBorders(Left: ThickRed), sheet.GetBorders(At("B7")));
        Assert.Equal(new CellBorders(Right: ThickRed), sheet.GetBorders(At("A7")));

        step = Set(sheet, new CellFormatChange { Borders = new BorderChange { Bottom = Thin } }, "3:3");

        Assert.Equal(new CellBorders(Top: Thin), Assert.Single(sheet.ToDocument().Rows, r => r.First == 3).Borders);
        Assert.Equal(new CellBorders(Top: Thin), sheet.GetBorders(At("D4")));
        // Where column B's line crosses row 4, the cell keeps both.
        Assert.Equal(new CellBorders(Top: Thin, Left: ThickRed), sheet.GetBorders(At("B4")));

        step.Undo();

        Assert.Empty(sheet.ToDocument().Rows);
        Assert.Equal(new CellBorders(Left: ThickRed), sheet.GetBorders(At("B4")));
    }

    [Fact] // ADR-0063 (SH-44), the eleventh Windows run, case 13: an outline sets the range's outer edges, read from the cells outside it too, and no edge inside it; no borders clears them all
    public void An_outline_sets_the_edges_the_cells_outside_read_case_13()
    {
        var sheet = NewSheet();

        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) }, "B2:D4");

        foreach (var row in new[] { "2", "3", "4" })
        {
            Assert.Equal(new CellBorders(Right: Thin), sheet.GetBorders(At("A" + row)));
            Assert.Equal(new CellBorders(Left: Thin), sheet.GetBorders(At("E" + row)));
        }
        foreach (var column in new[] { "B", "C", "D" })
        {
            Assert.Equal(new CellBorders(Bottom: Thin), sheet.GetBorders(At(column + "1")));
            Assert.Equal(new CellBorders(Top: Thin), sheet.GetBorders(At(column + "5")));
        }
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("C3")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("A1")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("E5")));

        Set(sheet, new CellFormatChange { Borders = BorderChange.None }, "B2:D4");

        Assert.Empty(sheet.ToDocument().Cells);
    }

    [Fact] // ADR-0063 (SH-44), the eleventh Windows run, case 13: no borders takes every line of the range away, inner edges too, and the same edges read from the cells outside it
    public void No_borders_clears_the_edges_the_cells_outside_read_case_13()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) with { InsideHorizontal = Thin, InsideVertical = Thin } }, "A1:D4");

        Set(sheet, new CellFormatChange { Borders = BorderChange.None }, "B2:C3");

        foreach (var cell in new[] { "B2", "C2", "B3", "C3" }) Assert.Equal(CellBorders.None, sheet.GetBorders(At(cell)));
        Assert.Equal(new CellBorders(Thin, Thin, Thin, BorderLine.None), sheet.GetBorders(At("A2")));
        Assert.Equal(new CellBorders(Thin, BorderLine.None, Thin, Thin), sheet.GetBorders(At("B1")));
        Assert.Equal(new CellBorders(Thin, Thin, BorderLine.None, Thin), sheet.GetBorders(At("D3")));
        Assert.Equal(new CellBorders(BorderLine.None, Thin, Thin, Thin), sheet.GetBorders(At("C4")));
        Assert.Equal(new CellBorders(Thin, Thin, Thin, Thin), sheet.GetBorders(At("A1")));
    }

    [Fact] // ADR-0063 (SH-44), the eleventh Windows run, case 13: a side on the Sheet's outer edge has no cell beside it to read it
    public void A_side_on_the_sheets_edge_has_no_neighbour_case_13()
    {
        var sheet = NewSheet();

        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) }, "A1", "XFD1048576");

        Assert.Equal(["A1", "B1", "A2", "XFD1048575", "XFC1048576", "XFD1048576"], sheet.ToDocument().Cells.Select(c => c.Address).Addresses());
    }

    [Fact] // ADR-0063 (SH-38, SH-44), the eleventh Windows run, case 15: an outline over a whole column sets only its left and right edges, recorded on the column; no top of row 1 and no bottom of row 1048576
    public void A_whole_columns_outline_sets_only_its_sides_case_15()
    {
        var sheet = NewSheet();

        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) }, "B:B");

        var document = sheet.ToDocument();
        Assert.Empty(document.Cells);
        Assert.Equal([0, 1, 2], document.Columns.Select(c => c.First));
        Assert.Equal(new CellBorders(Left: Thin, Right: Thin), document.Columns[1].Borders);
        foreach (var cell in new[] { "B1", "B2", "B5", "B1048575", "B1048576" }) Assert.Equal(new CellBorders(Left: Thin, Right: Thin), sheet.GetBorders(At(cell)));
        // The columns beside it read the same lines (case 13).
        Assert.Equal(new CellBorders(Right: Thin), sheet.GetBorders(At("A5")));
        Assert.Equal(new CellBorders(Left: Thin), sheet.GetBorders(At("C5")));
    }

    [Fact] // ADR-0063 (SH-38, SH-44), case 15 by mirror: an outline over whole rows sets only their top and bottom edges, recorded on the rows — a reading, not observed
    public void Whole_rows_outline_sets_only_their_top_and_bottom_case_15()
    {
        var sheet = NewSheet();

        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) }, "3:4");

        var document = sheet.ToDocument();
        Assert.Empty(document.Cells);
        Assert.Equal([1, 2, 3, 4], document.Rows.Select(r => r.First));
        Assert.Equal(new CellBorders(Top: Thin), sheet.GetBorders(At("A3")));
        Assert.Equal(new CellBorders(Top: Thin), sheet.GetBorders(At("XFD3")));
        Assert.Equal(new CellBorders(Bottom: Thin), sheet.GetBorders(At("A4")));
        Assert.Equal(new CellBorders(Bottom: Thin), sheet.GetBorders(At("XFD4")));
        // The rows beside them read the same lines (case 13).
        Assert.Equal(new CellBorders(Bottom: Thin), sheet.GetBorders(At("C2")));
        Assert.Equal(new CellBorders(Top: Thin), sheet.GetBorders(At("C5")));
    }

    [Fact] // ADR-0063 (SH-38, SH-44), case 15 by mirror: an outline over the whole Sheet sets only the left of column A and the right of the last column, as its columns do — a reading, not observed
    public void The_whole_sheets_outline_sets_only_its_sides_case_15()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Top = ThickRed } }, "3:3");

        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) }, "A:XFD");

        var document = sheet.ToDocument();
        Assert.Equal([(0, 0), (Sheet.ColumnCount - 1, Sheet.ColumnCount - 1)], document.Columns.Select(c => (c.First, c.Last)));
        Assert.Equal(new CellBorders(Left: Thin), sheet.GetBorders(At("A1")));
        Assert.Equal(new CellBorders(Left: Thin), sheet.GetBorders(At("A1048576")));
        Assert.Equal(new CellBorders(Right: Thin), sheet.GetBorders(At("XFD1")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("C5")));
        // Rows 2 and 3 record the line between them, which hides the columns' sides, so the cells of
        // columns A and XFD on them record their side as their own.
        Assert.Equal(new CellBorders(Top: ThickRed), sheet.GetBorders(At("C3")));
        Assert.Equal(new CellBorders(Top: ThickRed, Left: Thin), sheet.GetBorders(At("A3")));
        Assert.Equal(new CellBorders(Bottom: ThickRed, Right: Thin), sheet.GetBorders(At("XFD2")));
        Assert.Equal(["A2", "XFD2", "A3", "XFD3"], document.Cells.Select(c => c.Address).Addresses());
    }

    [Fact] // ADR-0063 (SH-38, SH-44): Borders on whole rows keep the sides a column gives a cell
    public void Borders_on_whole_rows_keep_a_columns_sides()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Left = ThickRed } }, "C:C");

        Set(sheet, new CellFormatChange { Borders = new BorderChange { InsideHorizontal = Thin } }, "3:5");

        Assert.Equal(new CellBorders(Bottom: Thin, Left: ThickRed), sheet.GetBorders(At("C3")));
        Assert.Equal(new CellBorders(Top: Thin, Bottom: Thin, Left: ThickRed), sheet.GetBorders(At("C4")));
        Assert.Equal(new CellBorders(Top: Thin, Bottom: Thin), sheet.GetBorders(At("D4")));
        Assert.Equal(new CellBorders(Left: ThickRed), sheet.GetBorders(At("C6")));
    }

    [Fact] // ADR-0063: a colour is Automatic or RGB; a Fill is RGB; no line has no colour; nothing else is constructed
    public void The_parts_hold_only_what_they_may()
    {
        Assert.True(default(CellColour).IsAutomatic);
        Assert.Equal(0x00FF7F, CellColour.FromRgb(0x00FF7F).Rgb);
        Assert.Equal(CellColour.FromRgb(0x123456), CellColour.FromRgb(0x12, 0x34, 0x56));
        Assert.Equal("#000000", CellColour.FromRgb(0).ToString());
        Assert.NotEqual(CellColour.Automatic, CellColour.FromRgb(0));
        Assert.Throws<InvalidOperationException>(() => CellColour.Automatic.Rgb);
        Assert.Throws<ArgumentOutOfRangeException>(() => CellColour.FromRgb(0x1000000));
        Assert.Throws<ArgumentOutOfRangeException>(() => CellColour.FromRgb(-1));

        Assert.Throws<ArgumentException>(() => CellFill.Solid(CellColour.Automatic));
        Assert.True(default(CellFill).IsNone);

        Assert.Equal(BorderLine.None, new BorderLine(BorderLineStyle.None, Red));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BorderLine((BorderLineStyle)99));
        Assert.Equal(13, Enum.GetValues<BorderLineStyle>().Count(s => s != BorderLineStyle.None));
    }
}
