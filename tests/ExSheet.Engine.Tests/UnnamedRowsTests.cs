using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0071 (ticket 48): a change says when it may have reformatted rows its
/// <see cref="SheetChange.Rows"/> does not name. A whole row's or column's Cell Format is shown by
/// the cells that hold nothing as well, and <c>Rows</c> names only the rows that hold a cell; rows
/// or columns inserted or deleted bring two cells' sides together on a new edge. A component that
/// paints a Cell Format reads every painted row again after either, and no other change asks it to.
/// </summary>
public class UnnamedRowsTests
{
    private static readonly CellFill Yellow = CellFill.Solid(CellColour.FromRgb(0xFFFF00));
    private static readonly BorderLine Thin = new(BorderLineStyle.Thin);

    private static SheetStep Set(Sheet sheet, CellFormatChange change, params string[] ranges) =>
        sheet.Do(SheetEdit.SetCellFormat([.. ranges.Select(CellRange.Parse)], change));

    [Fact] // ADR-0071, ticket 48: a Fill on a whole column of empty cells names no row, and says it reformatted rows
    public void A_fill_on_an_empty_column_names_no_row_and_reaches_unnamed_rows()
    {
        var sheet = NewSheet();

        var step = Set(sheet, new CellFormatChange { Fill = Yellow }, "F:F");

        Assert.Empty(step.Change.Rows);
        Assert.True(step.Change.ReformatsUnnamedRows);
    }

    [Fact] // ADR-0071, ticket 48: bold on whole rows names only the rows that hold a cell, and reaches the others
    public void Bold_on_whole_rows_names_the_rows_that_hold_a_cell_and_reaches_the_others()
    {
        var sheet = NewSheet();
        sheet.Enter(CellAddress.Parse("B4"), "1");

        var step = Set(sheet, new CellFormatChange { Bold = true }, "3:5");

        Assert.Equal([3], step.Change.Rows);
        Assert.True(step.Change.ReformatsUnnamedRows);
    }

    [Fact] // ADR-0071, ticket 48: undoing and redoing a level change reaches unnamed rows too
    public void Undoing_and_redoing_a_level_change_reaches_unnamed_rows()
    {
        var sheet = NewSheet();
        var step = Set(sheet, new CellFormatChange { Borders = BorderChange.Outline(Thin) }, "C:D");

        Assert.True(step.Undo().ReformatsUnnamedRows);
        Assert.True(step.Redo().ReformatsUnnamedRows);
    }

    [Fact] // ADR-0071, ticket 48: a level change among several ranges reaches unnamed rows
    public void A_level_among_several_ranges_reaches_unnamed_rows()
    {
        var sheet = NewSheet();

        var step = Set(sheet, new CellFormatChange { Fill = Yellow }, "B2:C3", "7:7");

        Assert.Equal([1, 2], step.Change.Rows);
        Assert.True(step.Change.ReformatsUnnamedRows);
    }

    [Fact] // ADR-0071, ticket 48: a level set to what it already shows changes nothing, and reaches nothing
    public void A_level_set_to_what_it_shows_reaches_nothing()
    {
        var sheet = NewSheet();

        var step = Set(sheet, new CellFormatChange { NumberFormat = NumberFormat.General }, "B:B");

        Assert.Empty(step.Change.Rows);
        Assert.False(step.Change.ReformatsUnnamedRows);
    }

    [Fact] // ADR-0071, ticket 48: a Cell Format on cells names their rows and the rows across their edges, and nothing beyond
    public void A_format_on_cells_names_its_rows_and_reaches_no_other()
    {
        var sheet = NewSheet();

        var step = Set(sheet, new CellFormatChange { Fill = Yellow, Borders = BorderChange.Outline(Thin) }, "B3:C4");

        Assert.Equal([1, 2, 3, 4], step.Change.Rows);
        Assert.False(step.Change.ReformatsUnnamedRows);
        Assert.False(step.Undo().ReformatsUnnamedRows);
    }

    [Fact] // ADR-0071, ticket 48: an entry reaches no row it does not name
    public void An_entry_reaches_no_unnamed_row()
    {
        var sheet = NewSheet();

        var step = sheet.Do(SheetEdit.Enter(CellAddress.Parse("A1"), "5"));

        Assert.False(step.Change.ReformatsUnnamedRows);
    }

    [Theory] // ADR-0071, ticket 48: inserting or deleting rows or columns brings sides together on new edges, so it reaches unnamed rows
    [InlineData("insert rows")]
    [InlineData("delete rows")]
    [InlineData("insert columns")]
    [InlineData("delete columns")]
    public void Inserting_or_deleting_reaches_unnamed_rows(string edit)
    {
        var sheet = NewSheet();
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Top = Thin } }, "A3");

        var step = sheet.Do(edit switch
        {
            "insert rows" => SheetEdit.InsertRows(1),
            "delete rows" => SheetEdit.DeleteRows(1),
            "insert columns" => SheetEdit.InsertColumns(0),
            _ => SheetEdit.DeleteColumns(1),
        });

        Assert.True(step.Change.ReformatsUnnamedRows);
        Assert.True(step.Undo().ReformatsUnnamedRows);
        Assert.True(step.Redo().ReformatsUnnamedRows);
    }

    [Fact] // ADR-0071, ticket 48: deleting the row between two edges changes the line an empty row shows, which Rows does not name
    public void Deleting_a_row_changes_the_line_an_empty_row_shows()
    {
        var sheet = NewSheet();
        // A2's top is the edge A1 shows as its bottom; A1 holds nothing.
        Set(sheet, new CellFormatChange { Borders = new BorderChange { Top = Thin } }, "A2");
        Assert.Equal(Thin, sheet.GetBorders(CellAddress.Parse("A1")).Bottom);

        var step = sheet.Do(SheetEdit.DeleteRows(1));

        Assert.True(sheet.GetBorders(CellAddress.Parse("A1")).Bottom.IsNone);
        Assert.DoesNotContain(0, step.Change.Rows);
        Assert.True(step.Change.ReformatsUnnamedRows);
    }
}
