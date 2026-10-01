using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0063 (SH-38): Font, Fill and Borders follow every rule the Number Format and the Alignment
/// follow — insertion and deletion move them, an inserted row or column copies the one before it,
/// an ExSheet-to-ExSheet copy, Ctrl+D, Ctrl+R and the fill handle carry them, Delete keeps them,
/// and every operation is one undo step that puts them back exactly.
/// </summary>
public class CellFormatCarryTests
{
    private static readonly CellFill Yellow = CellFill.Solid(CellColour.FromRgb(0xFFFF00));
    private static readonly CellFill Blue = CellFill.Solid(CellColour.FromRgb(0x0000FF));
    private static readonly BorderLine Thin = new(BorderLineStyle.Thin);
    private static readonly BorderLine Thick = new(BorderLineStyle.Thick);
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

    [Fact] // ADR-0063 (SH-38): an inserted row takes the Font, Fill and Borders of the one above — for the Borders a reading until the eleventh Windows run, case 12
    public void An_inserted_row_copies_the_one_above_case_12()
    {
        var sheet = NewSheet();
        // Case 12: B2 filled yellow, its top edge thin and its bottom edge thick; a row inserted at row 3.
        Set(sheet, new CellFormatChange { Fill = Yellow, Borders = new BorderChange { Top = Thin, Bottom = Thick } }, "B2");
        Set(sheet, new CellFormatChange { Italic = true }, "2:2");

        sheet.InsertRows(2);

        Assert.Equal(Yellow, sheet.GetFill(At("B3")));
        Assert.Equal(new CellBorders(Top: Thin, Bottom: Thick), sheet.GetBorders(At("B3")));
        Assert.Equal(Italic, sheet.GetFont(At("C3")));
        Assert.Equal(CellBorders.None, sheet.GetBorders(At("B4")));
        Assert.True(sheet.GetFill(At("B4")).IsNone);
        Assert.Null(sheet.GetEntry(At("B3")));
    }

    [Fact] // ADR-0063 (SH-38): an inserted column takes the Cell Format of the one to its left, cell and column
    public void An_inserted_column_copies_the_one_to_its_left()
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Italic = true, Fill = Blue }, "B:B");
        Set(sheet, new CellFormatChange { Fill = Yellow, Borders = new BorderChange { Left = Thin } }, "B4");

        sheet.InsertColumns(2, 2);

        foreach (var column in new[] { "C", "D" })
        {
            Assert.Equal(Yellow, sheet.GetFill(At(column + "4")));
            Assert.Equal(new CellBorders(Left: Thin), sheet.GetBorders(At(column + "4")));
            Assert.Equal(Italic, sheet.GetFont(At(column + "9")));
            Assert.Equal(Blue, sheet.GetFill(At(column + "9")));
        }
        Assert.Equal(CellFont.Default, sheet.GetFont(At("E9")));
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

    [Theory] // ADR-0063, ADR-0048 (SH-38, SH-44): a change over several ranges is one undo step, and undo puts every level back exactly
    [InlineData("B2:C3", "E5:F6")]
    [InlineData("B:B", "3:4")]
    [InlineData("3:4", "B:B")]
    [InlineData("B2:C3", "C:C", "2:2")]
    [InlineData("A:XFD", "D5")]
    [InlineData("2:4", "B2:C3")]
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
