using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0063 (SH-38, SH-44): a Cell Format holds Font, Fill and Borders beside the Number Format and
/// the Alignment, recorded at cell, row and column level, and set by a change that names only the
/// parts it sets, its Borders relative to each range. Each cell records its own four sides, and the
/// edge two cells share shows one line from either side, as the eleventh and twelfth Windows runs
/// found Excel's to (cases 11-7, 11-13, 11-15 and 12-14 to 12-16).
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
        // C3 shows the line it shares with B3, which column B records (case 11-7).
        Assert.Equal(new CellBorders(Left: Thin), sheet.GetBorders(At("C3")));

        var document = sheet.ToDocument();
        var column = Assert.Single(document.Columns);
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
        // D3 and D5 show the edges they share with C3 and E5 (case 11-13).
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

    [Fact] // ADR-0063 (SH-44), case 11-7: a border command records the edge on the cell it sets and clears the neighbour's record of it, so the later setting is the line shown from either side; undo puts back both
    public void The_later_setting_of_an_edge_wins_from_either_side_case_11_7()
    {
        var sheet = NewSheet();
        var thinBlue = new BorderLine(BorderLineStyle.Thin, CellColour.FromRgb(0x0000FF));

        Set(sheet, new CellFormatChange { Borders = new BorderChange { Right = ThickRed } }, "B2");

        Assert.Equal(new CellBorders(Right: ThickRed), sheet.GetBorders(At("B2")));
        Assert.Equal(new CellBorders(Left: ThickRed), sheet.GetBorders(At("C2")));
        Assert.Equal(["B2"], sheet.ToDocument().Cells.Select(c => c.Address).Addresses());

        var step = Set(sheet, new CellFormatChange { Borders = new BorderChange { Left = thinBlue } }, "C2");

        Assert.Equal(new CellBorders(Left: thinBlue), sheet.GetBorders(At("C2")));
        Assert.Equal(new CellBorders(Right: thinBlue), sheet.GetBorders(At("B2")));
        // B2's record of the edge is cleared, so C2 alone records it.
        Assert.Equal(["C2"], sheet.ToDocument().Cells.Select(c => c.Address).Addresses());

        step.Undo();

        Assert.Equal(new CellBorders(Right: ThickRed), sheet.GetBorders(At("B2")));
        Assert.Equal(new CellBorders(Left: ThickRed), sheet.GetBorders(At("C2")));
        Assert.Equal(["B2"], sheet.ToDocument().Cells.Select(c => c.Address).Addresses());
    }

    [Fact] // ADR-0063 (SH-38, SH-44), case 11-7: on whole columns and whole rows too, a side is recorded on the level it is set on and the neighbour's record of the edge is cleared, the later setting wins from either side, and undo puts back both
    public void The_later_setting_of_an_edge_wins_at_every_level_case_11_7()
    {
        var sheet = NewSheet();
        sheet.Enter("A5", "1");

        Set(sheet, new CellFormatChange { Borders = new BorderChange { Left = ThickRed } }, "B:B");

        var column = Assert.Single(sheet.ToDocument().Columns);
        Assert.Equal((1, new CellBorders(Left: ThickRed)), (column.First, column.Borders));
        Assert.Equal(new CellBorders(Right: ThickRed), sheet.GetBorders(At("A1048576")));
        // A5, which holds an Entry, shows the same.
        Assert.Equal(new CellBorders(Right: ThickRed), sheet.GetBorders(At("A5")));

        var step = Set(sheet, new CellFormatChange { Borders = new BorderChange { Right = Thin } }, "A:A");

        column = Assert.Single(sheet.ToDocument().Columns);
        Assert.Equal((0, new CellBorders(Right: Thin)), (column.First, column.Borders));
        Assert.Equal(new CellBorders(Left: Thin), sheet.GetBorders(At("B7")));
        Assert.Equal(new CellBorders(Right: Thin), sheet.GetBorders(At("A5")));

        step.Undo();

        Assert.Equal(1, Assert.Single(sheet.ToDocument().Columns).First);
        Assert.Equal(new CellBorders(Left: ThickRed), sheet.GetBorders(At("B7")));
        Assert.Equal(new CellBorders(Right: ThickRed), sheet.GetBorders(At("A7")));

        step = Set(sheet, new CellFormatChange { Borders = new BorderChange { Bottom = Thin } }, "3:3");

        var row = Assert.Single(sheet.ToDocument().Rows);
        Assert.Equal((2, new CellBorders(Bottom: Thin)), (row.First, row.Borders));
        Assert.Equal(new CellBorders(Top: Thin), sheet.GetBorders(At("D4")));
        // Where column B's line crosses row 4, the cell shows both.
        Assert.Equal(new CellBorders(Top: Thin, Left: ThickRed), sheet.GetBorders(At("B4")));

        step.Undo();

        Assert.Empty(sheet.ToDocument().Rows);
        Assert.Equal(new CellBorders(Left: ThickRed), sheet.GetBorders(At("B4")));
    }

    [Fact] // ADR-0063 (SH-44), case 11-13: an outline records the range's outer edges on its own cells, the cells outside it show them, and no edge inside it is set; no borders clears them all
    public void An_outline_is_shown_from_the_cells_outside_it_case_11_13()
    {
        var sheet = NewSheet();

        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) }, "B2:D4");

        Assert.Equal(["B2", "C2", "D2", "B3", "D3", "B4", "C4", "D4"], sheet.ToDocument().Cells.Select(c => c.Address).Addresses());
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

    [Fact] // ADR-0063 (SH-44), case 11-13: no borders takes every line of the range away, inner edges too, and the cells outside it lose their record of the same edges
    public void No_borders_clears_the_edges_the_cells_outside_record_case_11_13()
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
        var cells = sheet.ToDocument().Cells.ToDictionary(c => c.Address.ToString());
        Assert.Equal(new CellBorders(Thin, Thin, Thin, BorderLine.None), cells["A2"].Borders);
        Assert.Equal(new CellBorders(Thin, BorderLine.None, Thin, Thin), cells["B1"].Borders);
    }

    [Fact] // ADR-0063 (SH-44), case 11-13: the cells beside an outline record nothing of it and show it, and a side on the Sheet's outer edge has no cell beside it
    public void An_outline_is_recorded_on_the_ranges_own_cells_case_11_13()
    {
        var sheet = NewSheet();

        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) }, "A1", "XFD1048576");

        Assert.Equal(["A1", "XFD1048576"], sheet.ToDocument().Cells.Select(c => c.Address).Addresses());
        Assert.Equal(new CellBorders(Left: Thin), sheet.GetBorders(At("B1")));
        Assert.Equal(new CellBorders(Top: Thin), sheet.GetBorders(At("A2")));
        Assert.Equal(new CellBorders(Right: Thin), sheet.GetBorders(At("XFC1048576")));
        Assert.Equal(new CellBorders(Bottom: Thin), sheet.GetBorders(At("XFD1048575")));
    }

    [Fact] // ADR-0063 (SH-38, SH-44), case 11-15: an outline over a whole column sets only its left and right edges, recorded on the column; no top of row 1 and no bottom of row 1048576
    public void A_whole_columns_outline_sets_only_its_sides_case_11_15()
    {
        var sheet = NewSheet();

        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) }, "B:B");

        var document = sheet.ToDocument();
        Assert.Empty(document.Cells);
        var column = Assert.Single(document.Columns);
        Assert.Equal((1, new CellBorders(Left: Thin, Right: Thin)), (column.First, column.Borders));
        foreach (var cell in new[] { "B1", "B2", "B5", "B1048575", "B1048576" }) Assert.Equal(new CellBorders(Left: Thin, Right: Thin), sheet.GetBorders(At(cell)));
        // The columns beside it record nothing of it, and show its lines (case 11-13).
        Assert.Equal(new CellBorders(Right: Thin), sheet.GetBorders(At("A5")));
        Assert.Equal(new CellBorders(Left: Thin), sheet.GetBorders(At("C5")));
    }

    [Fact] // ADR-0063 (SH-38, SH-44), case 12-14: an outline over whole rows sets their top and bottom, recorded on the rows, and the left of column A, recorded on its cells; not the right of column XFD
    public void An_outline_over_whole_rows_sets_their_top_and_bottom_and_the_left_of_column_A_case_12_14()
    {
        var sheet = NewSheet();

        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) }, "3:4");

        var document = sheet.ToDocument();
        Assert.Equal([2, 3], document.Rows.Select(r => r.First));
        Assert.Equal(new CellBorders(Top: Thin), document.Rows[0].Borders);
        Assert.Equal(new CellBorders(Bottom: Thin), document.Rows[1].Borders);
        Assert.Equal(["A3", "A4"], document.Cells.Select(c => c.Address).Addresses());
        Assert.Equal(new CellBorders(Top: Thin, Left: Thin), sheet.GetBorders(At("A3")));
        Assert.Equal(new CellBorders(Bottom: Thin, Left: Thin), sheet.GetBorders(At("A4")));
        Assert.Equal(new CellBorders(Top: Thin), sheet.GetBorders(At("C3")));
        Assert.Equal(new CellBorders(Top: Thin), sheet.GetBorders(At("XFD3")));
        Assert.Equal(new CellBorders(Bottom: Thin), sheet.GetBorders(At("XFD4")));
        // The rows beside them record nothing of it, and show its lines (case 11-13).
        Assert.Equal(new CellBorders(Bottom: Thin), sheet.GetBorders(At("C2")));
        Assert.Equal(new CellBorders(Top: Thin), sheet.GetBorders(At("C5")));
    }

    [Fact] // ADR-0063 (SH-38, SH-44), case 12-15: an outline over the whole Sheet sets nothing, and what the rows and cells record stays
    public void An_outline_over_the_whole_sheet_sets_nothing_case_12_15()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Top = ThickRed } }, "3:3");
        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) }, "B5:C6");
        var before = sheet.ToDocument().ToJson();

        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) }, "A:XFD");

        Assert.Equal(before, sheet.ToDocument().ToJson());
        foreach (var cell in new[] { "A1", "XFD1", "A1048576", "XFD1048576" }) Assert.Equal(CellBorders.None, sheet.GetBorders(At(cell)));
        Assert.Equal(new CellBorders(Top: ThickRed), sheet.GetBorders(At("A3")));
    }

    [Fact] // ADR-0063 (SH-38, SH-44), case 12-16: Inside over whole columns is recorded on the columns, so it also shows on the top of row 1 and the bottom of row 1048576, and nothing outside them
    public void Inside_over_whole_columns_shows_on_row_1_and_the_last_row_case_12_16()
    {
        var sheet = NewSheet();

        Set(sheet, new CellFormatChange { Borders = BorderChange.Inside(Thin) }, "B:C");

        var document = sheet.ToDocument();
        Assert.Empty(document.Cells);
        Assert.Equal([(1, 1), (2, 2)], document.Columns.Select(c => (c.First, c.Last)));
        foreach (var row in new[] { "1", "5", "1048576" })
        {
            Assert.Equal(new CellBorders(Thin, Thin, BorderLine.None, Thin), sheet.GetBorders(At("B" + row)));
            Assert.Equal(new CellBorders(Thin, Thin, Thin, BorderLine.None), sheet.GetBorders(At("C" + row)));
            Assert.Equal(CellBorders.None, sheet.GetBorders(At("A" + row)));
            Assert.Equal(CellBorders.None, sheet.GetBorders(At("D" + row)));
        }
    }

    [Fact] // ADR-0063, ADR-0050 item 15 (SH-44): a changed top or bottom side names the row across that edge as well, which shows the line; so do its undo, a paste and a fill
    public void A_changed_edge_names_the_row_across_it()
    {
        var sheet = NewSheet();

        var step = Set(sheet, new CellFormatChange { Borders = new BorderChange { Bottom = ThickRed } }, "B2");

        Assert.Equal([1, 2], step.Change.Rows);
        Assert.Equal([1, 2], step.Undo().Rows);
        Assert.Equal([1], Set(sheet, new CellFormatChange { Borders = new BorderChange { Left = ThickRed } }, "B2").Change.Rows);

        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(ThickRed) }, "B2");
        sheet.Enter("B2", "1");

        Assert.Equal([3, 4, 5], sheet.Do(SheetEdit.Paste(sheet.Copy(CellRange.Parse("B2")).Block!, At("E5"))).Change.Rows);
        Assert.Equal([1, 2, 3, 4], sheet.Do(SheetEdit.FillCopy(CellRange.Parse("B2"), CellRange.Parse("B3:B4"), FillDirection.Down)).Change.Rows);
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
