using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0063 (SH-38): Font, Fill and Borders follow every rule the Number Format and the Alignment
/// follow — insertion and deletion move them, an inserted row or column copies the one before it
/// but for its Borders, an ExSheet-to-ExSheet copy, Ctrl+D, Ctrl+R and the fill handle carry them,
/// Delete keeps them, and every operation is one undo step that puts them back exactly. A copy
/// writes the target's own four sides and a structural edit moves each cell with its own, and the
/// edge two cells share shows the upper or left cell's line where both record one (the twelfth
/// Windows run, cases 12-1 to 12-13).
/// </summary>
public class CellFormatCarryTests
{
    private static readonly CellFill Yellow = CellFill.Solid(CellColour.FromRgb(0xFFFF00));
    private static readonly CellFill Blue = CellFill.Solid(CellColour.FromRgb(0x0000FF));
    private static readonly BorderLine Thin = new(BorderLineStyle.Thin);
    private static readonly BorderLine Thick = new(BorderLineStyle.Thick);
    private static readonly BorderLine ThickRed = new(BorderLineStyle.Thick, CellColour.FromRgb(0xFF0000));
    private static readonly BorderLine ThinBlue = new(BorderLineStyle.Thin, CellColour.FromRgb(0x0000FF));
    private static readonly CellFont Bold = new(Bold: true);
    private static readonly CellFont Italic = new(Italic: true);

    private static CellAddress At(string address) => CellAddress.Parse(address);

    private static CellRange Range(string range) => CellRange.Parse(range);

    private static SheetStep Set(Sheet sheet, CellFormatChange change, params string[] ranges) =>
        sheet.Do(SheetEdit.SetCellFormat([.. ranges.Select(CellRange.Parse)], change));

    /// <summary>B5 bold, yellow and outlined; row 7 filled blue; column D italic.</summary>
    private static Sheet Formatted()
    {
        var sheet = NewSheet();
        sheet.Enter("B5", "1");
        Set(sheet, new CellFormatChange { Bold = true, Fill = Yellow, Borders = BorderChange.Outline(Thin) }, "B5");
        Set(sheet, new CellFormatChange { Fill = Blue }, "7:7");
        Set(sheet, new CellFormatChange { Italic = true }, "D:D");
        return sheet;
    }

    [Fact] // ADR-0063 (SH-38): insertion and deletion move Font, Fill and Borders with their cells, rows and columns
    public void Insertion_and_deletion_move_them()
    {
        var sheet = Formatted();

        sheet.InsertRows(0, 2);
        sheet.InsertColumns(0);

        Assert.Equal(new CellFormat(NumberFormat.General, HorizontalAlignment.General, Bold, Yellow, new CellBorders(Thin, Thin, Thin, Thin)), sheet.GetCellFormat(At("C7")));
        Assert.Equal(Blue, sheet.GetFill(At("A9")));
        Assert.Equal(Italic, sheet.GetFont(At("E1")));
        Assert.Equal(CellFont.Default, sheet.GetFont(At("D1")));

        sheet.DeleteRows(0, 2);
        sheet.DeleteColumns(0);

        Assert.Equal(new CellFormat(NumberFormat.General, HorizontalAlignment.General, Bold, Yellow, new CellBorders(Thin, Thin, Thin, Thin)), sheet.GetCellFormat(At("B5")));
        Assert.Equal(Blue, sheet.GetFill(At("A7")));
        Assert.Equal(Italic, sheet.GetFont(At("D1")));
    }

