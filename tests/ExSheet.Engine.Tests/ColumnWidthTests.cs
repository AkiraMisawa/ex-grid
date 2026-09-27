using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>ADR-0046 (SH-22): column widths are part of the Sheet Document, in characters (ADR-0047).</summary>
public class ColumnWidthTests
{
    private static CellRange Columns(string text) => CellRange.Parse(text);

    [Fact] // ADR-0046 (SH-22): a width the user never set stays default and is not recorded
    public void A_default_width_is_not_recorded()
    {
        var sheet = NewSheet();

        Assert.Null(sheet.GetColumnWidth(0));
        Assert.Null(sheet.GetColumnWidth(Sheet.ColumnCount - 1));
        Assert.Empty(sheet.ToDocument().ColumnWidths);
        Assert.DoesNotContain("columnWidths", sheet.ToDocument().ToJson(), StringComparison.Ordinal);
    }

    [Fact] // ADR-0046/0048 (SH-22): setting a width is one step on every column the range spans, undone and redone exactly
    public void Setting_a_width_is_one_undoable_step()
    {
        var sheet = NewSheet();
        sheet.Do(SheetEdit.SetColumnWidth(Columns("C:C"), 12));
        var before = sheet.ToDocument().ToJson();

        var step = sheet.Do(SheetEdit.SetColumnWidth(Columns("B:D"), 20.5));

        Assert.Null(sheet.GetColumnWidth(0));
        Assert.Equal(20.5, sheet.GetColumnWidth(1));
        Assert.Equal(20.5, sheet.GetColumnWidth(2));
        Assert.Equal(20.5, sheet.GetColumnWidth(3));
        Assert.Null(sheet.GetColumnWidth(4));
        Assert.Equal([1, 2, 3], step.Change.Columns);
        Assert.Empty(step.Change.Rows);
        Assert.Empty(step.Change.ValueChanges);
        var after = sheet.ToDocument().ToJson();

        var undone = step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
        Assert.Equal(12, sheet.GetColumnWidth(2));
        Assert.Equal([1, 2, 3], undone.Columns);

        var redone = step.Redo();
        Assert.Equal(after, sheet.ToDocument().ToJson());
        Assert.Equal([1, 2, 3], redone.Columns);
    }

    [Fact] // ADR-0046: a range's columns are what it sets, as Excel's Range.ColumnWidth sets them
    public void Any_range_sets_the_columns_it_spans()
    {
        var sheet = NewSheet();

        sheet.SetColumnWidth(Columns("B2:C9"), 15);

        Assert.Equal(15, sheet.GetColumnWidth(1));
        Assert.Equal(15, sheet.GetColumnWidth(2));
        Assert.Null(sheet.GetColumnWidth(3));
    }

