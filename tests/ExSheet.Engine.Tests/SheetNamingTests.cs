using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// The name a new Sheet takes, as Excel's (ADR-0331, exsheet-workbook ticket 12's answer 13): each case is one
/// Excel answered, in verification/2026-10-24-windows-workbook-12 (the <c>names</c> group) and
/// verification/2026-10-25-windows-sheet-names (a workbook saved and opened again, copies, the undo of an
/// insertion). The names after each case are Excel's.
/// </summary>
public class SheetNamingTests
{
    private static string[] Names(Workbook workbook) => [.. workbook.Sheets.Select(s => s.Name)];

    private static WorkbookStep AddLast(Workbook workbook) => workbook.Do(workbook.Sheets[^1].Name, SheetEdit.InsertSheet());

    [Fact] // ADR-0331, ticket 12 answer 13 (add-after-gap): Sheet2 deleted, the next is Sheet4
    public void ADR0331_a_deleted_sheet_keeps_its_number_used()
    {
        var workbook = new Workbook(EnUs);
        AddLast(workbook);
        AddLast(workbook);
        workbook.Do("Sheet2", SheetEdit.DeleteSheet());

        AddLast(workbook);

        Assert.Equal(["Sheet1", "Sheet3", "Sheet4"], Names(workbook));
    }

    [Theory] // ADR-0331, ticket 12 answer 13 (add-after-rename, add-after-rename-3, add-after-other): the count, not the names, numbers the next
    [InlineData("Sheet5", new[] { "Sheet5", "Sheet2", "Sheet3" })]
    [InlineData("Sheet3", new[] { "Sheet3", "Sheet2", "Sheet4" })]
    [InlineData("Jan", new[] { "Jan", "Sheet2", "Sheet3" })]
    public void ADR0331_a_renamed_sheet_leaves_the_count_where_it_was(string name, string[] after)
    {
        var workbook = new Workbook(EnUs);
        workbook.Do("Sheet1", SheetEdit.Rename(name));

        AddLast(workbook);
        AddLast(workbook);

        Assert.Equal(after, Names(workbook));
    }

    [Fact] // ADR-0331, ticket 12 answer 13 (add-taken): a Sheet inserted with a name of its own uses a number too
    public void ADR0331_a_sheet_inserted_with_its_own_name_uses_a_number()
    {
        var workbook = new Workbook(EnUs);
        workbook.Do("Sheet1", SheetEdit.Rename("Sheet2"));
        workbook.Do("Sheet2", SheetEdit.InsertSheet("Sheet2x"));

        AddLast(workbook);

        Assert.Equal(["Sheet2", "Sheet2x", "Sheet3"], Names(workbook));
    }

    [Theory] // ADR-0331, 2026-10-25 run (session.jsonl): a copy uses a number, a move or a rename of one added does not give it back
    [InlineData("copy", new[] { "Sheet1", "Sheet1 (2)", "Sheet3", "Sheet4" })]
    [InlineData("copy-twice", new[] { "Sheet1", "Sheet1 (3)", "Sheet1 (2)", "Sheet4", "Sheet5" })]
    [InlineData("add-delete", new[] { "Sheet1", "Sheet3", "Sheet4" })]
    [InlineData("add-rename", new[] { "Sheet1", "Feb", "Sheet3", "Sheet4" })]
    [InlineData("move", new[] { "Sheet2", "Sheet1", "Sheet3", "Sheet4" })]
    public void ADR0331_what_uses_a_number_in_one_session(string id, string[] after)
    {
        var workbook = new Workbook(EnUs);
        switch (id)
        {
            case "copy":
                workbook.Do("Sheet1", SheetEdit.CopySheet());
                break;
            case "copy-twice":
                workbook.Do("Sheet1", SheetEdit.CopySheet());
                workbook.Do("Sheet1", SheetEdit.CopySheet());
                break;
            case "add-delete":
                AddLast(workbook);
                workbook.Do("Sheet2", SheetEdit.DeleteSheet());
                break;
            case "add-rename":
                AddLast(workbook);
                workbook.Do("Sheet2", SheetEdit.Rename("Feb"));
                break;
            case "move":
                AddLast(workbook);
                workbook.Do("Sheet2", SheetEdit.MoveSheet(0));
                break;
        }

        AddLast(workbook);
        AddLast(workbook);

        Assert.Equal(after, Names(workbook));
    }

    [Theory] // ADR-0331, 2026-10-25 run (reopened.jsonl): opened from a document, the count starts at none
    [InlineData(new[] { "Sheet1", "Sheet2", "Sheet3" }, new[] { "Sheet1", "Sheet2", "Sheet3", "Sheet4", "Sheet5" })]
    [InlineData(new[] { "Sheet1", "Sheet3" }, new[] { "Sheet1", "Sheet3", "Sheet2", "Sheet4" })]
    [InlineData(new[] { "Sheet5" }, new[] { "Sheet5", "Sheet1", "Sheet2" })]
    [InlineData(new[] { "Jan", "Feb" }, new[] { "Jan", "Feb", "Sheet1", "Sheet2" })]
    [InlineData(new[] { "Sheet9" }, new[] { "Sheet9", "Sheet1", "Sheet2" })]
    public void ADR0331_an_opened_workbook_numbers_from_the_first_free(string[] saved, string[] after)
    {
        var workbook = Workbook.Open(new Workbook(EnUs, saved).ToDocument());

        AddLast(workbook);
        AddLast(workbook);

        Assert.Equal(after, Names(workbook));
    }

    [Fact] // ADR-0331, 2026-10-25 run (keys-xs.jsonl): an insertion undone gives its number back, a redo takes it again
    public void ADR0331_an_insertion_undone_gives_its_number_back()
    {
        var workbook = new Workbook(EnUs);
        var inserted = AddLast(workbook);
        inserted.Undo();

        AddLast(workbook);
        Assert.Equal(["Sheet1", "Sheet2"], Names(workbook));

        var third = AddLast(workbook);
        third.Undo();
        third.Redo();
        AddLast(workbook);
        Assert.Equal(["Sheet1", "Sheet2", "Sheet3", "Sheet4"], Names(workbook));
    }
}