    [Fact] // ADR-0063 (SH-38), cases 11-12 and 12-12: an inserted row takes the Fill and the Font of the row above and not its Borders; its top edge is the one it shares with that row, so it shows that row's bottom, and undo puts back both
    public void An_inserted_row_takes_the_fill_and_not_the_borders_case_11_12()
    {
        var sheet = NewSheet();
        // Case 11-12: B2 filled yellow, its top edge thin and its bottom edge thick; a row inserted at row 3.
        Set(sheet, new CellFormatChange { Fill = Yellow, Borders = new BorderChange { Top = Thin, Bottom = Thick } }, "B2");
        Set(sheet, new CellFormatChange { Italic = true }, "2:2");
        Assert.Equal(new CellBorders(Top: Thick), sheet.GetBorders(At("B3")));
        var before = sheet.ToDocument().ToJson();

        var step = sheet.Do(SheetEdit.InsertRows(2));

        Assert.Equal(Yellow, sheet.GetFill(At("B3")));
        Assert.Equal(new CellBorders(Top: Thick), sheet.GetBorders(At("B3")));
        Assert.Equal(new CellBorders(Top: Thin, Bottom: Thick), sheet.GetBorders(At("B2")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("B4")));
        Assert.True(sheet.GetFill(At("B4")).IsNone);
        Assert.Null(sheet.GetEntry(At("B3")));
        Assert.Null(Assert.Single(sheet.ToDocument().Cells, c => c.Address == At("B3")).Borders);
        Assert.Equal(Italic, sheet.GetFont(At("B3")));
        Assert.Equal(Italic, sheet.GetFont(At("C3")));

        var after = sheet.ToDocument().ToJson();
        step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
        Assert.Equal(new CellBorders(Top: Thick), sheet.GetBorders(At("B3")));
        step.Redo();
        Assert.Equal(after, sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0063 (SH-38), case 12-12: an inserted row takes the Font of the cell above as it takes the Fill, and the cells beside it stay plain
    public void An_inserted_row_takes_the_font_case_12_12()
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "abc");
        var font = new CellFont(CellColour.FromRgb(0xFF0000), Bold: true, Italic: true);
        Set(sheet, new CellFormatChange { Bold = true, Italic = true, FontColour = font.Colour, Fill = Yellow }, "B2");

        sheet.InsertRows(2);

        Assert.Equal(font, sheet.GetFont(At("B3")));
        Assert.Equal(Yellow, sheet.GetFill(At("B3")));
        Assert.Null(sheet.GetEntry(At("B3")));
        foreach (var cell in new[] { "B4", "A3", "C3" }) Assert.Equal(CellFormat.Default, sheet.GetCellFormat(At(cell)));
    }