    [Fact] // ADR-0046 (SH-22): putting a column back at the default width records nothing again
    public void Null_puts_the_default_width_back()
    {
        var sheet = NewSheet();
        var before = sheet.ToDocument().ToJson();
        sheet.SetColumnWidth(Columns("B:C"), 15);

        var change = sheet.SetColumnWidth(Columns("B:B"), null);

        Assert.Null(sheet.GetColumnWidth(1));
        Assert.Equal(15, sheet.GetColumnWidth(2));
        Assert.Equal([1], change.Columns);
        sheet.SetColumnWidth(Columns("C:C"), null);
        Assert.Equal(before, sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0046: setting the width a column already has changes nothing
    public void Setting_the_same_width_changes_nothing()
    {
        var sheet = NewSheet();
        sheet.SetColumnWidth(Columns("B:B"), 15);

        Assert.Empty(sheet.SetColumnWidth(Columns("B:B"), 15).Columns);
        Assert.Empty(sheet.SetColumnWidth(Columns("C:C"), null).Columns);
    }

    [Theory] // ADR-0046: a width is more than 0 and at most Excel's 255 characters; 0 would hide the column, which is not in the first version
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(255.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_width_outside_Excels_range_is_refused(double width)
    {
        var sheet = NewSheet();

        Assert.Throws<ArgumentOutOfRangeException>(() => SheetEdit.SetColumnWidth(Columns("B:B"), width));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.SetColumnWidth(Columns("B:B"), width));
        Assert.Null(sheet.GetColumnWidth(1));
    }

    [Fact] // ADR-0046: the widest column Excel allows is accepted
    public void The_widest_width_is_accepted()
    {
        var sheet = NewSheet();

        sheet.SetColumnWidth(Columns("B:B"), Sheet.MaxColumnWidth);

        Assert.Equal(255, sheet.GetColumnWidth(1));
    }

    [Fact] // ADR-0046 (SH-22): inserted columns take the width of the column to their left, as they take its formats; the rest move right
    public void Inserted_columns_take_the_width_to_their_left()
    {
        var sheet = NewSheet();
        sheet.SetColumnWidth(Columns("B:B"), 20);
        sheet.SetColumnWidth(Columns("D:D"), 30);
        var before = sheet.ToDocument().ToJson();

        var step = sheet.Do(SheetEdit.InsertColumns(2, 2));

        Assert.Equal(20, sheet.GetColumnWidth(1));
        Assert.Equal(20, sheet.GetColumnWidth(2));
        Assert.Equal(20, sheet.GetColumnWidth(3));
        Assert.Null(sheet.GetColumnWidth(4));
        Assert.Equal(30, sheet.GetColumnWidth(5));
        Assert.Null(sheet.GetColumnWidth(6));
        Assert.Equal([2, 3, 5], step.Change.Columns);

        var undone = step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
        Assert.Equal([2, 3, 5], undone.Columns);
    }

    [Fact] // ADR-0046: a column inserted after a default-width column is at the default width
    public void A_column_inserted_after_a_default_one_is_default()
    {
        var sheet = NewSheet();
        sheet.SetColumnWidth(Columns("C:C"), 20);

        sheet.Do(SheetEdit.InsertColumns(2));

        Assert.Null(sheet.GetColumnWidth(2));
        Assert.Equal(20, sheet.GetColumnWidth(3));
    }

    [Fact] // ADR-0046: columns inserted at A have no column to their left, and take no width
    public void Columns_inserted_at_A_take_no_width()
    {
        var sheet = NewSheet();
        sheet.SetColumnWidth(Columns("A:A"), 20);

        sheet.Do(SheetEdit.InsertColumns(0));

        Assert.Null(sheet.GetColumnWidth(0));
        Assert.Equal(20, sheet.GetColumnWidth(1));
    }

    [Fact] // ADR-0046 (SH-22): deleted columns drop their widths, the rest move left, and undoing puts every width back
    public void Deleted_columns_drop_their_widths()
    {
        var sheet = NewSheet();
        sheet.SetColumnWidth(Columns("B:C"), 20);
        sheet.SetColumnWidth(Columns("E:E"), 30);
        var before = sheet.ToDocument().ToJson();

        var step = sheet.Do(SheetEdit.DeleteColumns(1, 2));

        Assert.Null(sheet.GetColumnWidth(1));
        Assert.Equal(30, sheet.GetColumnWidth(2));
        Assert.Null(sheet.GetColumnWidth(4));
        Assert.Equal([1, 2, 4], step.Change.Columns);

        step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0046: a width pushed off the right edge by an insertion is dropped, and undoing puts it back
    public void A_width_pushed_off_the_edge_comes_back_on_undo()
    {
        var sheet = NewSheet();
        sheet.SetColumnWidth(CellRange.WholeColumns(Sheet.ColumnCount - 1, Sheet.ColumnCount - 1), 20);
        var before = sheet.ToDocument().ToJson();

        var step = sheet.Do(SheetEdit.InsertColumns(0));

        Assert.Empty(sheet.ToDocument().ColumnWidths);
        step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0046: inserting or deleting rows leaves the widths alone
    public void Rows_leave_widths_alone()
    {
        var sheet = NewSheet();
        sheet.SetColumnWidth(Columns("B:B"), 20);

        var inserted = sheet.Do(SheetEdit.InsertRows(0, 3));
        var deleted = sheet.Do(SheetEdit.DeleteRows(0, 2));

        Assert.Equal(20, sheet.GetColumnWidth(1));
        Assert.Empty(inserted.Change.Columns);
        Assert.Empty(deleted.Change.Columns);
    }

    [Fact] // ADR-0046/0048 (SH-22): widths are recorded as runs of adjacent columns and round-trip
    public void Widths_are_recorded_as_runs_and_round_trip()
    {
        var sheet = NewSheet();
        sheet.SetColumnWidth(Columns("B:D"), 20);
        sheet.SetColumnWidth(Columns("E:E"), 12.5);
        sheet.SetColumnWidth(Columns("G:G"), 20);
        sheet.Enter("B2", "1");

        var json = sheet.ToDocument().ToJson();

        Assert.Equal(
            """{"version":4,"culture":"en-US","name":"Sheet1","columnWidths":[{"at":"B:D","width":20},{"at":"E:E","width":12.5},{"at":"G:G","width":20}],"cells":[{"at":"B2","number":1}]}""",
            json);
        var reopened = Sheet.Open(SheetDocument.FromJson(json));
        Assert.Equal(20, reopened.GetColumnWidth(2));
        Assert.Equal(12.5, reopened.GetColumnWidth(4));
        Assert.Null(reopened.GetColumnWidth(5));
        Assert.Equal(json, reopened.ToDocument().ToJson());
    }

    [Fact] // ADR-0046: a width set on every column is one run, and reads back
    public void A_width_on_every_column_round_trips()
    {
        var sheet = NewSheet();
        sheet.SetColumnWidth(CellRange.Parse("A:XFD"), 10);

        var document = sheet.ToDocument();
        var run = Assert.Single(document.ColumnWidths);
        Assert.Equal((0, Sheet.ColumnCount - 1), (run.First, run.Last));

        var reopened = Sheet.Open(SheetDocument.FromJson(document.ToJson()));
        Assert.Equal(10, reopened.GetColumnWidth(Sheet.ColumnCount - 1));
    }

    [Fact] // ADR-0048 (SH-22): a version 3 document still opens, every column at the default width
    public void A_version_3_document_opens_at_default_widths()
    {
        var document = SheetDocument.FromJson("""{"version":3,"culture":"en-US","name":"Sheet1","columns":[{"at":"B:B","format":"0.00"}],"cells":[]}""");

        var sheet = Sheet.Open(document);

        Assert.Empty(document.ColumnWidths);
        Assert.Null(sheet.GetColumnWidth(1));
        Assert.Equal(
            """{"version":4,"culture":"en-US","name":"Sheet1","columns":[{"at":"B:B","format":"0.00"}],"cells":[]}""",
            sheet.ToDocument().ToJson());
    }
}
