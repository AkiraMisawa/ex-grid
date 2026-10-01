using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0063 (SH-38, SH-44): a Cell Format holds Font, Fill and Borders beside the Number Format and
/// the Alignment, recorded at cell, row and column level, and set by a change that names only the
/// parts it sets, its Borders relative to each range.
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
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("C3")));

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
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("D4")));

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

    [Fact] // ADR-0063 (SH-44): setting a side writes only the cell's own side, never its neighbour's — a reading until the eleventh Windows run, case 7
    public void A_side_is_written_on_its_own_cell_alone_case_7()
    {
        var sheet = NewSheet();
        var thinBlue = new BorderLine(BorderLineStyle.Thin, CellColour.FromRgb(0x0000FF));

        Set(sheet, new CellFormatChange { Borders = new BorderChange { Right = ThickRed } }, "B2");

        Assert.Equal(new CellBorders(Right: ThickRed), sheet.GetBorders(At("B2")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("C2")));

        Set(sheet, new CellFormatChange { Borders = new BorderChange { Left = thinBlue } }, "C2");

        Assert.Equal(new CellBorders(Left: thinBlue), sheet.GetBorders(At("C2")));
        Assert.Equal(new CellBorders(Right: ThickRed), sheet.GetBorders(At("B2")));

        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) }, "B2");

        Assert.Equal(new CellBorders(Left: thinBlue), sheet.GetBorders(At("C2")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("B1")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("A2")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("B3")));
    }

    [Fact] // ADR-0063 (SH-44): None takes every line of the range away, inner edges too, and only the range's cells' own (case 7's reading)
    public void None_clears_every_edge_of_the_range()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) with { InsideHorizontal = Thin, InsideVertical = Thin } }, "A1:D4");

        Set(sheet, new CellFormatChange { Borders = BorderChange.None }, "B2:C3");

        foreach (var cell in new[] { "B2", "C2", "B3", "C3" }) Assert.Equal(CellBorders.None, sheet.GetBorders(At(cell)));
        Assert.Equal(new CellBorders(Thin, Thin, Thin, Thin), sheet.GetBorders(At("A2")));
        Assert.Equal(new CellBorders(Thin, Thin, Thin, Thin), sheet.GetBorders(At("B1")));
    }

    [Fact] // ADR-0063 (SH-38, SH-44): a whole column's outline is its sides on the column, its top and bottom on its first and last cells — a reading until the eleventh Windows run, case 15
    public void A_whole_columns_outline_case_15()
    {
        var sheet = NewSheet();

        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) }, "B:B");

        var document = sheet.ToDocument();
        Assert.Equal(new CellBorders(Left: Thin, Right: Thin), Assert.Single(document.Columns).Borders);
        Assert.Equal(["B1", "B1048576"], document.Cells.Select(c => c.Address).Addresses());
        Assert.Equal(new CellBorders(Top: Thin, Left: Thin, Right: Thin), sheet.GetBorders(At("B1")));
        Assert.Equal(new CellBorders(Left: Thin, Right: Thin), sheet.GetBorders(At("B5")));
        Assert.Equal(new CellBorders(Bottom: Thin, Left: Thin, Right: Thin), sheet.GetBorders(At("B1048576")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("C5")));
    }

    [Fact] // ADR-0063 (SH-38, SH-44): whole rows' outline: the rows record their top and bottom, the Sheet's first and last columns the sides
    public void Whole_rows_outline()
    {
        var sheet = NewSheet();

        Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) }, "3:4");

        Assert.Equal(new CellBorders(Top: Thin), sheet.GetBorders(At("C3")));
        Assert.Equal(new CellBorders(Bottom: Thin), sheet.GetBorders(At("C4")));
        Assert.Equal(new CellBorders(Top: Thin, Left: Thin), sheet.GetBorders(At("A3")));
        Assert.Equal(new CellBorders(Bottom: Thin, Left: Thin), sheet.GetBorders(At("A4")));
        Assert.Equal(new CellBorders(Top: Thin, Right: Thin), sheet.GetBorders(At("XFD3")));
        Assert.Equal(new CellBorders(Bottom: Thin, Right: Thin), sheet.GetBorders(At("XFD4")));
        var document = sheet.ToDocument();
        Assert.Equal(2, document.Rows.Count);
        Assert.Equal(["A3", "XFD3", "A4", "XFD4"], document.Cells.Select(c => c.Address).Addresses());
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