    [Fact] // ADR-0063 (SH-38), case 12-11: of two inserted rows the first shows the line of the row above on its top, the edges between and below them are empty, and both take the Fill
    public void Of_two_inserted_rows_the_first_shows_the_line_above_case_12_11()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Fill = Yellow, Borders = new BorderChange { Bottom = Thick } }, "B2");

        sheet.InsertRows(2, 2);

        Assert.Equal(new CellBorders(Top: Thick), sheet.GetBorders(At("B3")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("B4")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("B5")));
        foreach (var cell in new[] { "B3", "B4" }) Assert.Equal(Yellow, sheet.GetFill(At(cell)));
        Assert.True(sheet.GetFill(At("B5")).IsNone);
    }

    [Fact] // ADR-0063 (SH-38), case 12-10: a row inserted at row 1 takes nothing, and a line on row 1's top moves down with its cell, so it shows between the new row 1 and row 2
    public void A_line_on_row_1s_top_moves_down_with_it_case_12_10()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Top = Thick } }, "B1");

        sheet.InsertRows(0);

        Assert.Equal(new CellBorders(Bottom: Thick), sheet.GetBorders(At("B1")));
        Assert.Equal(new CellBorders(Top: Thick), sheet.GetBorders(At("B2")));
        var document = sheet.ToDocument();
        Assert.Empty(document.Rows);
        var cell = Assert.Single(document.Cells);
        Assert.Equal((At("B2"), new CellBorders(Top: Thick)), (cell.Address, cell.Borders));
    }

    [Fact] // ADR-0063 (SH-38), case 11-12: recorded on whole rows and whole columns, an inserted row takes no Borders, a column's line runs on through it, and its top shows the bottom of the row above; undo puts back both
    public void An_inserted_row_takes_no_borders_at_any_level_case_11_12()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Fill = Yellow, Borders = BorderChange.Outline(Thin) }, "2:2");
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Left = Thick } }, "C:C");
        var before = sheet.ToDocument().ToJson();

        var step = sheet.Do(SheetEdit.InsertRows(2));

        Assert.Equal(Yellow, sheet.GetFill(At("E3")));
        Assert.Equal(new CellBorders(Top: Thin), sheet.GetBorders(At("E3")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("E4")));
        Assert.Equal(new CellBorders(Top: Thin, Left: Thick), sheet.GetBorders(At("C3")));
        Assert.Equal(new CellBorders(Top: Thin, Right: Thick), sheet.GetBorders(At("B3")));
        Assert.Equal(new CellBorders(Left: Thick), sheet.GetBorders(At("C4")));
        Assert.Equal(new CellBorders(Right: Thick), sheet.GetBorders(At("B4")));
        // The left of column A on row 2 (case 12-14) is not taken either.
        Assert.Equal(new CellBorders(Top: Thin, Bottom: Thin, Left: Thin), sheet.GetBorders(At("A2")));
        Assert.Equal(new CellBorders(Top: Thin), sheet.GetBorders(At("A3")));

        var after = sheet.ToDocument().ToJson();
        step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
        Assert.Equal(new CellBorders(Top: Thin, Left: Thick), sheet.GetBorders(At("C3")));
        step.Redo();
        Assert.Equal(after, sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0063 (SH-38), case 12-13: an inserted column takes the Fill of the one to its left and not its Borders; its left edge shows that column's right line, its right edge is empty, and the cells beside it stay plain
    public void An_inserted_column_takes_the_fill_and_not_the_borders_case_12_13()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Fill = Yellow, Borders = new BorderChange { Right = Thick } }, "B2");

        sheet.InsertColumns(2);

        Assert.Equal(Yellow, sheet.GetFill(At("C2")));
        Assert.Equal(new CellBorders(Left: Thick), sheet.GetBorders(At("C2")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("D2")));
        foreach (var cell in new[] { "D2", "C1", "C3" }) Assert.True(sheet.GetFill(At(cell)).IsNone);
    }

    [Fact] // ADR-0063 (SH-38), case 12-13 at every level: an inserted column takes the Fill and Font of the one to its left, cell and column, and not its Borders; its left edge shows that column's right line, and the column that moved right shows none on its left; undo puts back both
    public void An_inserted_column_takes_no_borders_at_any_level_case_12_13()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Italic = true, Fill = Blue, Borders = BorderChange.Outline(Thick) }, "B:B");
        Set(sheet, new CellFormatChange { Fill = Yellow, Borders = new BorderChange { Right = Thin } }, "B4");
        var before = sheet.ToDocument().ToJson();

        var step = sheet.Do(SheetEdit.InsertColumns(2, 2));

        foreach (var column in new[] { "C", "D" })
        {
            Assert.Equal(Yellow, sheet.GetFill(At(column + "4")));
            Assert.Equal(Italic, sheet.GetFont(At(column + "9")));
            Assert.Equal(Blue, sheet.GetFill(At(column + "9")));
        }
        Assert.Equal(new CellBorders(Left: Thick), sheet.GetBorders(At("C9")));
        Assert.Equal(new CellBorders(Left: Thin), sheet.GetBorders(At("C4")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("D9")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("D4")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("E9")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("E4")));
        Assert.Equal(new CellBorders(Left: Thick, Right: Thick), sheet.GetBorders(At("B9")));
        Assert.Equal(CellFont.Default, sheet.GetFont(At("E9")));

        var after = sheet.ToDocument().ToJson();
        step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
        step.Redo();
        Assert.Equal(after, sheet.ToDocument().ToJson());
    }

    [Theory] // ADR-0063 (SH-38), cases 12-6 and 12-8: two rows brought together by a deletion keep their own sides; where both record a line the upper row's is shown, and the lower row's own stays under it; undo puts the row back
    [InlineData(true)]
    [InlineData(false)]
    public void Rows_brought_together_show_the_upper_rows_line_case_12_6_and_12_8(bool lowerRecordsOne)
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Bottom = Thick } }, "B2");
        if (lowerRecordsOne) Set(sheet, new CellFormatChange { Borders = new BorderChange { Top = ThinBlue } }, "B4");
        var before = sheet.ToDocument().ToJson();

        var step = sheet.Do(SheetEdit.DeleteRows(2));

        Assert.Equal(new CellBorders(Bottom: Thick), sheet.GetBorders(At("B2")));
        Assert.Equal(new CellBorders(Top: Thick), sheet.GetBorders(At("B3")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("B4")));
        step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0063 (SH-38), case 12-7: two columns brought together by a deletion show the left column's line where both record one
    public void Columns_brought_together_show_the_left_columns_line_case_12_7()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Right = Thick } }, "B2");
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Left = ThinBlue } }, "D2");

        sheet.DeleteColumns(2);

        Assert.Equal(new CellBorders(Right: Thick), sheet.GetBorders(At("B2")));
        Assert.Equal(new CellBorders(Left: Thick), sheet.GetBorders(At("C2")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("D2")));
    }

    [Fact] // ADR-0063 (SH-38), case 12-9: where the upper row records no line, the line the lower row records stays, from either side
    public void Rows_brought_together_show_the_lower_rows_line_where_the_upper_has_none_case_12_9()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Top = ThinBlue } }, "B3");

        sheet.DeleteRows(1);

        Assert.Equal(new CellBorders(Bottom: ThinBlue), sheet.GetBorders(At("B1")));
        Assert.Equal(new CellBorders(Top: ThinBlue), sheet.GetBorders(At("B2")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("B3")));
    }

    [Theory] // ADR-0063, ADR-0048 (SH-38): undoing an insertion or deletion puts Font, Fill and Borders back exactly
    [InlineData("insert rows")]
    [InlineData("insert columns")]
    [InlineData("delete rows")]
    [InlineData("delete columns")]
    public void Undoing_a_structural_edit_puts_them_back(string what)
    {
        var sheet = Formatted();
        var before = sheet.ToDocument().ToJson();
        var edit = what switch
        {
            "insert rows" => SheetEdit.InsertRows(5, 3),
            "insert columns" => SheetEdit.InsertColumns(2),
            "delete rows" => SheetEdit.DeleteRows(4, 3),
            _ => SheetEdit.DeleteColumns(1, 3),
        };

        var step = sheet.Do(edit);
        var after = sheet.ToDocument().ToJson();
        Assert.NotEqual(before, after);

        step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
        step.Redo();
        Assert.Equal(after, sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0063, ADR-0048 (SH-38): an ExSheet-to-ExSheet copy carries Font, Fill and Borders as the cells show them, from any level
    public void A_copy_carries_them()
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "1");
        sheet.Enter("B3", "2");
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Left = Thin } }, "B:B");
        Set(sheet, new CellFormatChange { Fill = Yellow }, "3:3");
        Set(sheet, new CellFormatChange { Bold = true }, "B2");

        var block = sheet.Copy(Range("B2:B3")).Block!;

        Assert.Equal((Bold, CellFill.None, new CellBorders(Left: Thin)), (block.CellFormatAt(0, 0).Font, block.CellFormatAt(0, 0).Fill, block.CellFormatAt(0, 0).Borders));
        Assert.Equal((CellFont.Default, Yellow, new CellBorders(Left: Thin)), (block.CellFormatAt(1, 0).Font, block.CellFormatAt(1, 0).Fill, block.CellFormatAt(1, 0).Borders));

        sheet.Do(SheetEdit.Paste(block, At("E5")));

        Assert.Equal(Bold, sheet.GetFont(At("E5")));
        Assert.Equal(new CellBorders(Left: Thin), sheet.GetBorders(At("E5")));
        Assert.Equal(Yellow, sheet.GetFill(At("E6")));
        Assert.Equal(new CellBorders(Left: Thin), sheet.GetBorders(At("E6")));
        Assert.Equal(1, sheet.Number("E5"));
    }

    [Fact] // ADR-0063 (SH-38): a pasted cell records nothing its row or column already gives it
    public void A_paste_records_only_what_differs_from_its_levels()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Left = Thin }, Fill = Yellow }, "B:B");
        sheet.Enter("B2", "1");
        var block = sheet.Copy(Range("B2")).Block!;

        sheet.Do(SheetEdit.Paste(block, At("B10")));
        sheet.Do(SheetEdit.Paste(block, At("D10")));

        var cells = sheet.ToDocument().Cells.ToDictionary(c => c.Address.ToString());
        Assert.Equal((null, null), (cells["B10"].Fill, cells["B10"].Borders));
        Assert.Equal((Yellow, new CellBorders(Left: Thin)), (cells["D10"].Fill, cells["D10"].Borders));
    }

    [Theory] // ADR-0063 (SH-38, SH-23): Ctrl+D and Ctrl+R carry Font, Fill and Borders with the Entries
    [InlineData("B2", "B3:B4", FillDirection.Down, "B4")]
    [InlineData("B2", "C2:D2", FillDirection.Right, "D2")]
    public void The_fill_keys_carry_them(string source, string target, FillDirection direction, string far)
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "1");
        Set(sheet, new CellFormatChange { Italic = true, Fill = Yellow, Borders = BorderChange.Outline(Thick) }, "B2");

        var step = sheet.Do(SheetEdit.FillCopy(Range(source), Range(target), direction));

        Assert.Equal(sheet.GetCellFormat(At("B2")), sheet.GetCellFormat(At(far)));
        Assert.Equal(1, sheet.Number(far));
        step.Undo();
        Assert.Equal(CellFormat.Default, sheet.GetCellFormat(At(far)));
    }

    [Fact] // ADR-0063 (SH-38), case 12-1: a paste writes the target's own four sides and touches no neighbour; where both cells then record a line on an edge, the left cell's is shown
    public void A_paste_writes_the_targets_own_sides_case_12_1()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Left = ThinBlue } }, "C2");
        sheet.Enter("E5", "1");
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Right = ThickRed } }, "E5");

        sheet.Do(SheetEdit.Paste(sheet.Copy(Range("E5")).Block!, At("B2")));

        Assert.Equal(1, sheet.Number("B2"));
        Assert.Equal(new CellBorders(Right: ThickRed), sheet.GetBorders(At("B2")));
        Assert.Equal(new CellBorders(Left: ThickRed), sheet.GetBorders(At("C2")));
        // C2 still records its own line under B2's: the paste did not touch it.
        Assert.Equal(new CellBorders(Left: ThinBlue), Assert.Single(sheet.ToDocument().Cells, c => c.Address == At("C2")).Borders);
    }

    [Fact] // ADR-0063 (SH-38), case 12-2: a paste of a cell with no Borders takes away the line its target recorded, from either side
    public void A_paste_without_borders_takes_the_targets_line_away_case_12_2()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Right = ThickRed } }, "B2");
        sheet.Enter("E5", "1");

        sheet.Do(SheetEdit.Paste(sheet.Copy(Range("E5")).Block!, At("B2")));

        Assert.Equal(CellBorders.None, sheet.GetBorders(At("B2")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("C2")));
    }

    [Theory] // ADR-0063 (SH-38, SH-23), cases 12-3 and 12-5: Ctrl+D and the fill handle write the source's own four sides on each target and touch no neighbour; the edge the source shares with its first target shows the source's bottom, and a target's right line is shown over the one the cell to its right records
    [InlineData(true)]
    [InlineData(false)]
    public void Filling_down_writes_the_targets_own_sides_case_12_3_and_12_5(bool ctrlD)
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "1");
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Right = ThickRed, Bottom = Thick } }, "B2");
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Left = ThinBlue } }, "C3");

        sheet.Do(ctrlD
            ? SheetEdit.FillCopy(Range("B2"), Range("B3:B4"), FillDirection.Down)
            : SheetEdit.Fill(Range("B2"), Range("B3:B4"), FillDirection.Down));

        Assert.Equal(1, sheet.Number("B4"));
        Assert.Equal(new CellBorders(Bottom: Thick, Right: ThickRed), sheet.GetBorders(At("B2")));
        Assert.Equal(new CellBorders(Top: Thick, Bottom: Thick, Right: ThickRed), sheet.GetBorders(At("B3")));
        Assert.Equal(new CellBorders(Top: Thick, Bottom: Thick, Right: ThickRed), sheet.GetBorders(At("B4")));
        Assert.Equal(new CellBorders(Top: Thick), sheet.GetBorders(At("B5")));
        Assert.Equal(new CellBorders(Left: ThickRed), sheet.GetBorders(At("C3")));
        Assert.Equal(new CellBorders(Left: ThickRed), sheet.GetBorders(At("C4")));
        // Each target records the source's own sides, which have no top, and C3 keeps its own.
        var cells = sheet.ToDocument().Cells.ToDictionary(c => c.Address.ToString());
        Assert.Equal(new CellBorders(Bottom: Thick, Right: ThickRed), cells["B3"].Borders);
        Assert.Equal(new CellBorders(Left: ThinBlue), cells["C3"].Borders);
    }

    [Fact] // ADR-0063 (SH-38, SH-23), case 12-4: Ctrl+R writes the source's own four sides on each target and touches no neighbour; the right line of the last target is shown over the one the cell to its right records
    public void Filling_right_writes_the_targets_own_sides_case_12_4()
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "1");
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Bottom = Thick, Right = ThickRed } }, "B2");
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Left = ThinBlue } }, "E2");

        sheet.Do(SheetEdit.FillCopy(Range("B2"), Range("C2:D2"), FillDirection.Right));

        foreach (var cell in new[] { "C2", "D2" }) Assert.Equal(new CellBorders(Bottom: Thick, Left: ThickRed, Right: ThickRed), sheet.GetBorders(At(cell)));
        foreach (var cell in new[] { "C3", "D3" }) Assert.Equal(new CellBorders(Top: Thick), sheet.GetBorders(At(cell)));
        Assert.Equal(new CellBorders(Left: ThickRed), sheet.GetBorders(At("E2")));
        Assert.Equal(new CellBorders(Left: ThinBlue), Assert.Single(sheet.ToDocument().Cells, c => c.Address == At("E2")).Borders);
    }

    [Fact] // ADR-0063 (SH-38): the fill handle carries Font, Fill and Borders with the series it continues
    public void The_fill_handle_carries_them()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        Set(sheet, new CellFormatChange { Bold = true, Borders = new BorderChange { Bottom = Thin } }, "A1:A2");

        sheet.Do(SheetEdit.Fill(Range("A1:A2"), Range("A3:A4"), FillDirection.Down));

        Assert.Equal(4, sheet.Number("A4"));
        Assert.Equal(Bold, sheet.GetFont(At("A4")));
        Assert.Equal(new CellBorders(Bottom: Thin), sheet.GetBorders(At("A4")));
    }

    [Fact] // ADR-0063 (SH-38, SH-24): Delete clears the Entries and keeps Font, Fill and Borders; undo puts the Entries back
    public void Delete_keeps_them()
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "1");
        Set(sheet, new CellFormatChange { Bold = true, Fill = Yellow, Borders = BorderChange.Outline(Thin) }, "B2");
        var shown = sheet.GetCellFormat(At("B2"));

        var step = sheet.Do(SheetEdit.SetEntries([new(At("B2"), null)]));

        Assert.Null(sheet.GetValue(At("B2")));
        Assert.Equal(shown, sheet.GetCellFormat(At("B2")));
        var cell = Assert.Single(sheet.ToDocument().Cells);
        Assert.Null(cell.Entry);
        Assert.Equal(Bold, cell.Font);

        step.Undo();
        Assert.Equal(1, sheet.Number("B2"));
        Assert.Equal(shown, sheet.GetCellFormat(At("B2")));
    }

    [Theory] // ADR-0063, ADR-0048 (SH-38, SH-44): a change over several ranges is one undo step, and undo puts every level back exactly — the cells beside each range included, whose record of an edge it clears (case 11-7), where they lie in another range
    [InlineData("B2:C3", "E5:F6")]
    [InlineData("B:B", "3:4")]
    [InlineData("3:4", "B:B")]
    [InlineData("B2:C3", "C:C", "2:2")]
    [InlineData("A:XFD", "D5")]
    [InlineData("2:4", "B2:C3")]
    [InlineData("B2:C3", "D2:E3")]
    [InlineData("B:B", "C:C")]
    [InlineData("1:1", "B2", "D:D")]
    public void One_undo_step_puts_every_level_back(params string[] ranges)
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "0.5");
        sheet.Enter("C3", "0.25");
        sheet.Enter("D5", "7");
        Set(sheet, new CellFormatChange { Italic = true, Borders = new BorderChange { Left = Thin } }, "C:C");
        Set(sheet, new CellFormatChange { Fill = Blue, Underline = true }, "3:3");
        Set(sheet, new CellFormatChange { Strikethrough = true, Borders = BorderChange.Outline(Thick) }, "B2");
        var before = sheet.ToDocument().ToJson();
        var change = new CellFormatChange
        {
            NumberFormat = NumberFormat.Parse("0.00"),
            Bold = true,
            FontColour = CellColour.FromRgb(0xC00000),
            Fill = Yellow,
            Borders = BorderChange.Outline(Thin) with { InsideHorizontal = BorderLine.None },
        };

        var step = Set(sheet, change, ranges);
        var after = sheet.ToDocument().ToJson();
        Assert.NotEqual(before, after);

        step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
        step.Redo();
        Assert.Equal(after, sheet.ToDocument().ToJson());
        step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
    }
}
